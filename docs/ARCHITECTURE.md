# Reactive update architecture

This document describes how project state changes propagate through the application: the reactive compile pipeline, bulk updates, the suppression mechanisms (`IsBulkUpdating`, `MuteWhile`), and the threading rules that keep the whole thing deadlock-free. It reflects the substantial UI/threading rework carried out after v0.9.3 and is written against the code as of August 2026.

> **Transparency note:** many of the refinements documented here - the bulk-update gating, `MuteWhile`, the threading discipline, the performance rework they emerged from, and this document itself - were developed with the help of Claude AI (Anthropic), working alongside the maintainer.

Everything described here centres on [`CoreViewModel`](../src/Zametek.ViewModel.ProjectPlan/CoreViewModel.cs), the hub that owns the project model, and the manager view models (Gantt, resource, earned-value and scenario charts, arrow/vertex graphs, tracking, output, metrics) that hang off it.

---

## 1. The moving parts

- **`CoreViewModel`** owns the editable state (activities, resource / work stream / graph / holiday settings, project start, today), the compiler (`m_VertexGraphCompiler`), the compilation result (`GraphCompilation`), and the derived outputs: `ArrowGraph`, `VertexGraph`, `ResourceSeriesSet`, `TrackingSeriesSet` and the metrics. All mutation of that state is serialized on a single private lock, `m_Lock`.
- **Manager view models** subscribe to `CoreViewModel` properties with ReactiveUI `WhenAnyValue` chains and rebuild their own outputs (plots, graphs, grids) when inputs change. Each one makes its pipeline in `IStartSubscriptions.StartSubscriptions()`, not in its constructor (§7 rule 12), and disposes it in `IKillSubscriptions.KillSubscriptions()`.
- **Schedulers.** Heavy work (compiles, Build\* methods, chart plot builds) runs on `RxSchedulers.TaskpoolScheduler`; only UI delivery uses `RxSchedulers.MainThreadScheduler`. ReactiveUI 24 models the main thread as an `ISequencer` rather than an Rx `IScheduler`; the repo-scoped `ObserveOn(ISequencer)` bridge in [`ObservableExtensions`](../src/Zametek.ViewModel.ProjectPlan/Miscellaneous/ObservableExtensions.cs) exists because importing `ReactiveUI.Primitives` wholesale would make every core Rx operator call ambiguous (it redeclares `Select`, `Where`, `Subscribe`, ...).

## 2. Emissions: what they are, and *when* they happen

An **emission** is a value being pushed through an observable pipeline. In this codebase they come from two sources:

1. **Property change notifications.** When a view model property raises `RaisePropertyChanged`, every `WhenAnyValue` chain watching it re-reads the property getter **synchronously, on the thread that raised the change**, and pushes the value down its pipeline. This is why the raising thread matters, and why some getters must be lock-free (see §7).
2. **DynamicData changesets.** The activity collection is observed via `ToObservableChangeSet()`; `AutoRefresh(activity => activity.IsCompiled)` additionally emits a *Refresh* change whenever that one property changes on any activity. The uncompiled-activities watcher in `CoreViewModel` is built this way.

The single most important distinction in the whole design is **emission time versus delivery time**:

- Operators placed **before** `ObserveOn(...)` run at *emission time* - synchronously, on the thread that pushed the value, at the moment the state change happened.
- The subscriber callback (and anything after `ObserveOn`) runs at *delivery time* - later, on the scheduler's thread, when the world may already have moved on.

Two standing consequences:

- **Gates must sit before `ObserveOn`.** A suppression check such as `.Where(_ => !IsBulkUpdating)` or `.MuteWhile(...)` asks "is a bulk update in progress *right now*?" - a question only meaningful at emission time. If the check ran at delivery time, the deferred taskpool invocation would often execute after the bulk update window had already closed, see a `false` flag, and let a stale trigger through.
- **Handlers must re-read live state.** A delivered payload is a snapshot from its emission time and may be stale. The Build\* handlers therefore treat the payload as a *trigger*, not as data: they take `m_Lock` and read current state. The uncompiled-activities subscriber is the canonical example - its changeset says "something became uncompiled", but by delivery time a load may already have compiled everything, so the handler re-checks `RawActivities.Any(a => !a.IsCompiled)` before arming a compile (a redundant compile would wrongly mark the scenario as modified).

## 3. The regular (reactive) compile path

This is the steady-state flow for a **single edit** - the user changes an activity's duration, adds a dependency, edits a resource setting:

1. The edit marks activities uncompiled / raises a settings property.
2. The uncompiled-activities watcher (`m_AreActivitiesUncompiledSub`) or a settings applier arms the trigger: it first raises `IsReadyToReviseTrackers` (tracker revisions run *inline* during that raise), then sets `IsReadyToCompile = ReadyToCompile.Yes`. `IsReadyToCompile` is deliberately **the last thing set** by every arm site, and it is an enum rather than a bool because repeated `true` assignments would not re-raise (ReactiveUI issue #3846).
3. `m_CompileOnSettingsUpdateSub` observes `IsReadyToCompile` and hops to the taskpool - arming happens on the UI thread, but **the compile must never run there**. Under `m_Lock` it calls `RunAutoCompile()`, which runs `RunCompile()` only if `AutoCompile` is enabled.
4. `RunCompile()` (under `m_Lock`, wrapped in `BeginBusy`/`EndBusy`) feeds resources and work streams to the vertex-graph compiler, then assigns the result to `GraphCompilation`. It also sets `IsProjectScenarioUpdated`, clears `HasStaleOutputs`, and disarms both triggers.
5. The `GraphCompilation` change emission reaches `m_BuildCascadeSub`, which (on the taskpool) calls `RunBuildCascade()`: the seven Build\* methods in dependency order - `BuildArrowGraph` → `BuildVertexGraph` → `BuildResourceSeriesSet` → `BuildTrackingSeriesSet` → `BuildNetworkMetrics` → `BuildRiskMetrics` → `BuildFinancialMetrics` - followed by a bump of `CompilationOutputRevision`, the settled signal (§9).
6. Chart manager view models keyed on `CompilationOutputRevision` rebuild their plots, on the taskpool, once.

Net result: **one edit, one compile, one cascade, one rebuild per chart.**

One deliberate exception: risk metrics have a non-compile trigger. `m_BuildRiskMetricsSub` watches `GraphSettings` directly, because activity-severity settings feed the risk metrics without requiring a recompile.

## 4. Bulk updates

A **bulk update** is one logical operation that rewrites large parts of the model in sequence. There are three entry points, all in `CoreViewModel`:

- `ResetProjectScenario()` - clear everything back to an empty scenario;
- `ProcessProjectScenario(...)` - load a scenario (also the funnel for every scenario switch, and it logs which scenario is in play);
- `ProcessProjectScenarioImport(...)` - import from MS Project / Excel.

Each of these assigns *dozens* of properties: project start, today, display / holiday / work-stream / resource / graph settings, graph layouts, then the entire activity list. If the reactive pipeline stayed live, every intermediate assignment would be a trigger: multiple redundant compiles, cascades running against half-populated state (activities present before their resource settings, or vice versa), and spurious "scenario modified" flags.

The mechanism:

```csharp
try
{
    BeginBulkUpdate();
    lock (m_Lock)
    {
        BeginBusy();
        // ... rewrite the model: settings, layouts, activities ...

        RunCompile();

        // The internal Build* subscriptions drop their emissions during a
        // bulk update, so run the cascade actively while everything is
        // still muted.
        RunBuildCascade();
    }
}
finally
{
    EndBusy();
    EndBulkUpdate();
}
```

Key properties of the mechanism:

- **`IsBulkUpdating` is a ref-counted gate.** `BeginBulkUpdate()` / `EndBulkUpdate()` use an `Interlocked` nesting counter and raise the property change only on the outermost transitions. Nesting is routine: `ProcessProjectScenario` calls `ResetProjectScenario` inside its own bulk window, so both frames hold the gate. Always pair Begin/End in a `try`/`finally`.
- **The core subscriptions drop their emissions** while the gate is up, via `.Where(_ => !IsBulkUpdating)` placed before `ObserveOn` (emission-time check, §2). Dropping is correct - not conflating - because the bulk method itself takes over the pipeline's job.
- **The bulk method drives explicitly.** Once *all* state is in place it calls `RunCompile()` then `RunBuildCascade()` directly, inside the still-muted window. Exactly one compile and one cascade run, at the single moment when every input is consistent. (`ResetProjectScenario` is the exception: it does not compile - it assigns a fresh empty `GraphCompilation` and clears each output directly.)

Contrast of the two modes:

| | Regular compile | Bulk update |
|---|---|---|
| Trigger | One edit | Load / import / reset |
| Who drives | The reactive pipeline | The bulk method, imperatively |
| Pipeline state | Live | Muted (`IsBulkUpdating`) |
| Compile | `RunAutoCompile` via subscription | `RunCompile()` called explicitly |
| Cascade | `m_BuildCascadeSub` via subscription | `RunBuildCascade()` called explicitly |
| Chart rebuilds | One per settled signal | One, replayed at `EndBulkUpdate` |

```mermaid
flowchart TB
    subgraph regular ["Regular edit - the pipeline drives"]
        A["User edit"] --> B["Emission: IsCompiled / settings change"]
        B --> C["Arm IsReadyToCompile<br/>(last thing set, UI thread)"]
        C --> D["CompileOnSettingsUpdateSub<br/>(taskpool, under m_Lock)"]
        D --> E["RunAutoCompile → RunCompile"]
        E --> F["GraphCompilation emission"]
        F --> G["RunBuildCascade<br/>(taskpool, under m_Lock)"]
        G --> H["CompilationOutputRevision bump"]
        H --> I["Chart managers rebuild once"]
    end
    subgraph bulk ["Bulk update - the method drives"]
        J["Load / import / reset"] --> K["BeginBulkUpdate"]
        K --> L["Rewrite model state<br/>(core emissions dropped,<br/>chart emissions conflated)"]
        L --> M["RunCompile - explicit"]
        M --> N["RunBuildCascade - explicit"]
        N --> O["EndBulkUpdate"]
        O --> P["MuteWhile falling edge<br/>replays one value"]
        P --> I
    end
```

## 5. Drop versus conflate: two suppression strategies

The two sides of the `IsBulkUpdating` gate suppress differently, and the difference matches their responsibilities:

- **Core-internal subscriptions DROP** (`.Where(_ => !IsBulkUpdating)`). Their emissions during a bulk update are *redundant*: the bulk method runs the compile and the cascade itself, so nothing is lost by discarding them.
- **Manager view models CONFLATE** (`.MuteWhile(...)`). Nobody rebuilds a Gantt plot on the chart VM's behalf - if its triggers were silently dropped, the chart would still show the previous project after a load. So the muted emissions are conflated to one remembered value that is replayed when the gate falls, producing exactly one rebuild at the end.

Rule of thumb when adding a new subscription: if the bulk update methods already produce your output for you, drop; if you are the only producer of your output, conflate with `MuteWhile`.

## 6. `MuteWhile`: what it is and how it works

`MuteWhile` (in [`ObservableExtensions`](../src/Zametek.ViewModel.ProjectPlan/Miscellaneous/ObservableExtensions.cs)) is a custom Rx operator: it suppresses a source sequence while the latest value of a boolean gate sequence is `true`, remembering only the most recent suppressed value, and replays that one value when the gate falls back to `false`.

Behaviour by gate state:

- **Gate `false` (unmuted):** every source value is forwarded immediately, on the thread that emitted it.
- **Gate `true` (muted):** source values are swallowed; only the latest is remembered, each new arrival overwriting the last (conflation).
- **Falling edge (`true` → `false`):** if anything arrived while muted, the remembered value is forwarded once, on the thread that changed the gate - this is the single "active trigger" at the end of a bulk update. If nothing arrived, nothing is forwarded.

Implementation details that matter to correctness:

- The gate is piped through `DistinctUntilChanged()` - a gate that re-raises `true` repeatedly must not replay the pending value twice.
- The gate is subscribed **before** the source, so an initial gate value (e.g. a `WhenAnyValue` seed) is in place before the source's first emission; otherwise that first value could slip past a gate that should already be closed.
- Each subscription gets its own private state (`Observable.Create` factory).
- All state transitions happen under a small internal lock, but `observer.OnNext` is **always called outside it** - downstream handlers run arbitrary code and must never execute while the operator's lock is held.
- The replayed value is a snapshot from its original emission time. Downstream handlers must read live state rather than trust the payload (§2) - which the Build\* manager subscriptions do.

Canonical usage (from [`GanttChartManagerViewModel`](../src/Zametek.ViewModel.ProjectPlan/GanttChartManagement/GanttChartManagerViewModel.cs)):

```csharp
m_BuildGanttChartPlotModelSub = this
    .WhenAnyValue(
        rcm => rcm.m_CoreViewModel.CompilationOutputRevision,   // settled signal
        rcm => rcm.m_CoreViewModel.ResourceSettings,
        // ... further inputs ...
        (x, ...) => x)
    .MuteWhile(this.WhenAnyValue(rcm => rcm.m_CoreViewModel.IsBulkUpdating))
    .ObserveOn(RxSchedulers.TaskpoolScheduler)
    .Subscribe(async _ => await BuildGanttChartPlotModelAsync());
```

Note the operator order - inputs → `MuteWhile` → `ObserveOn` → handler - and that the mute decision therefore happens at emission time (§2).

## 7. Threading and ordering rules

These rules are load-bearing; several were earned through deadlocks and double-build bugs during the post-v0.9.3 rework.

1. **`m_Lock` serializes model state.** Every mutation of `CoreViewModel` state, every compile, and every Build\* method runs under it.
2. **Compiles and cascades never run on the UI thread.** Arm sites raise triggers wherever they are (often the UI thread); the subscriptions hop to `RxSchedulers.TaskpoolScheduler` before doing work. UI bindings are contending for `m_Lock` at the same time, so nothing avoidable happens inside the locked sections (e.g. `RunCompile` captures values under the lock but logs after releasing it).
3. **Suppression gates sit before `ObserveOn`** so they run at emission time (§2). This applies to both `.Where(_ => !IsBulkUpdating)` and `.MuteWhile(...)`.
4. **`IsBusy` and `IsBulkUpdating` getters are lock-free** (`Volatile.Read` over an `Interlocked` counter). `WhenAnyValue` observers re-read a raised property *synchronously on the raising thread*; if these getters took `m_Lock`, every raise would couple those observers to the lock and invite deadlock.
5. **Never call `BeginBulkUpdate`/`EndBulkUpdate` while holding `m_Lock`.** Raising `IsBulkUpdating` causes its getter to be re-read synchronously by property-chain observers on the raising thread; if that thread held `m_Lock`, it could deadlock against the Build\* subscriptions that serialize on `m_Lock`. This is why the bulk methods call `BeginBulkUpdate()` *before* `lock (m_Lock)` and `EndBulkUpdate()` in the `finally`, after the lock is released.
6. **Never call out to unknown code while holding an internal lock.** `MuteWhile` decides under its lock but forwards (`observer.OnNext`) outside it; `EndBusy` uses a defensive lock-free CAS loop (clamped at zero) rather than a lock at all.
7. **Handlers re-read live state; payloads are stale snapshots** (§2).
8. **Busy/bulk scopes are ref-counted and exception-safe.** `BeginBusy`/`EndBusy` and `BeginBulkUpdate`/`EndBulkUpdate` nest freely (the busy sections routinely call each other) and are always paired through `try`/`finally`, so a throw mid-load cannot leave the UI stuck busy or the pipeline permanently muted.
9. **Property getters that participate in change notification never take application locks.** Rule 4 is the general law, not an `IsBusy` special case, and it was re-earned the hard way: a captured dump (2026-08) showed the UI thread inside a ReactiveUI expression-chain sink - which holds its own internal gate while it re-reads the observed getter by reflection - blocked on `GanttActivitySelectorViewModel`'s `m_Lock`, while the worker holding that lock was raising `PropertyChanged` into the same sink and blocked on its gate: a textbook ABBA deadlock, with the raise-under-lock half supplied invisibly by an `ObservableCollection.CollectionChanged` handler running synchronously inside a locked mutation block. A getter cannot control which locks its readers already hold, so derived values (joined display strings, selected-id lists) are computed under the lock *at write time* and published as immutable snapshots - a plain string, a wholesale-replaced `int[]` - that the getter returns with a lock-free read. See the selector view models' `RefreshDerivedProperties()` (called first in their `Raise*PropertiesChanged()` methods) and the tracker sets' replace-only `m_LastTracker`-style references, whose atomic reference reads need no lock at all. Plain Avalonia bindings, sort comparers and copy-table readers hold no gate and can at worst contend briefly; only a lock-taking getter can complete a deadlock cycle.
10. **Live activity state is compiler state; it is never mutated from a deferred callback.** The activities registered in the vertex graph compiler are the `ManagedActivityViewModel` instances themselves (`IManagedActivityViewModel` extends `IDependentActivity`), so the object a compile clones for scheduling - and writes its results back into - is the same live object the grids edit and the bindings read. The compile side is serialized by `m_Lock` (rule 1), but the UI-side mutators (a grid commit's `EndEdit`, the scalar property setters) hold no lock at all; what keeps them off the compiler's data is ordering, not exclusion - they run on the UI thread and a compile is armed only *after* they finish. A mutation that is *deferred* (queued to another scheduler between the change and the write) breaks that ordering, and the 2026-08-18 ClrMD analysis of `zametek-deadlock-2.dmp` caught the consequence red-handed: a per-activity settings subscription, deferred to the UI thread via `ObserveOn`, ran `HashSet.Clear()` on a live activity's `TargetResources` at the exact moment a scenario-load compile on a worker thread was cloning that activity. The `HashSet` copy constructor memcpys the internals and `Clear()` zeroes the buckets before the entries, so the clone captured a set that enumerates correctly and reports the right `Count` but answers `Contains()` false forever - which starved the resource scheduler (it gates assignment on `Contains`) into the unbounded spin previously parked as the "third dump" edge case. The corruption window is a few instructions wide, which is why it struck rarely and randomly. The rule that follows: anything that mutates an activity's underlying `IDependentActivity` - target sets, dependencies, scalars - must run synchronously on the thread that decides the change, ordered against compiles by `m_Lock` or by arming the compile only afterwards, never through an `ObserveOn` deferral. Settings changes therefore no longer fan out through per-activity subscriptions at all: the `ResourceSettings`/`WorkStreamSettings` setters push into every activity inline (`SetResourceSettings`/`SetWorkStreamSettings`) under `m_Lock`, *before* `IsReadyToCompile` arms the compile, and each activity seeds itself from the current settings (including the write-back that prunes target ids referring to since-removed resources) in its constructor. The same now goes for the date-derived scalars, which were the last per-activity subscriptions left and which taught the rule's second half: **`Scheduler.CurrentThread` is a deferral too.** `MinimumEarliestStartTime` and `MaximumLatestFinishTime` are counted from the project start along the working calendar, so the project start, the holiday settings and the non-working day mode each invalidate them; all three used to arrive through `ObserveOn(Scheduler.CurrentThread)`, which looks synchronous and usually is - the trampoline runs the action inline when nothing else holds it - but queues the work behind the current action whenever the write is made from inside another scheduled delivery, which is the ordinary case once the cascade is running. The delivery then landed *after* the same setter had armed the compile, so a compile could start against counts belonging to the previous calendar; three tests that make the change from inside `Scheduler.CurrentThread.Schedule` and read the activity before that action returns caught it on all three triggers. They now push in synchronously like the settings: `ProjectStart` calls `SetProjectStart`, `HolidaySettings` calls `UpdateEarliestStartAndLatestFinishDateTimes`, and `ProjectScenarioDisplaySettingsViewModel.NonWorkingDayMode` calls the latter through a core callback that takes no lock, because that setter can still be inside the display settings lock and taking `m_Lock` there would recreate the lock-order edge of rule 6. What may still be deferred is the one thing that mutates nothing: the calculator's *display* mode, which only re-raises the properties that render a date. Rule 11 closes the rest of it.
11. **A compilation reads and writes a copy of the plan, never the live activities.** Rule 10 removed the systematic racer, but it left the ordinary one: the compile runs on the taskpool, so a grid edit committed while a previously-armed compile is still in flight mutates the very activity the compiler is cloning - and, in the other direction, the compiler writes its results (times, slack, allocated resources, resource dependencies, successors) *into* the live activities as it calculates, racing binding reads and `DeepCopy()` saves for the whole duration of the compile. [`RunCompile`](../src/Zametek.ViewModel.ProjectPlan/CoreViewModel.cs) therefore runs **snapshot -> compile -> publish**: `SnapshotCompiler()` copies every activity (`CloneObject()`) into a throwaway `VertexGraphCompiler`, the compile runs against those copies, and `PublishCompilation()` hands each live activity its own results back through `SetCompiledValues`. The activities own both halves of that exchange, so the field list that a compilation produces lives next to the fields themselves rather than in a loop somewhere else, and an activity can assert that the results it is being given are its own. Publishing writes *only* what a compilation produces and never an activity's own inputs (duration, targets, constraints, user-set dependencies), which is what lets an edit made mid-compile survive it. Crucially, **it publishes the times through the activity's own setters**, because those setters are how a compilation's results have always reached the view: the compiler held the view models *themselves* as its graph nodes, so `node.Content.EarliestStartTime = ...` was a call to `ManagedActivityViewModel`'s setter, which announces the value and everything derived from it (the date offsets, latest start, total and interfering slack, criticality, the dependency and successor strings). Writing underneath those setters leaves the values correct but unannounced - the charts and the output window still update, because they read the published `GraphCompilation`, while the activities grid keeps showing the previous compilation's numbers. The collections are the only part written directly, having no setters of their own; they are written first, so the announcements the times make cover the strings derived from them. This is also why `PublishCompilation` does **not** hold the leaf lock across its pass: publishing announces, and nothing may be announced under that lock, so each activity takes it for its own writes and announces after releasing it - results therefore arrive one activity at a time, exactly as they did when the compiler wrote them as it calculated. None of this arms another compilation: an activity arms one only by being marked uncompiled, which follows from an edit being committed (`IEditableObject.EndEdit`), never from a value being announced (`m_AreActivitiesUncompiledSub` auto-refreshes on `IsCompiled` alone). **Exclusion is a leaf lock** (`m_ActivityDataLock`, created by the core and shared with every activity): held around the snapshot pass, the publish pass, and each individual write inside an activity - and *never* around a raise, a call to anything else, or another lock. It is strictly inside `m_Lock` in the lock order and is the only lock the activities take, so it cannot complete a cycle; being reentrant, an activity re-taking it inside a publish pass costs nothing while the outer acquisition keeps the whole pass atomic. Two consequences worth knowing: the copies must be made **in the order the live graph holds its activities** (`VertexGraphCompiler.ActivityIds`), because the scheduling priority list breaks a slack tie in favour of whichever activity it meets first; and a compilation that is abandoned - timed out, or failed - now publishes *nothing*, so the previous results stay in place whole rather than half-overwritten. The live compiler keeps its own graph for everything between compilations (`GetNextActivityId`, dependency edits, `TransitiveReduction`, `IsIsolated`) and stays correct without compiling, because a compilation is net-zero on graph structure and it derives the project start and finish by reading them off the live activities that publishing has just updated. Not covered: the tracker sets, which guard their own state and hold immutable records, and the compile still runs under `m_Lock` (releasing it is a separate change - see TODO).
12. **A constructor starts no pipeline that delivers on another thread; `StartSubscriptions()` does.** A subscription that hops threads - `ObserveOn` the task pool or the main-thread scheduler - and is made in a constructor hands its current value to another thread at once, while the constructor is still running and whether or not anyone wanted it. On the desktop that was latent: the constructors happened to subscribe last, so each object was whole by the time the UI thread delivered. In a headless host it was not, because there the main-thread scheduler is the thread pool, so every such pipeline raced the job that had just built the view model. The engine killed each view model as it resolved it, but a delivery already on its way could not be stopped: a job built the compile's outputs a second time in about two jobs in five, and a chart's plot again in up to half of them, on a pool thread, beside the job's own build and export. (A cancellation test that failed on the CI build server was counting those builds.) A view model therefore sets up its state and its *inert* plumbing in its constructor, and its pipelines in [`StartSubscriptions()`](../src/Zametek.Contract.ProjectPlan/IStartSubs.cs), which does its work once, and not at all after `KillSubscriptions()`. The desktop and the browser start every view model their container builds, as soon as it is built and before it is handed on, so each is started in the order the constructors ran ([`StartSubscriptionsModule`](../src/Zametek.Shell.ProjectPlan/StartSubscriptionsModule.cs)); a view model that makes others for itself - the core's activities, the project manager's nodes, a chart manager's selector - starts them when it is started, or as it makes them if it has been; and the engine starts none. What stays in a constructor is what delivers on the thread that raised the change: `ToProperty` helpers, `Scheduler.CurrentThread` subscriptions, and the events between a view model and its own collections. So a view model that is never started is inert but still correct. The converse is the trap: whatever an engine needs a pipeline to deliver must be done explicitly, because it will not be. The Gantt and EV selectors therefore take the filter a plan has saved in their constructors, as the EV selector always did, where the Gantt one had relied on a delivery to the pool winning its race against the job's build. [`UnstartedViewModelTests`](../test/Zametek.Engine.ProjectPlan.Tests/UnstartedViewModelTests.cs) holds both schedulers while a job runs on a real plan and fails if a view model hands anything to either, or if the job's outputs differ from those of the same job with them free. [`StartableViewModelTests`](../test/Zametek.Engine.ProjectPlan.Tests/StartableViewModelTests.cs) and the `*StartTests` of the view model tests say it of each view model on its own, and say the other half: that starting one starts what it should, once, and killing it stops what it started.

## 8. `IsBusy` versus `IsBulkUpdating`

They look similar (both ref-counted, both raised on outermost transitions) but serve different masters, and confusing them causes subtle bugs:

- **`IsBusy` is a UI-facing signal only** - busy indicators, disabled controls. It must **never** gate reactive behaviour; use `IsBulkUpdating` or dedicated flags for that. (Its doc comment in `CoreViewModel` states this contract.)
- **`IsBulkUpdating` is the behavioural gate** described in §4–§5.

The UI consumption of `IsBusy` has its own refinement, in [`MainViewModel`](../src/Zametek.ViewModel.ProjectPlan/MainViewModel.cs): the busy overlay is *immediate* for long-form operations (project loads, scenario processing - `IsMainBusy` and the scenario manager's `IsBusy`) but *delayed by 250 ms* for the core compile signal, via a `Timer`/`Switch` pipeline. Quick auto-compiles finish well inside the delay, so an ordinary edit never flips the overlay on and off (which would restyle large parts of the window); clearing is immediate, and rapid busy/idle flips inside the window collapse to no visible change.

## 9. The settled signal: `CompilationOutputRevision`

A compile produces its outputs across seven Build\* steps. A chart that watched, say, `ResourceSeriesSet` *and* `GraphCompilation` would rebuild twice per compile - once per input, possibly seeing mixed generations of state in between. `RunBuildCascade()` therefore bumps `CompilationOutputRevision` (an `int`, incremented modulo a wrap constant) **after all seven outputs are in place**, and chart managers key their rebuild pipelines on that revision instead of on the individual outputs: one compile means exactly one settled raise means exactly one rebuild.

## 10. Maintaining display order: input surfaces versus display-only surfaces

Several panels present rows or sections "in resource order" - the order the user arranged in the Resource Settings grid (drag to reorder; [`UpdateDisplayOrders`](../src/Zametek.ViewModel.ProjectPlan/ResourceSettingsManagement/ResourceSettingsManagerViewModel.cs) stamps `DisplayOrder` as a *descending* rank, so the grid's top row holds the highest value). Two different mechanisms keep that order correct, chosen by which side of the compile the surface sits on.

**Input surfaces mirror the live settings collections.** The effort timesheet ([`EffortTrackingManagerViewModel`](../src/Zametek.ViewModel.ProjectPlan/TrackingManagement/EffortTrackingManagerViewModel.cs)) renders the live `IManagedResourceViewModel` instances themselves: its sections bind to the same objects the settings grid edits, and there is no compiled snapshot in between. It therefore observes the settings panel's `OrderableResources` (and the core's `OrderableActivities`) through `Observable.FromEventPattern` collection-changed bridges merged into its refresh trigger, and rebuilds its sections the moment a drag lands. One data source, so mirroring its order live is unambiguous; comparing the live *instances* rather than ids in `RefreshTimesheet` also stops the sections driving stale, disposed view models after a scenario switch.

**Display-only surfaces take their order from the cascade.** Compiled outputs - the charts, the Metrics panel, the Resource Metrics panel - are snapshots produced by the Build\* cascade, and their ordering is baked into the rebuilt output itself. The per-resource metrics are the canonical example: `MetricCalculationService.BuildFinancialMetrics` emits the `ResourceMetrics` list already sorted the way the settings grid displays its rows (descending `DisplayOrder`, ties broken by descending resource id, which drops the implicit/spare series to the bottom), so the panel simply renders list order and the persisted scenario stores that same order. No `Orderable*` event bridges, no sibling view-model coupling, and no second source of truth to reconcile.

The reason display-only surfaces must not borrow the input-surface technique is epoch consistency. A compiled snapshot is keyed by the resource ids *as of the last compile*, while the live collections change ids and membership immediately: renumbering rewrites every id at once, and adding or deleting resources creates rows with no snapshot entry (or entries with no row). Joining live order onto snapshot values would tear during every stale window and need last-known-order bookkeeping to avoid transient scrambles. The cascade route sidesteps all of it: a reorder or renumber funnels through `UpdateResourceSettingsToCore()`, the `ResourceSettings` setter arms `IsReadyToCompile`, the auto-compile runs, and the rebuild delivers ids, values and order together, atomically - with `HasStaleOutputs` flagging the gap in between. A pure drag-reorder does trigger a full recompile (the settings record comparison sees the `DisplayOrder` change); that is the existing, accepted cost of keeping everything downstream consistent.

The rule of thumb: **if a surface renders the live settings objects, mirror the `Orderable*` collections; if it renders compiled output, bake the order into the output at build time.**

## 11. The headless engine: the pattern at its extreme

The headless engine ([`JobRunner`](../src/Zametek.Engine.ProjectPlan/JobRunner.cs)), which the `zpp` command-line tool runs on - one job per process, or many side by side in its server, `zpp serve` - is the bulk update idea taken to its logical end. Each job runs in a DI scope of its own, disposed when the job ends. Within it, the engine resolves each view model and never starts it (§7 rule 12), so no reactive pipeline of theirs exists at all; it sets `core.AutoCompile = false`, and drives everything explicitly: `RunCompile()` followed by the Build\* calls in the same dependency order as `RunBuildCascade`. Seams that only the reactive pipeline used to reach must be public for this to work - e.g. `IProjectScenarioManagerViewModel.BuildTrackedMetrics()`, which the GUI invokes via a subscription but the engine must call directly before building the scenario chart.

Jobs run side by side, and each produces exactly what it would alone, so two rules hold for everything a job reaches:

- **No view model calls `Dispatcher.UIThread`.** Work that only the UI thread may do goes through [`IUIDispatcher`](../src/Zametek.Contract.ProjectPlan/Miscellaneous/IUIDispatcher.cs) (and, inside the graph library, [`IGraphDispatcher`](../src/Zametek.Graphs.Avalonia/Abstractions/IGraphDispatcher.cs)). The desktop's implementations are the UI thread, exactly as before; the engine's run the work inline, because a job has no UI thread and nothing pumps one - work posted there would be held, with the job's whole scope, for the life of the process. Views keep using the dispatcher directly.
- **Nothing reads the wall clock.** The time comes from the scope's `IDateTimeCalculator` (`GetLocalNow()`, `GetLocalToday()`), never `DateTime.Now`, `DateTime.Today` or `DateTimeOffset.Now`, and a library that stamps the clock itself (NPOI, on every workbook it writes) is overridden. A job's calculator reads a `JobClock`, which is the host's clock unless the request fixes the time it runs at (`JobRequest.Now`, `zpp --now`). The time zone comes from the calculator too (`LocalTimeZone`): a file older than v0.3.0 holds its times without an offset from UTC, and opening one reads them in that zone.

The one documented exception to identical outputs is the ids a job mints - for an import's new project and scenario, or when a pre-v0.6 file is upgraded - which stay random.

`zpp serve` ([`JobServer`](../src/Zametek.ProjectPlan.CommandLine/JobServer.cs), with its API in [`ProjectEndpoints`](../src/Zametek.ProjectPlan.CommandLine/ProjectEndpoints.cs)) is where those rules carry weight: one process, one engine container for its whole life, and as many jobs at once as its limits allow, each in its own scope. The container is built by the same code as `zpp`'s (`Program.BuildServices`), but validated as it is built, so that a singleton holding a job-scoped service - which would carry one job's state into every other - stops the server starting; the web host has a container of its own, and takes only the `JobRunner` from the engine's. The server stops a job whose caller has gone, or whose time has run out, through the token `JobRunner.RunAsync` checks between steps - a step already under way, a compile or a chart being drawn, finishes first, the compile within its watchdog. Two more rules keep the server's answers exactly what `zpp` would have printed and written, and its jobs apart:

- **What `zpp` prints goes through [`IJobConsole`](../src/Zametek.ProjectPlan.CommandLine/Contracts/IJobConsole.cs).** `zpp`'s console is the process's, and its [`IJobSink`](../src/Zametek.Engine.ProjectPlan/Contracts/IJobSink.cs) writes files; the server's are a job's own, in memory, and its console records each call - with each output where the job produced it - as the answer's transcript, which a client plays back through zpp's own console and sink. The text both print comes from [`JobConsoleHelper`](../src/Zametek.ProjectPlan.CommandLine/Miscellaneous/JobConsoleHelper.cs), so anything written to `Console` directly would reach `zpp`'s terminal and never the server's answer. `ProjectEndpointsParityTests` runs `zpp` and the server on the same plans and compares the exit codes, the text and every file, byte for byte.
- **A job chooses nothing the process holds.** The culture is the process's, set once, when the server starts and before anything formats a date - `DateTimeCalculator` reads its date formats in its type initializer - and the time zone is the process's too (`TimeZoneInfo.Local`). A job can fix the time it runs at, because its clock is its scope's, but not its culture or its time zone: those are the server's - `--culture` sets the one, and `TZ`, where the platform reads it, the other - and `/v1/info` reports both.

A Unix domain socket, when the server listens on one, is its access control, so the socket is left to the user running the server before anybody can connect to it. [`UnixSocketFileHelper`](../src/Zametek.ProjectPlan.CommandLine/Miscellaneous/UnixSocketFileHelper.cs) is installed as the socket transport's `CreateBoundListenSocket`, which binds the socket, restricts its file - to an access list holding that user alone on Windows, where a socket has the access of its folder, and to mode 600 elsewhere - and only then lets Kestrel listen: a socket that is bound and not yet listening refuses every connection, so there is no moment at which another user could connect, and a server that cannot restrict its socket does not start. The same class makes the path ready before the engine starts: it removes the socket that a killed server left behind - only when nothing answers on it and the file is empty (and, on Windows, a socket) - and nothing else.

`zpp --server` ([`JobClient`](../src/Zametek.ProjectPlan.CommandLine/JobClient.cs)) is the server's other half: it sends a run to a server as a job, and plays the answer's transcript back through `zpp`'s own console and file sink ([`JobTranscriptHelper`](../src/Zametek.ProjectPlan.CommandLine/Miscellaneous/JobTranscriptHelper.cs)), so that the run prints, writes and exits as it would have here - and fails as it would: a chart or a graph that cannot be written is reported and the run carries on, a project or an export that cannot be written stops it. A server that does not run the job ends the run with an exit code of its own, 5, so that a pipeline can tell a server it could try again, or do without, from a job that failed. `JobClientParityTests` runs `zpp`'s `Main` here and on a server on the same command lines and compares the exit codes, the text and every file, byte for byte. Two rules of its own keep it so:

- **The client names its files, and checks what `zpp` checks, itself.** Each output is written where `zpp`'s options say, under the name `Program.BuildOutputFilenames` gives it, as a run here names it - never the name in the answer, which the server chose without knowing this machine, and through which an answer could write anywhere. Whatever `zpp` refuses before a job runs - its options, a plan or a directory that is not there, an export to a file it does not write - the client refuses before anything is sent, as `zpp` refuses it; the server cannot, because it is never sent the paths.
- **The client never touches the engine.** `Main` decides where the run goes once its options are parsed and checked, before anything builds the engine or so much as loads its container: running here is a method of its own (`RunHereAsync`), and what the client takes from the engine's assemblies is types and file-name helpers that load nothing of the view models ([`FileFormatHelper`](../src/Zametek.ViewModel.ProjectPlan/Miscellaneous/FileFormatHelper.cs)). A run on a server loads no ReactiveUI, Avalonia, Skia or ASP.NET Core, and the client could move into a binary of its own without a redesign, should machines that only ever send runs to a server turn up.

## 12. Diagnostics

[`CascadeDiagnostics`](../src/Zametek.ViewModel.ProjectPlan/Miscellaneous/CascadeDiagnostics.cs) is a dormant tracing facility left in place from the performance investigation: call sites throughout the cascade record markers, build counts, collection-change notifications and stack traces, but every call is compiled away unless the `CASCADE_DIAGNOSTICS` symbol is defined (instructions are in the class comment; output goes to `Debug.WriteLine` and is mirrored to `zametek-cascade-diagnostics.log` in the user's temp directory, so a Debug build can simply be run - no debugger attached - and the trace collected from the file afterwards). Re-enable it when verifying gating behaviour - e.g. that a load produces exactly one compile and one cascade - or when hunting redundant rebuilds and deadlocks.
