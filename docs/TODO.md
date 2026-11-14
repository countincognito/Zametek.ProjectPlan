# TODO

This file lists the maintainer-internal engineering intents that must version with the source code. GitHub issues take the bugs and the feature requests of users. Items that stay valid after the spin-out of Zametek.Graphs.Avalonia belong in [src/Zametek.Graphs.Avalonia/TODO.md](../src/Zametek.Graphs.Avalonia/TODO.md). Put a date on each entry when you add it. Delete the entry when you complete the work.

- [ ] **Bound the work after the compile** *(2026-11-13, from the stress test of 0.11.0-beta.1)*

  Since 0.11.0-beta.1, the effort series no longer reads each activity for each resource on each day. The series of 1500 activities and 500 resources took 34 seconds and 38 GB of allocations, and it now takes 1.3 seconds and 278 MB. A job of that plan now takes 9 seconds, and not 60. The rest of this entry is from before the change. Measure it again, and then decide what to bound. A job still builds each output of the cascade two times (refer to the entry about the two compiles of a job).

  The test compiles one network of 1500 activities with 10, 50, 100, 200 and 500 developers, on a server with no other jobs. The jobs take 5.4, 7.8, 13.1, 21.6 and 51.5 seconds. Each job adds 107, 107, 108, 161 and 740 MB to the memory of the server. The compile limit does not stop this work. With a `compileTimeout` of PT5S, the plan with 200 developers gets 200 after 25 seconds. With PT1S, it gets `compilation-timed-out` after 3.9 seconds.

  At the limits of the compiler (2000 activities and 1000 resources), one job takes 214 seconds, and the server uses 2.9 GB at the peak. With the default job limit of 120 seconds, the job fails after it holds the memory for two minutes. Thus, four such jobs at one time can use 12 GB. Find the work that grows with the activities and the resources. The allocation streams of each resource are one possible cause: they have one value for each day. Then bound the work: make it smaller, put it under the compile limit, or give the server a limit of memory.

  The Linux test of the same day measured the job at the limits after the change. It took 15 seconds on Windows, and 17.6 seconds on Linux (WSL, 2 processors). But the server used 4.5 GB at the peak on Windows (3.5 GB on Linux), and it kept 3.4 GB after the job.

  In the stress test, a long run of 440 jobs gave its memory back. Thus, this is probably the garbage collector, which keeps its heap. By default, the server runs as many jobs at one time as the computer has processors. Thus, on a computer with 2 processors and 8 GB, two such jobs fill the memory. Measure the job with `DOTNET_GCConserveMemory` or with a limit of the heap, together with point 5 of the entry "Package `zpp serve`".

- [ ] **Stop the cache of non-working days from taking a gap as known** *(2026-11-13, found during the fix of a constraint date on a non-working day)*

  `AppendNonWorkingDays` in `DateTimeCalculator` searches the non-working days of a range. Then it makes the known span reach the range. When the range does not touch the span, the days between them become known, but the method did not search them. Their non-working days then count as working days. The first fix of a constraint date on a non-working day checked the date before it counted from the project start. Then a minimum earliest start of 220 became 306.

  The counts of the days start at the project start, and the shades of the Gantt chart go from one day to the next. Thus, nothing in the application gets a gap now, except maybe a shade before the project start. Make the span grow only next to itself: search the days from the span to the new range. Do not search the whole range to a date that is far away, for example in the year 9999. That can take a long time (refer to the entry about the five inputs that give 500).

- [ ] **When the schedule counts hours, store the times of a plan without an offset** *(2026-11-13, a decision of the maintainer)*

  Since 0.11.0-beta.1, a date of a plan is the day that it shows, in its own offset (a70c2e1c). The schedule counts whole days, so the time of the day and the offset have no effect. The file keeps a `DateTimeOffset` for each date, and the code must not read such a value as a moment. A plain date (`DateOnly`) was another option, but it removes the time of the day, which hours need.

  If the schedule counts hours one day, keep the dates and the times, and remove the offset. Store each time of the plan as the time on the clock of the plan, for example `2027-01-04T09:00:00`. Do this in one change of the version of the file, as for the entry about `Duration`. A working day of 09:00 to 17:30 then stays 09:00 to 17:30 in each time zone. A change of the clock does not move it either.

  The version of the file tells the migration that the earlier times have no meaning, so it keeps only their dates. Hours that a person works on a day do not need this change, because a tracker counts them for a day. A log of work as moments, with a start and a stop from a clock, needs a time zone of the plan. The zone puts each moment on a day and an hour of the plan.

- [ ] **Build with the .NET 11 SDK** *(2026-11-13, found when CI failed)*

  `global.json` keeps the build on .NET 10, with the newest .NET 10 SDK of the computer. On 2026-11-13, CI failed in the build of `Zametek.ViewModel.ProjectPlan`. The task `MavenReferenceItemResolve` of IKVM.Maven.Sdk 1.12.1 failed with `MSB4217` ("Task host node exited prematurely") and `MSB4027` (a disposed `MetadataLoadContext`). IKVM.Maven.Sdk runs its tasks in a task host (`TaskHostFactory`).

  The image of the runner, version 20261111, added the .NET 11 SDK (11.0.100), and nothing pinned the SDK. Before the move to .NET 11, find a version of IKVM.Maven.Sdk that works with the MSBuild of .NET 11, or report the problem to ikvmnet. Then change `global.json`.

- [ ] **Install the MSI of the final 0.11.0 over a 0.11.0 pre-release** *(2026-11-12, do before the final 0.11.0)*

  Each MSI of 0.11.0 has the version 0.11.0.0: the alpha, the beta and the final release. Since 0.11.0-beta.1, `<MajorUpgrade>` in `pkg/Zametek.ProjectPlan.MsiPackager/Package.wxs` has `AllowSameVersionUpgrades="yes"`. Thus, the MSI removes an installed product of the same version. The Upgrade table of an MSI that was built from beta.1 shows it. Its row that removes the earlier versions has the attributes 513, which include the maximum of 0.11.0.0. The MSI of the beta had 1.

  On 2026-11-12, the maintainer removed the MSI files of v0.11.0-alpha and v0.11.0-beta from their releases, and the next pre-releases have no MSI. But a user who installed one of these MSI files has 0.11.0.0. Before the final release, install its MSI over the beta, and make sure that the installed apps show one entry.

- [ ] **Let the export to Excel take a resource tracker with two entries for one activity** *(2026-11-13, found during the fix of B10)*

  A resource tracker can have two entries for one activity on one day. Then `zpp -x` fails with exit code 1, and the message is "An item with the same key has already been added". The application does not write such a tracker, but a program can. The tracking series adds the two entries. Add them in the export too, or refuse such a tracker in the check before the load (`duplicateId`).

- [ ] **Suggestion: give the headcount for each day in the data output** *(2026-11-12, a suggestion from phase 1 of the zpp skill)*

  `outputs.data` writes `-resources.csv`, with one row for each resource on each day. It has no total for each day. The Excel export has this total, in the column `Total` of the sheet "Resource Chart (Activity)". Add the headcount for each day to the data output. For example, add a file `-headcount.csv` with the columns `Time` and `Headcount`.

  The CSV has the data now: on each day, count the rows where `Allocated` is `true`. On `docs/assets/sample.zpp`, this count is the same as the column `Total` on each of the 210 days, and its maximum is the `PeakHeadcount` of 10. But the sheet "Resource Chart (Activity)" counts the column `Allocated`, not the column `Activity`. A count of `Activity` gives a different curve, with a maximum of 6. Thus, the file is a convenience, and it also prevents a count of the wrong column.

- [ ] **Suggestion: let the scheduler prefer task continuity** *(2026-11-12, a suggestion from phase 1 of the zpp skill)*

  The priority list gives an activity to the first free resource in the allocation order. Righting Software also asks for task continuity. When activity A depends on activity B, the developer of B also does A. That developer knows the context, and has a stake in B.

  In the worked example of the zpp skill, the developer of Claims Store stops on day 50. Then `zpp` gives ClaimsAccess, which depends on Claims Store, to the developer of Security on day 55. The book keeps the developer of Claims Store, who then waits for 5 days. Thus, the schedule of `zpp` costs 5 person-days less, and the schedule of the book keeps the continuity.

  An option of the scenario can make the scheduler prefer a free resource that did a predecessor of the activity. It must be an option, because it can add idle days.

  The priority list is in the library Zametek.Maths.Graphs (`PriorityListResourceScheduler`, and `scheduling/scheduler.rs` in Rust). Thus, the change starts in the library. Today, `TargetResources` can keep an activity on one resource, but the user must set it on each activity.

- [ ] **Put Inter into the chart SVG files** *(2026-11-12, from the smoke test of v0.11.0-beta)*

  The chart SVG files (Gantt, resource, earned value, scenario and simulation) do not contain the font Inter. Windows 11 does not install Inter. Until 2026-11-12, the SVG named only `Inter`, and thus Microsoft Edge showed the text in its default serif font. Now each text names fallback fonts after Inter (`ChartFonts.SvgFontFamilies`), and Edge shows the text in Segoe UI.

  But the SVG gives the position of each letter from the measurements of Inter, for example `x="0, 8.897727"`. Thus, with a fallback font, the spaces between the letters are not regular. For example, Edge shows "Requirements & Architecture" as "Requirements &Architecture".

  To show the text exactly as the PNG does, put Inter into the SVG with `@font-face`. Each file then becomes larger by the size of the font, unless the export keeps only the letters that the chart uses. Skia can change text to paths in an SVG, but SkiaSharp does not give access to this option (checked in 3.119.4 and 4.148.0).

- ~~**Suggestion: tell how far a plan misses its deadline**~~ DECLINED on 2026-11-12. *(2026-11-11, a suggestion from the design of the zpp skill)*

  A frequent question is "can we finish by day T with these people?". The suggestion was a maximum LFT on the end milestone, with negative total slack as in Microsoft Project. On 2026-11-11, the maintainer decided against it. A maximum LFT pins an activity, and constraints of that kind make the compile too complex. A deadline is only a guideline.

  Add the deadline to the job: an option such as `--deadline` (`deadline` in the API). The plan does not keep it, and the compile does not change. The answer gives the shortfall: the finish of the plan minus the deadline, or 0. A simulation also gives the share of the iterations that finish by the deadline, as the target of a simulation does now.

  On 2026-11-12, the maintainer declined the suggestion, because it adds little to the target of the simulation. The target gives the share of the iterations that finish by it. A client can calculate the shortfall from `projectFinishDays`. A deadline lets a request ask about a date without a change to the plan. It also changes a date to working days in the calendar of the plan. These two gains do not justify the change.

- [ ] **Do not save the calculated `Duration` in the next version of the file format** *(2026-11-11)*

  The estimates are the input: `MostLikelyDuration`, and `OptimisticDuration` and `PessimisticDuration` when the activity has them. `Duration` is the duration that the compiler schedules. It is (O + 4 × ML + P + 3) / 6, which is ML when the activity has no spread. When you edit `Duration` in the application, it sets ML.

  A plan file keeps `Duration` as well as the estimates. When the file has `MostLikelyDuration`, the load calculates `Duration` again and ignores the value in the file. Thus, a program that changes only `Duration` changes nothing ([The project file](PROJECT-FILE.md) says so).

  In the next version of the file format, save only the estimates. Read `Duration` from the earlier versions. Use it as the most likely duration when `MostLikelyDuration` has no value, as the load does now. Do this only when the format changes for another reason, because each new version needs a mapper step. Until then, [The project file](PROJECT-FILE.md) tells a program to set `MostLikelyDuration`, or to leave it out and set `Duration`.

- [ ] **Suggestion: find the decision points of the time–cost–risk curve** *(2026-11-11, a suggestion from the design of the zpp skill)*

  Righting Software gets decision points from a fitted direct cost and a fitted risk. They are the maximum risk, the two risk crossover points and the inflection point of the risk. The inflection point is the target for a decompression. The method also finds the minimum direct cost and the minimum total cost. It also asks what total cost a duration needs, to find the death zone.

  From its own fits, `zpp` can calculate these points and give them as data. The data of the scenario chart (`scenarioChart` in the API) already gives the coefficients of each fit with all their digits. Keep at least six significant digits in each coefficient, because a cubic fit is sensitive to rounding.

  On 2026-11-11, the maintainer moved this suggestion to a release after 0.11.0. The method uses fits of its own against the duration. The direct cost is a quadratic, the indirect cost is a line, the total cost is their sum, and the risk is a cubic. Two questions are still open. Which metric of risk does the cubic fit (for example, an option with the activity risk as the default)? Where do the points go (the answer, the console, a CSV file or the chart of the application)?

- [ ] **Suggestion: compare the tracked scenarios with a baseline** *(2026-11-11, a suggestion from the design of the zpp skill)*

  A decision meeting compares options with the normal plan. It asks how much shorter, how much more expensive and how much riskier each option is. The scenario chart plots the values, but not the differences. An output can list the tracked scenarios with their metrics and their differences from a baseline scenario that the user marks. The compression of an option is then its difference in duration as a percentage.

  On 2026-11-11, the maintainer moved this suggestion to a release after 0.11.0. A question that is still open: how a client marks the baseline. An option of the request, such as `--baseline <scenario>`, does not change the file format. A mark in the plan needs a new version of the format.

- [ ] **Suggestion: a productivity factor for each resource** *(2026-11-11, a suggestion from the design of the zpp skill)*

  Righting Software compresses a plan with better resources. A senior developer finishes an activity sooner, but costs more. zpp cannot show this today. A factor on the resource, for example 0.8, can scale the duration of each activity that the resource does. With the operator `OR`, the duration then depends on the resource that the scheduler selects, and the priority list must allow for this. The simulation and the Rust port must give the same result. Until then, a client gives the activities of the senior shorter estimates and targets them at that resource.

- [ ] **Remove System.Reactive when DynamicData and Dock have no dependency on Rx** *(2026-08-02, last checked 2026-10-29)*

  ReactiveUI 24 no longer uses System.Reactive. But removal has no value while other packages still depend on it. Thus, the assembly ships in all cases.

  **Dock.** Dock now ships two lanes. The Dock documentation recommends the suffixed System.Reactive lane (`Dock.Model.ReactiveUI.Reactive`, with `ReactiveUI.Reactive`). It calls the unsuffixed family the optional ReactiveUI.Primitives lane (`docfx/articles/dock-reactiveui.md` in the Dock repository).

  The unsuffixed lane keeps `System.Reactive`. Version 12.1.0.13 of `Dock.Model.ReactiveUI` declares `System.Reactive` 7.0.0, and so does the suffixed twin. On Dock master, the props file `build/ReactiveUI.Primitives.props` of the unsuffixed project references `ReactiveUI` and `System.Reactive` together. Thus, the Dock gate can stay closed for good. The earlier expectation, that the next unsuffixed release drops the reference, does not hold.

  **DynamicData.** `DynamicData` is the remaining hard blocker in released packages. Version 9.5.0 (2026-10-13) declares `System.Reactive` 6.1.0, and no suffixed twin exists. PR [reactivemarbles/DynamicData#1116](https://github.com/reactivemarbles/DynamicData/pull/1116) migrates DynamicData to ReactiveUI.Primitives. It is open, and it has conflicts with main.

  On 2026-10-28, a maintainer wrote in this PR that Primitives support will be opt-in. The default `DynamicData` package stays on `System.Reactive`. Primitives support ships as a separate package that consumers choose to reference. Thus, the default package does not become free of Rx.

  **Watch.** Watch for the separate DynamicData package for Primitives, and for a Dock release whose unsuffixed package drops `System.Reactive`. If both packages keep the dependency, the assembly ships in all cases, and you cannot complete this entry. Then decide whether to delete the entry.

  **Caution for version bumps.** This repository is deliberately in the unsuffixed Primitives lane. Do not adopt a package with the suffix `.Reactive`. The two families have distinct type identities, and you must not mix them.

  **After both gates open.** Examine the migration of the own Rx use of this repository to `ReactiveUI.Primitives`. The migration must close these gaps:

  - `FromEventPattern` does not exist. Two uses are the collection-changed bridges in the effort tracking manager.
  - `Subject` and `BehaviorSubject` do not exist. Primitives has different "Signal" abstractions.
  - `Observable.Create` does not exist. `MuteWhile` uses it.

  **Starting points.** Comments above the `System.Reactive` reference in `Zametek.Graphs.Avalonia.csproj` and the Dock and DynamicData block in `Zametek.ViewModel.ProjectPlan.csproj` mark the places. The bridge `ObservableExtensions.ObserveOn(ISequencer)` is the seam where the unwinding starts.

- [ ] **Remove `SyncedMultiComboBox` when Ursa follows `SelectedItems`** *(2026-10-31)*

  In Ursa 2.2.0, an item of `MultiComboBox` adds its data to `SelectedItems` when it attaches to the logical tree, if its own `IsSelected` is true. The control also stops listening to `SelectedItems` when it unloads, and it does not listen again. The two chart filters use [`SyncedMultiComboBox`](../src/Zametek.View.ProjectPlan/Controls/SyncedMultiComboBox.cs), and its item corrects `IsSelected` first.

  The activity grid, the resource grid and the two edit dialogs still use the plain control. Their items belong to one row or one dialog, and thus a selection cannot go to another scenario. But a selection that changes without its dropdown can possibly come back in the same way. No test shows this yet.

  **Watch.** Check each new version of Ursa. If the control is correct, remove the subclass. [`ChartFilterTests`](../test/Zametek.View.ProjectPlan.Tests/Controls/ChartFilterTests.cs) then show if the plain control is enough.

- [ ] **Spin out Zametek.Graphs.Avalonia into its own repository** *(2026-08-02)*

  The library is already framework-decoupled, and it has its own README. The checklist is in [src/Zametek.Graphs.Avalonia/TODO.md](../src/Zametek.Graphs.Avalonia/TODO.md), so that it stays with the folder.

- [ ] **Formal drag-and-drop from charts** *(2026-08-02, consolidated from a TODO in the source code)*

  `ScottPlotUserControl.CheckPointerDrag` detects the threshold between a click and a drag. But it only tracks the state. The inline comment shows where `DragDrop.DoDragDropAsync` starts a real drag-and-drop operation (for example to drag a chart image into another application).

- [ ] **Reduce UI-thread reads of locked view-model getters** *(2026-08-04)*

  A dotnet-trace profile of a burst of edits showed several seconds of Monitor contention. Bindings read locked getters again while the background compile cascade held the locks. Most of the contention disappeared with the fix for `IsBusy`. If input hitches appear again under heavy background activity, prefer lock-free snapshots (volatile fields or immutable models) for hot bound properties.

- [ ] **Consider a report to Dock about the float and re-dock behavior** *(2026-08-02, optional)*

  Under Dock 12.1, a drop can return a floating tool to the layout. Then Dock materializes the destination while the visual tree of the closed float window is still assembled. Version 12.0.0.2 did not show this. The crash was finally the own pattern of the application: a shared control as `Content`. The fix is by construction in `ScottPlotUserControl`.

  A minimal reproduction is possibly worth a report to Dock. Any dockable that binds a shared control instance as `Content` shows the problem. Then the Dock maintainers can examine the order of the materialization across windows.

- [ ] **Surgery for rule 6: no notifications and no calls to foreign components under a lock** *(2026-08-17)*

  Two deadlocks, which dumps prove, come from cycles in the lock order. Both dumps are from 2026-08-17, during scenario loads. The cycles involve `CoreViewModel.m_Lock`, `ProjectScenarioDisplaySettingsViewModel.m_Lock`, `GanttActivitySelectorViewModel.m_Lock` and the internal `ExpressionChainSink` gates of ReactiveUI.

  The point fixes are already in place:

  - The sweep for lock-free getters (ARCHITECTURE §7 rule 9).
  - The move of the core callbacks of the display settings (`SetIsProjectScenarioUpdated` and `RequestCompilation`) out of all the lock blocks of the setters.
  - Ref-counted guards for revision in the Gantt and earned-value selectors. A shared bool let the `finally` of one reviser clear the guard of the other reviser during the revision, across threads.

  The REMAINING sites that call out under a lock are not live bugs, because every remaining edge is one-way with the point fixes in place. A fix for them is hardening. These sites form the deferred surgery. The list that follows has the items in descending order of value:

  1. `CoreViewModel.ProcessProjectScenario` and `ResetProjectScenario` hold `m_Lock` across the whole fan-out of a load. The fan-out raises the settings setters, raises `TrackersRevision`, and calls `DisplaySettingsViewModel.SetValues`. The `Scheduler.CurrentThread` subscriptions of `TrackersRevision` run the selector revisions synchronously ON the raising thread. Thus, they take selector locks under the core lock. This is the Core→Selector edge in the first dump. Since 2026-11-02, the selector of the Gantt chart only reads the plan there, and it takes its lock on the UI thread (§7 rule 17). The days of the resources still take their locks there. The call to `SetValues` is the Core→DS edge in the second dump.

     The surgery splits each method into two phases. The state phase runs under the lock. The fan-out phase runs after the release. The state phase collects the pending raises and signals, and the fan-out phase emits them outside the lock. §7 rule 7 makes this safe. Handlers read the live state again, because payloads are stale snapshots. The window of `BeginBulkUpdate` and `EndBulkUpdate` wraps both phases, and thus the semantics of the suppression survive.
  2. `ProjectScenarioDisplaySettingsViewModel.SetValues` assigns about 30 raising setters under the DS lock. Thus, every synchronous observer (the sink gates of ReactiveUI) runs under that lock. The surgery sets the fields and collects the names of the changed properties under the lock. Then it raises all of them after the release. This also removes the re-entrant locking of the setters.
  3. The five selector view models change `ObservableCollection`s inside their locked `Set*` methods. Thus, the `CollectionChanged` handlers run under the selector lock. These handlers raise notifications. In the Gantt and earned-value selectors, they also write through to the DS and the core. The surgery has two options. One option detaches the handler around the locked changes and does one manual refresh and raise after the release. The other option computes the change set under the lock and applies it outside.
  4. The plot-model setters of the chart managers (the earned-value, Gantt and resource charts) and `ResourceTrackerSetViewModel.RefreshIndex` raise inside their locks. These are trivial splits: set under the lock, and raise after the release.
  5. Document the global DAG of lock acquisition in ARCHITECTURE §7: Manager→Core→DisplaySettings and Core→Selector, with the node and tracker view models as leaves. Verify that the core never calls back into the scenario manager under its lock. Then future calls across view models have an order to follow.

  **Related problem.** The investigation found one more problem in the same area. The DS setters mark the scenario as modified, also when `RaiseAndSetIfChanged` writes nothing.

  See also the entry of 2026-08-04 above. The sweep for getters already delivered its suggestion of lock-free snapshots for hot bound properties, for the getters of the selectors and the trackers.

  **Direction to explore: delete locks and do not order them** *(2026-08-17)*

  The reactive cascade is not the problem, and it does not need a rewrite. In all three dumps, the dependency graph behaved correctly. The failures were in the locking around it. But the current design is safe only by discipline and not by construction. Three properties combine badly:

  - Each view model has one lock, and calls across view models are free in both directions.
  - Synchronous notification (`PropertyChanged` and `CollectionChanged`) re-enters arbitrary foreign components while it holds locks. The application does not own these locks and cannot inspect them. Examples are the `ExpressionChainSink` gates of ReactiveUI, DynamicData and Dock.
  - The `Scheduler.CurrentThread` subscriptions run on the thread that raised. Thus, no static analysis can tell which thread runs a given piece of work.

  Thus, the lock graph is also not fully knowable. A deadlock stays one refactor away, however carefully the application follows rules 6 and 9.

  The target end state has no locks in the view models. A view model is affine to the UI thread. Background work (parse, compile, metrics) operates on immutable snapshots, as the compiler already does when it clones the graph. It returns one result, and the UI thread applies it. With no locks, there is no lock order to get wrong. Rule 9 then becomes unnecessary and not only enforced.

  A pragmatic option gives 80% of the value, if full affinity is too invasive on the load path. It has exactly ONE lock at the boundary between the core and the model, and no other lock. With rules 6 and 9, this removes by construction every cycle among the own locks of the application.

  The path is incremental, and the team did most of it already. The sweep for getters removed the first class of deadlock for good. The moves in the setters removed the live case of the second class. When rule 6 holds everywhere, most remaining locks in view models will guard only brief writes to fields that happen on one thread. Then you can DELETE these locks and do not need to order them. This is better than a documented lock DAG that someone must maintain for ever, and it makes item 5 above unnecessary.

  Add enforcement by machine, and do not rely on vigilance. `System.Threading.Lock` exposes `IsHeldByCurrentThread`. Thus, assertions only for Debug at the boundaries of raises and callbacks change a latent deadlock into a loud test failure. Without them, you learn about the deadlock from a dump weeks later. This work is now a separate entry below. It has value on its own, and it does not depend on any of the surgery above.

  **The third dump.** The third dump (`zametek-deadlock-2.dmp`, 2026-08-17) is not a deadlock. No thread waited for any lock, and the UI thread was idle. A worker of a scenario load spun inside `PriorityListResourceScheduler.CalculateResourceSchedules`, and nothing was ever scheduled onto any of the 14 resource builders. The investigation of this dump is complete.

  The ClrMD analysis of 2026-08-18 found the root cause of the spin: a torn `HashSet` clone poisoned the `Contains` gate of the scheduler. ARCHITECTURE §7 rule 10 tells the full story, and it shows the race on the ProjectPlan side that tore the clone.

  The scheduler now detects the dead state upstream. Zametek.Maths.Graphs 3.2.0 reports compilation error C0020 and does not spin, and `Compile` takes a cancellation token. The compile watchdog of the application (`CompilationTimeoutMilliseconds`, 2026-08-19) limits all that the scheduler cannot prove dead. The last structural item of this paragraph landed on 2026-10-25: the compile and the cascade now run outside the lock, against a snapshot (§7 rule 14).

  **Size, and a recommendation not to do the surgery as written** *(2026-08-21)*

  The measurement is against the source code as it is now. The surgery spans these parts:

  - `CoreViewModel`: 2,453 lines and 66 `lock (m_Lock)` sites. The fan-out of the load is about ninety lines under one lock. These lines contain two `SetValues` calls, four assignments of settings that each raise and fan out, and the call of `ReviseTrackers()`. The `Scheduler.CurrentThread` subscriptions of this call run selector revisions synchronously on the raising thread.
  - `ProjectScenarioDisplaySettingsViewModel`: 735 lines and 33 lock sites. Its `SetValues` makes sixty property assignments, and each one uses a locked raising setter.
  - The five selector view models: 1,455 lines in total.
  - A handful of trivial splits in the chart managers.
  - The documentation item.

  This is the largest open entry in this file. The risk is higher than the number of lines shows. The change affects WHEN notifications fire in a cascade that already produced three dumps. Verification needs the diff of the CLI worktree and checks of the GUI. The compile work taught that naive concurrency tests have no teeth.

  Two reasons argue against the surgery in this form. First, there is no live bug. The point fixes are in place, and every remaining edge is one-way. Thus, the surgery improves safety and does not fix a bug.

  Second, this entry argues against itself. The direction to explore above makes items 1 to 4 unnecessary and not only done. It also makes item 5 unnecessary: with no locks, there is no DAG of acquisition to document. If the surgery for the lock order comes first, the cost is full for something that the better end state discards.

  Better value was nearby. Phase 2b (run the compile and the cascade outside `m_Lock`) landed on 2026-10-25, and it was larger than this entry expected. The lock did hidden jobs, and counters now do them (§7 rule 14). The pattern that it proves is the one of the direction above: copy under the lock, build without it, and publish with checks.

  **The browser head changes the economics** *(2026-08-21)*

  Single-threaded WebAssembly runs everything on the UI thread in all cases. Thus, "UI-thread affine, no locks, background work on immutable snapshots" is not a costly migration there. The platform already imposes it.

  If the web app becomes worth shipping, this direction is no longer speculative. It becomes the natural shape of the application. Then you must delete this entry. Do not do the work that it lists.

- [ ] **Speed up the calculation of the tracking series** *(2026-10-25)*

  The cascade of a large plan takes about 650 ms on the sample plan with 985 activities (`docs/assets/erp-migration.zpp`). The compile takes 100 to 170 ms. `CalculateTrackingSeriesSet` in `ResourceSchedulingService` takes 310 to 370 ms, and thus it is about half of the cascade. The arrow graph takes 50 to 90 ms, the vertex graph takes 160 to 190 ms, and all the metrics together take 30 to 70 ms.

  The cascade no longer holds `m_Lock` (ARCHITECTURE §7 rule 14), and thus the time does not block the UI. But it delays the charts after each edit, and a bulk update waits for it. Profile the calculation and look for work that repeats for each activity. A faster tracking series shortens the wait for every chart.

- [ ] **Bring the browser head up to the desktop application** *(2026-08-21)*

  `Zametek.ProjectPlan.Browser` now presents the whole application. The dock layout, the data grids, the interactive graphs and the ScottPlot charts all render under WebAssembly. The behavior behind them remains. Seven items remain:

  1. ~~**Split `MainView`**~~ DONE on 2026-08-21. It is now a `UserControl` shell that both heads share. `Zametek.ProjectPlan.Desktop.MainWindow` is the thin `Window` wrapper. It carries the title, the icon and the handlers for the confirmation of closing. `File | Exit` became the event `MainView.ExitRequested`, and the host decides how to honor it. `MainView.CanExit` hides the item where there is no host window.
  2. **Make the file dialogs hand back streams.** The file layer below the dialogs already works in streams *(2026-09-26, phase 1 of the plan for the headless service)*. All of these types read or write a `Stream` that the caller owns, in an explicit format:

     - `IProjectFileOpen` and `IProjectFileSave`.
     - `IProjectScenarioFileImport` and `IProjectScenarioFileExport`.
     - `IXlsxScenarioFileImporter` and `IXlsxScenarioFileExporter`.
     - `IMicrosoftProjectFileImporter`.
     - The `Write*ChartImageAsync` methods of the chart managers.
     - `IInteractiveGraph.WriteImageAsync` of the graph library.

     The desktop and `zpp` open the files at the edge (`FileStreamHelper`, with `FileFormatHelper` reading the format from a file name).

     The dialogs remain. `IDialogService.ShowOpenFileDialogAsync` and `ShowSaveFileDialogAsync`, and `IGraphHost.PickSaveFileAsync` of the graph library, still return a local path. A browser has no local path. It has only opaque `IStorageFile` handles that the application must stream.

     Thus, the dialogs must return a handle. The handle gives a name from which to read the format, and a way to open it for reading or writing. `MainViewModel` and the Save-As paths of the charts and graphs then open the handle and not a path. Until then, the two file methods of `BrowserDialogService` throw, on purpose. A return of null looks the same as a cancellation by the user.
  3. **Persist the settings.** `BrowserSettingService` holds everything for the life of the page. Back it with localStorage or IndexedDB, so that the dock layout, the layouts of the grids, the recents and the preferences survive a reload. `ISettingService` itself has the shape of a path (`SettingsFilename`, `DockLayoutFilename`, `ProjectDirectory`, `RecentProjectFilePaths`). Thus, this is partly a question of the contract and not only of the implementation. The list of recents cannot hold paths in a browser. A handle that the user gave access to is not a name that the application can open again.
  4. **Disable floating dockables.** `DockFactory` sets `CanFloat = true` on 17 dockables. Floating creates host windows, and a browser has none. Nobody tested this so far. The obvious test is to drag a tool out of the layout.
  5. **Confirm the diagnostic channel.** The head configures Serilog to the console, with errors routed to stderr. Nobody verified that this output arrives. During bring-up, neither Serilog output nor a plain `Console.WriteLine` appeared in the browser console. But this observation is not trustworthy. At that time, the tool that read the console showed only `[error]` entries. A direct `console.log` from the page was also invisible until the UI began to compose, and then it appeared normally.

     Treat the channel as UNVERIFIED. Do not treat it as broken. The first step is to open the developer console against a running browser head and see if the startup line is there. The result matters either way. A web app with logging that goes nowhere reports a failure as a blank canvas and nothing else.
  6. **Prune what a browser cannot use.** Remove `Process.Start` in `UriHelper` (use the own navigation of the browser). Remove the file-writing thread of `PerfTelemetry`. Hide the MS Project import from the UI, so that it does not reach `UnavailableMicrosoftProjectFileImporter`.

- [ ] **Decide how to host and deploy the web app** *(2026-08-21)*

  `dotnet publish` makes a static `AppBundle`, but nothing publishes it anywhere. [Build from source](BUILDING.md#the-web-app) already documents two requirements. The first is a secure context (HTTPS or localhost) for the browser APIs that the file pickers need. The second is a `Service-Worker-Allowed: /` header on `_framework/sw.js`, or that script served from the root of the site. Avalonia registers its service worker at the root scope. Without the header, Firefox and Safari lose the fallback for save-file.

  Decide these questions together:

  - Does the release workflow publish the bundle as an artifact?
  - Does GitHub Pages serve the bundle? GitHub Pages cannot send custom headers, and thus the placement of `sw.js` matters. Or does a host that can send headers serve it?
  - Is it useful to configure Brotli precompression? The payload includes the whole runtime.

- [ ] **Reconsider single-threaded WebAssembly when the browser head does real work** *(2026-08-21)*

  The browser head runs single-threaded. This lets any static host serve it. Thus, every `Task.Run` in the layer of the view models runs on the UI thread. There are many, and `RunCompile` is one. The compile of a large plan will freeze the tab.

  The compile watchdog (`CompilationTimeoutMilliseconds`) probably cannot fire at all. Its timer callback has no thread to run on while a synchronous compile holds the only thread. Nobody measured either effect. Measure with a real plan before you decide.

  The alternative is `WasmEnableThreads`. It keeps `Task.Run` truly off the UI thread, and it restores the watchdog. But the host must send COOP/COEP headers, which rules out plain GitHub Pages. The Avalonia configuration is also less well tested by other users.

  The direction "delete locks rather than order them" in the entry for rule 6 above becomes more attractive if the browser stays single-threaded. A layer of view models with affinity to the UI thread has nothing to contend over.

- [ ] **Stop the MSIX build from depending on whether NuGet restores** *(2026-08-21)*

  The packaging build needs the `project.assets.json` file of every project to carry a target for each RID in the bundle. `build-msix.ps1` arranges this. It sets `RuntimeIdentifiers` in the environment, and thus the restore fills all three targets in one pass. This works only if the restore runs.

  Sometimes the restore does not run. NuGet then reports "All projects are up-to-date for restore" and keeps the assets file that is already there. An ordinary `dotnet build` can leave such a file, and this file has no RIDs at all. Then the inner publish for each RID fails with `NETSDK1047`, and the message names a project that looks innocent.

  This occurred once, during the `PlatformTarget` investigation, on `Zametek.Shell.ProjectPlan`. It was immediately after a plain build of `Zametek.ProjectPlan.slnx`. The run before and the run after both succeeded. Thus, the fault looks like flakiness and not like a missing input.

  Three attempts failed. The comments of the script record all three, and thus the next person does not repeat them:

  1. Deleting `obj` in `-Clean` forces a restore and closes the fault in theory. The attempt on 2026-08-21 failed. MSBuild evaluates every project before it runs any target, and that evaluation imports `obj\<project>.nuget.g.props`. With an empty `obj`, the DesktopBridge targets fail with `MSB4024`. They look for a file that the same command is about to write.
  2. A separate invocation for the restore fixes that problem. But then the targets for each RID are not filled. The `RuntimeIdentifiers` environment variable does not reach a standalone `/t:Restore` in the way it reaches the `/restore` switch.
  3. Passing `RuntimeIdentifiers` as a global property is worse. The inner publishes for each RID cannot then override it. The SDK rejects the whole list as a single identifier (`NETSDK1083`).

  Two options remain. The first is `-p:RestoreForce=true` on the existing single invocation. It costs a few seconds of re-evaluation on a build that takes minutes, and it makes the RID targets unconditional. The second option is cheaper and less certain. It reads the assets file of each referenced project at the start. It fails early with a message that names the real problem, and `NETSDK1047` does not misdirect.

- [ ] **Audit the packaging path for more commands and paths that fail in silence** *(2026-08-21)*

  `-Clean` in `build-msix.ps1` never deleted anything, from the day that someone wrote it. It used `Get-ChildItem -Path $dir -Include bin,obj -Directory`. `-Include` filters the leaf of `-Path` and not the children, unless the path itself ends in a wildcard. Thus, the pipeline matched nothing, and the script still printed "Cleaned obj/bin".

  Nothing failed, and this is the reason why nobody noticed it for a year. It also silently invalidated every "clean build" that anyone believed they ran. One example is the first comparison in the `PlatformTarget` investigation. Stale IKVM native staging made 64 files look as if a change to a managed compiler switch rearranged the JDK.

  A second case appeared almost immediately, in 349baed0. The WiX components took their files from `$(var.<project>.TargetDir)publish\`. The publish no longer wrote to this directory, and nothing reported a missing input.

  The packaging projects are outside the filter `Zametek.ProjectPlan.slnf`. Thus, no gate builds or exercises them. The same class of fault can be in the rest of that path. Its sign is a command or a path that seems correct, because its failure mode is silence.

  Read `build-msix.ps1`, the two `.wapproj` files and the WiX project with this specific suspicion. Then ask if a gate can cover any of them. Since 2026-11-06, the WiX project stops a build for a platform that is not x64. It also stops when a file of its publish is not there or is older than a file under `src` (the target `CheckTheInstallerInputs`).

- [ ] **Add a Debug assertion for the locks that the application holds when it announces a change** *(2026-08-21, extracted from the rule-6 entry as the piece that is worth doing on its own)*

  The two deadlocks of 2026-08-17 were both the own half of an ABBA. One thread raised `PropertyChanged` into an expression-chain sink of ReactiveUI while it held the `m_Lock` of a view model. Another thread was inside the gate of that sink and read again a getter that wanted the same lock.

  Rules 6 and 9 forbid exactly this, and the point fixes removed the live cases. But nothing enforces the rules: no test, no analyzer and no gate. Thus, the real risk is not in the edges that exist now, which are one-way. The risk is a future edit that quietly creates a cycle again.

  A deadlock freezes the application with no exception, no log line and no crash. The only diagnosis is a process dump that someone reads with ClrMD. This has already happened three times.

  `System.Threading.Lock` exposes `IsHeldByCurrentThread`. Thus, an assertion with `[Conditional("DEBUG")]` at the boundary of the announcement changes this whole failure mode to a test failure that names the offending line. The assertion sees only the own locks of the application. It never sees the locks of ReactiveUI, DynamicData or Dock. This is the intent. The application controls its own half, and the rules govern that half.

  The seam is `ViewModelBase`. It is an empty class that derives from `ReactiveObject`, and it sits between ReactiveUI and every view model. Confirm if ReactiveUI offers a suitable override there. If it does not, use a small helper that the raise sites call.

  Size the scope first. The layer of the view models has 31 `Lock` fields. Thus, the assertion needs a cheap way to ask "does this thread hold any of them?". There are two options. One option is a registry of the locks in use. The other option is an assertion in each view model for only its own lock and the shared leaf lock `m_ActivityDataLock`.

  The payoff is immediate and not theoretical. The pre-commit hook builds and tests in Debug, and the test suites drive real scenario loads and compiles through the whole cascade. Thus, a violation shows in the gates and not in a dump weeks later.

  The assertion encodes rules that exist already and does not invent new rules. Rule 11 holds the leaf lock around the writes of an activity on purpose, and never around its announcements. This is exactly the shape that the assertion checks. The work has value whether or not the surgery for rule 6 above ever happens. If the team takes the direction "delete locks rather than order them" instead, these assertions prove that each lock is now removable.

- [ ] **Audit every deferred subscription for §7 rule 7: handlers must read the live state again and not act on the payload** *(2026-09-13, prompted by the wipe of the inter-activity phases, which the team fixed on the same day)*

  Rule 7 says that a value delivered through a subscription is a snapshot of the moment of the raise. It is not a snapshot of the moment of the arrival. Thus, a handler that acts on its payload can act on stale state.

  This is not a theory. A load of a project whose resources carry inter-activity phases silently emptied the phases. The cause was `ResourceSettingsManagerViewModel`. It observed `CoreViewModel.WorkStreamSettings` and pushed the delivered snapshot into each resource.

  During a load, the worker thread writes both settings four times: the reset clears each one, and then the scenario supplies each one. This happens before the UI thread drains either `ObserveOn` queue. The queue for the resource settings is first in the schedule, and thus it drains in full. This correctly rebuilds every resource from the live state.

  Then the queue for the work streams delivers its first snapshot, which is now stale and empty. It reconciled the phases of each resource against no work streams and discarded them. A run of the cascade diagnostics showed all four writes on the worker within 7 ms. The first delivery arrived 470 ms later. The delivery that did the damage had `isStalePayload=True payloadWorkStreams=0 liveWorkStreams=1`.

  The fix for this case is structural. It deletes the subscription. `WorkStreamSettingsManagerViewModel.UpdateWorkStreamSettingsToCore` now pushes into the resources synchronously. This mirrors the push that `CoreViewModel` already makes into the activities. The audit covers the other cases. Four settings managers (graph, holiday, resource and work stream) share the identical shape `Subscribe(rs => { if (m_Current != rs) ProcessSettings(rs); })`, and all four consume the payload.

  These four are much less dangerous than the case that did the damage. `ProcessSettings` rebuilds its whole collection from the payload. Thus, a stale delivery that a real delivery follows still converges on the right answer. The wipe of the phases was different, because the discarded state lived only in the resource view models. The later delivery had no way to restore it.

  But the four are not free. Each stale delivery disposes and rebuilds every managed view model in that collection. Any state of a view model that the payload cannot rebuild goes with them.

  The team did the sweep on 2026-09-13 and closed most of the findings. The text that follows gives the remainder and the findings that are worth keeping. The layer of the view models has 55 deferred subscriptions. Of these, 24 bound their payload.

  **Subscriptions that now read the live state again:**

  - `CoreViewModel.m_CompileOnSettingsUpdateSub`. It took `m_Lock` and read `IsBusy` live, but it trusted the delivered value for the other half of the same condition. Thus, two edits in quick succession compiled twice, and the redundant compile marked the scenario as modified.
  - In all four settings managers, the `ProcessSettings` subscription and the `AreSettingsUpdated` subscription. The second one is included because both `ProcessSettings` and `UpdateXToCore` clear that flag. Thus, a queued `true` often outlived its own clearing. It was then possible for the flag to push view models straight back into the core after a load replaced them.

  **A correction.** An earlier draft of this entry claimed that the `m_Current != rs` guards are dead weight. This is wrong. `UpdateXToCore` assigns one instance to both `m_Current` and the core property. When that raise comes back, the guard compares the same reference and correctly stops the manager.

  The guard prevents a rebuild of the grid of the manager in response to an edit that the manager made itself. Such a rebuild discards the selection and the edits in progress. The guard is dead only in the own comparison of `UpdateXToCore`. There the new instance can never equal the stored instance, because the record equality becomes a reference equality on the `List<T>` members.

  The seeding of the graph layout in `ArrowGraphManagerViewModel` and `VertexGraphManagerViewModel` is also converted. It now reads the current layout of the core. It recognizes its own echo when it compares the layout with the instance that it pushed. This replaces an `m_SuppressNextSeed` bool.

  The next delivery to arrive consumed that bool, and not the echo that the application set it for. A drag landed while the layout change of a load was still in the queue. Then the delivery of the load ate the flag, and the seeding skipped the arrangement that the user had just opened.

  **Two problems outside this rule.** The team fixed both at the same time. Both were in the title pipeline of `MainViewModel`:

  - The pipeline assigned `ProjectTitle` on the taskpool, although it binds to `Window.Title`. Thus, a background thread raised a change notification for a UI binding. This survived on Win32 because `SetWindowText` posts and does not block. The browser head has no taskpool thread at all.
  - The `WhenAnyValue` selector cleared `IsReadyToReviseTitle` from inside itself. Thus, a projection changed one of its two own inputs and re-entered its own chain. It ended only because ReactiveUI collapses the second raise with the same value. The readiness enums exist to work around that behavior.

  **The date subscriptions of the activities.** The team also fixed the date subscriptions of `ManagedActivityViewModel`, under rule 10 and not under this rule. An earlier draft of this entry made two errors about them. It described one subscription where there are two. It also called `ProjectStart` an anchor for the display of dates and not an input of the compiler.

  This understates the effect. The setter runs `SetMinimumEarliestStartTimes` and `SetMaximumLatestFinishTimes`. They write `DependentActivity.MinimumEarliestStartTime` and `MaximumLatestFinishTime`, and the compiler reads both. `m_ProjectStartSub` carried the project start. `m_DateTimeCalculatorCalculatorModeSub` carried the holiday settings and the mode for non-working days. Together they are the calendar along which the application counts those two integers.

  Both subscriptions observed on `Scheduler.CurrentThread`. This scheduler runs inline only while nothing else holds the trampoline of the thread. A write from inside another scheduled delivery is the ordinary case when the cascade runs. It queued the subscriptions behind that delivery. It was then possible for the compile that the same setter armed to start with counts that the application still measured along the previous calendar.

  Three tests make each change from inside `Scheduler.CurrentThread.Schedule` and read the activity before that action returns. Against the source code without the fix, all three tests gave 3 where 2 was correct.

  Both subscriptions are now deleted. All three changes now push in synchronously, as `SetResourceSettings` and `SetWorkStreamSettings` do:

  - `CoreViewModel.ProjectStart` calls `SetProjectStart` on every activity.
  - `CoreViewModel.HolidaySettings` calls `UpdateEarliestStartAndLatestFinishDateTimes`.
  - `ProjectScenarioDisplaySettingsViewModel.NonWorkingDayMode` does the same through a new callback of the core. It is the only writer of the mode of the calculator. The callback takes no lock, on purpose. That setter can still be inside the lock of the display settings, because `SetValues` holds it across the whole batch and `Lock` is re-entrant. A call to `m_Lock` there creates again the edge of lock order from the display settings to the core. This edge deadlocked against `ProcessProjectScenario`.

  The third subscription is on the display mode of the calculator. It stays in place on purpose. It only raises display properties again and changes nothing. Thus, neither rule applies to it.

  **The audit found no problem in everything else:**

  - The subscription for uncompiled activities is the reference implementation, and a comment says so.
  - The subscriptions that followed the readiness flag of the trackers now follow revision counters (§9). The two selectors of activities use the value of `TrackersRevision` only as a trigger, and they read the plan during the raise. The selector of the Gantt chart posts work to the UI thread. On purpose, that work applies the plan of the last revision and not a live read. A load can hold `m_Lock` with an empty plan when the UI thread runs the work (§7 rule 17). The tracker sets and the timesheet follow `GraphCompilation` and `TrackerIndex`, and they read the live state.
  - The title subscription of `MainViewModel` binds the product of its own `Select` and not a snapshot of the state.
  - The internal subscription of `MuteWhile` is the gate at emission time itself.
  - The subscriptions for edge routing of the graphs have no `ObserveOn`, and thus rule 7 never applies to them.

  Pair this audit with the Debug lock assertions above. Both change an invisible rule of order to something that a machine can check. The analyzer is a separate entry below. It is a piece of work in its own right and not a tail on this entry.

  With the date subscriptions of the activities complete, no action remains under this heading. The text remains as the written record of what the sweep found and why each decision went the way it did. It stays here until the analyzer entry can carry it.

- [ ] **Write a Roslyn analyzer for §7 rule 7: STANDALONE WORK, NOT A TAIL ON THE AUDIT** *(2026-09-13)*

  This entry is separate from the sweep above on purpose. Do it as a focused piece of work, and not at the end of an unrelated change. It is a new project in this repository, and a new kind of artifact for it. An analyzer has its own packaging, its own test harness and its own failure modes around false positives. It needs an uninterrupted effort. If you start it as a follow-on to a bug fix, the result is a half-built analyzer that someone disabled.

  The analyzer encodes the rule that the audit found broken again and again. A value delivered through a subscription is a snapshot from the moment of the raise. Thus, a handler that binds its payload and uses it as data can act on stale state. The analyzer must flag a `Subscribe` that reads its lambda parameter on a pipeline that contains an `ObserveOn`. An attribute or a comment marker must suppress the flag. It applies where the delivered value is a product and not a snapshot of the state.

  Two cases must not get a flag. The first case is the title subscription of `MainViewModel`, whose payload is the output of its own `Select`. The second case is the subscriptions for readiness flags. They gate on an enum that triggers on an edge, and its setter raises without a condition.

  A live read there swallows a revision that somebody asked for. Thus, the analyzer must tell a level from an edge, or it pushes people in the wrong direction. This distinction is the hard part of the design. Settle it before you write any source code.

  The reason to do the work is this. The audit found nine violations across five files, in source code that people reviewed many times. The two tests that guard the wipe of the inter-activity phases (`WorkStreamSettingsDeliveryTests`) pin that one case and nothing else. Only an analyzer catches the next case. Pair it with the entry for the Debug lock assertions above. The motivation and the payoff are the same, and one analyzer project can host both checks.

- [ ] **Decide if graph exports with no user interface must show the saved arrangement** *(2026-09-25, raised during the spike of phase 0 for the headless service, and kept on the fixed layout for now)*

  `zpp` exports the arrow and vertex graphs through the fixed-layout path: `WriteFixedLayoutArrowGraphImageAsync` and `WriteFixedLayoutVertexGraphImageAsync`, then `InteractiveGraphViewModel.WriteFixedLayoutImageFormatAsync`, then `MsaglGraphLayoutEngine.RenderSvg` with the static presets `GraphConfigurations.Arrow` and `GraphConfigurations.Vertex`. This path lays out the graph afresh. It ignores the node positions that the project scenario saves. Thus, a graph that the user arranged in the desktop application exports differently from the Save-As of the desktop. The Save-As renders the interactive canvas (`InteractiveGraphRenderer` over `GraphNodes` and `GraphEdges`, or the real templates in raster mode).

  `zpp serve` (2026-10-02) has the same behavior, and so does `zpp --server`. Their parity tests pin their graphs to the graphs of `zpp`. Thus, a switch changes all three at once.

  A switch needs two changes. First, the interactive render must stop its run inside `Dispatcher.UIThread.Invoke` (`InteractiveGraphViewModel.cs:869`). This is the same hazard as the `Refresh` call that leaks a parked thread for each job, where there is no user interface. Second, a node without a saved position still needs a deterministic fallback, and the same MSAGL layout gives it.

  A third need is already met. It is to draw with the bundled font through the own font cache of `InteractiveGraphRenderer`, which does not use the typeface providers of Svg.Skia. It came with the bundled fonts on 2026-09-25. A run of the CLI already computes the interactive layout, because the `Refresh` subscription fires. Then the run discards the layout.

- [ ] **Revisit ReadyToRun for the command line tool `zpp`** *(2026-09-25, measured during the spike of phase 0 for the headless service, and left for now)*

  `release.yml` publishes `zpp` as a single file and self-contained, without ReadyToRun. The measurement is on win-x64 with `two-scenarios.zpp`. It is the median of 7 cold runs, after the one-time self-extraction of each variant. With ReadyToRun, the load, the compile and the metrics went from 842 ms to 458 ms. With every export, the time went from 1,841 ms to 874 ms.

  The cost is size. The single file grows from 234 MB to 462 MB. About 100 MB of the growth is R2R output for the IKVM and Java assemblies, which only the MS Project import uses. There is also a `zpp.r2r.dll` of 265 MB. The self-extraction at the first run also takes longer: 5.8 s and not 4.5 s.

  Since `zpp serve` (2026-10-02), the single file also carries ASP.NET Core. This brings its size to 264 MB without ReadyToRun. A script that runs many plans can now send them to one warm server (`zpp --server`). This weakens the case to pay the size of ReadyToRun for the start-up.

  But a run that goes to a warm server still has the own start-up of `zpp`. It takes about a quarter of a second on win-x64 (Release, not a single file). ReadyToRun can possibly shorten this time too. Nobody measured it.

  Try these actions before you decide. Use `PublishReadyToRunExclude` for the IKVM and MPXJ assemblies. Find what produces `zpp.r2r.dll` (it looks like a composite image) and if you can avoid it. Nobody measured the start-up of the desktop application.

- [ ] **Package `zpp serve`** *(2026-10-02, phase 6 of the plan for the headless service)*

  Make a Docker image, a systemd unit, a launchd plist and a Windows Service, with CI to match. The image is the canonical deployment. The data outputs and the graph SVGs are already byte-identical across platforms. But the platform measures or rasterizes the text of other outputs: the charts, in any format, and the raster graphs.

  These outputs are byte-identical only on the same operating system, the same CPU architecture and the same Skia build. The spike of phase 0 found a difference between Windows and Linux. The same bundled font has a measurement that differs by a fraction of a pixel. The anti-aliasing of the glyphs also differs.

  The packaging must get these six points right:

  1. **The shutdown timeout.** A stopped server gives the jobs in progress up to the job time limit to finish. The default limit is 120 s. Docker gives a container only 10 s. systemd gives a unit 90 s by default (`TimeoutStopSec`). Raise each timeout to at least the job limit, or the shutdown kills jobs part way.
  2. **The culture and the time zone.** The image must set them explicitly (`--culture`, `TZ`) and must not inherit them. The default is the culture and zone of the host. The decision of 2026-10-01 made this default, so that a local `zpp serve` agrees with `zpp`.
  3. **The integrations for hosting.** The integrations for systemd and Windows services do nothing in a process that neither of them started. Thus, one binary can carry both.
  4. **The probes.** `/health/live` is for liveness. `/health/ready` is for readiness, and it comes a few seconds after the start-up, when the warm-up is complete.
  5. **The garbage collector.** `zpp` runs the workstation GC. Measure the server GC (`DOTNET_gcServer=1` in the image or in the environment of the service) with a long test of 1,000 jobs before you choose.
  6. **The size of the image.** It carries MPXJ and the Java runtime image of IKVM, although the server refuses MS Project imports. `Zametek.ViewModel.ProjectPlan` references MPXJ.Net. If the importer moves to an assembly of its own, the image can leave them out.

  The packaging must also give the single file a folder. At its first start, the single file unpacks approximately 250 MB into `~/.net/zpp/<hash>`. A service without a home folder cannot write there, for example a systemd unit with `ProtectHome` or `DynamicUser`. Then `zpp` stops with the exit code 159. Set `DOTNET_BUNDLE_EXTRACT_BASE_DIR` in the image and in each unit (refer to [The single file](COMMAND-LINE.md#the-single-file)).

  CI must build and test on Windows, Linux and macOS. It needs exact golden files for the data outputs and the graph SVGs on all platforms. It needs exact golden files for the other outputs on the platform of the image. On other platforms, it needs a perceptual tolerance.

- [ ] **Decide which omissions of `zpp serve` to add** *(2026-10-02, deferred when the team planned phase 4)*

  The design left out each of these items on purpose, and nobody needed any of them yet:

  1. **A mode for stdin and stdout.** This mode takes jobs as JSON lines for scripts, with no port and no key. `zpp --server` over loopback or a Unix domain socket made it redundant (2026-10-02).
  2. **Paths of the own server.** With this option, the server reads plans from directories under a root with an allow-list, and writes outputs to them. Bytes in and bytes out behave the same, whether the caller is local or remote. In Docker, the paths of the host are not the paths of the container.
  3. **MS Project import.** The server refuses it with 415 today, and `zpp --server` refuses to send it and points to `--local`. The import runs MPXJ on the Java runtime of IKVM, and nobody measured its cost at first use. The parsing of untrusted `.mpp` files is a reason to give it the least privilege that a container gives.
  4. **A culture or a time zone for each request.** `DateTimeCalculator` reads its date formats one time for each process, into static fields, from the culture that is current at that time. Thus, it must first take its culture from the scope of the job. A time zone for each request requires everything that reads `TimeZoneInfo.Local` to read the zone of the job.
  5. **Caching of answers.** A job with `now` gets the same answer to the same request on the same server (the same version, culture and time zone). The job id and the ids that the job mints (of an import, or of the upgrade of an old file) are the exceptions. Thus, the server can cache the answer under a hash of the request, the version, the culture and the time zone.

- [ ] **Stop a job from compiling its plan two times** *(2026-10-02, noticed in phase 4, and worth more now that `zpp serve` takes the start-up out of the cost of a job)*

  The load of a plan already compiles it and builds its outputs. `CoreViewModel.ProcessProjectScenario` and `ProcessProjectScenarioImport` both end with `RunCompile()` and `RunBuildCascade()`, whatever `AutoCompile` says. Then `JobRunner.RunAsync` sets the base theme and runs `RunCompile()` and all seven Build\* steps again. The arrow and vertex graphs each compile the plan once more. Thus, every job does all the compiling and building of the load a second time. The sequence came over from `Program.RunAsync` of zpp without a change.

  Before you remove the second pass, find what it sees that the first pass did not. The application sets the base theme between them. Thus, the removal can need only a change to set the theme before the load.

  Also measure the cost. A warm job without exports takes about 25 ms on the server (`two-scenarios.zpp`, win-x64). Nobody measured how much of this time the second pass takes.

  Verify the change in two ways. Run `zpp --now` on every sample plan with every output, before and after, and compare the files byte for byte. Also run the parity tests.

- [ ] **Run the parallel loop of the simulation on threads of its own** *(2026-11-06, after the measurement of `zpp serve`)*

  The simulator of the library runs its iterations in `Parallel.For`, on the thread pool. The loop keeps each thread that it takes until its end, and other work of the process waits for a thread. `zpp serve` now keeps more threads in the pool (`JobServer.KeepThreadsForRequests`), and so its requests do not wait ([The plan](SIMULATION-PLAN.md#the-server-keeps-threads-for-its-requests) has the measurement). The desktop application does not keep more threads, and a simulation there takes a thread for each processor. Nobody measured if its compilations and its charts wait while a simulation runs.

  A `TaskScheduler` with threads of its own would keep the loop away from the pool. But `Parallel.For` uses `ParallelOptions.TaskScheduler`, or the scheduler of the thread pool when that is null. It does not use the scheduler of the task that calls it (tested on .NET 10, 2026-11-06). The library sets no scheduler. Thus, the change needs an option in the library, for example in `SimulationSettings`, and a release of the library.

- [ ] **Group the critical paths of a simulation in a way that helps for a plan that resources decide** *(2026-10-16)*

  A plan with resources has many critical paths in a simulation, because the resources do the work in a different order in each iteration. For the sample plan, 3,000 iterations give about 475 distinct paths. The six heaviest have 7 % to 8 % of the weight each, and the group "Other paths" has about 54 %. Thus, the chart shows the main chains, but about half of the weight is in one group.

  Until version 4.0.1 of the library, "Other paths" had 92 %, and the eight paths of the chart were all one chain of activities. A defect of the library caused this ([The plan](SIMULATION-PLAN.md#the-application-uses-version-401-of-the-library) tells how).

  Choices: group the paths by the activities that decide the finish (the last activity before the finish that is not shared by all the paths), group them by the first activity that differs, or show the criticality index of the activities as a bar chart next to the histogram. The criticality table already says which activities matter most. On the sample plan (2026-11-05), each of the first two choices gives two groups only. The first gives 39 Manual Polishing and 40 System Testing, with about half each. The second gives 7 Dev Training with 96 % and 10 CM and setup with 4 %.

- [ ] **Count the criticality of the activities with the bits of the library** *(2026-10-16)*

  `SimulationAnalyzer` calls `SimulationResult.IsCritical(iteration, activity)` once for each activity and each iteration. For 1,000,000 iterations of a plan of 2,000 activities, that is two thousand million calls. A method of `SimulationResult` that counts the set bits of one activity over the iterations would take a few seconds less. Measure before the change.

- [ ] **Run the schedule simulation in small batches in the web host** *(2026-10-16)*

  The web host has one thread. The panel runs the simulation with `Task.Run`, and the simulator uses `Parallel.For`. On the web host, both run on the one thread, and the UI freezes until the run ends. A run of 5,000 iterations of a plan like the sample plan takes about 5 seconds on one thread.

  Choices: give the simulator a way to run a number of chunks and return (the chunk is already the unit of the work, with its own seed), and let the panel call it from a timer so that the UI gets the thread between batches. Or hide the panel on the web host until this is done. The result must stay the same, because a chunk does not depend on the thread that runs it.

- [ ] **Show the problems of an import in a window of their own** *(2026-10-17)*

  The desktop shows the first ten problems of a workbook in an error, and it offers to save a CSV report of all of them (`ImportProblemsPresenter`). A window with a grid (sheet, cell, column, row, value, message), with the buttons "Save report" and "Copy", is easier to use when a workbook has hundreds of problems. `ScenarioImportException.Issues` already holds all that the window needs.

- [ ] **Write the report of the import problems from `zpp`** *(2026-10-17)*

  `zpp` writes one line for each problem to the standard error stream, and a server lists the first 100. An option like `--import-report <file>` can write the same CSV report as the desktop (`ImportIssueText.WriteReport`). The server can return it as an output of the job.

- [ ] **Report the problems of an MS Project import in the same way** *(2026-10-17)*

  The import of an Excel workbook collects its problems and cancels (`ScenarioImportException`, `ImportProblemsPresenter`). The import of an MS Project file (`MicrosoftProjectFileImporter`) does not. A file that MPXJ cannot read, or a value that the importer cannot use, fails with the message of the library. The open project stays as it is, but the user sees only this message. The importer can collect its problems as `ImportIssue` records and throw the same exception. Then the desktop, `zpp` and the server show them in the same way. `ImportIssue` has a sheet, a row and a column, and an MS Project file has none of these. Decide first what the place of a problem is: the kind of item (task, resource or calendar), its unique id and the name of the field.

- [ ] **Say where a bad date is in a workbook that counts from 1904** *(2026-10-17)*

  ClosedXML converts a number that has the format of a date while it loads a workbook that counts its dates from 1904. Old Excel for the Mac makes such workbooks. A number that no calendar has then makes the whole file unreadable. The message is "Not a legal OleAut date." In a normal workbook, the reader finds the cell and reports `notADate`.

  One fix is to find the cell in the file when the load fails with this message, and to report it. Another fix is to read the cells of such a workbook without the date conversion of the library.

- [ ] **Let a job make only some of the CSV files of a simulation** *(2026-10-18, from the review of the known gaps)*

  The output `simulation` of a job makes the chart of the simulation and all its CSV files: the statistics, the histogram, the paths, the activities and, for a run, the iterations. At the end of phase 4, a request could ask for each file with an output of its own (`simulationChart`, `simulationStatistics`, `simulationHistogram`, `simulationPaths`, `simulationActivities` and `simulationIterations`). Step 5 of phase 5 (`4e98d8fe`) replaced them with the one output `simulation`. The answer still lists each file as an output of its own, but a request cannot ask for a part. A client that needs only the statistics gets the other files too. `zpp` never had an option for only some of the files.

  The file of the iterations has one row for each iteration, and a run can have 1,000,000 iterations. Thus the files that a client does not need cost most for this file.

  Choices: add the single outputs again, next to `simulation`. Or let `simulation` carry a list of the parts to make (for example `"parts": ["chart", "statistics"]`). The command line needs an option in both cases (for example `--simulation-files statistics,histogram`). Decide first what a request means when it gives `simulation` and a single output together.

  **Where.** `SimulationCsvFile` is a flags enum, and so the engine can already make a set of files. `JobRunner` has the table of `SimulationCsvFile` and `JobOutput`. The change is also in `JobRequest`, in `CompileOptionsHelper` and `Program` of the command line, and in `docs/API.md`, `docs/openapi.yaml` and `docs/COMMAND-LINE.md`. The contract tests of the OpenAPI description change with the API. The workbook of the simulation (`simulationWorkbook`) is an output of its own and is independent of `simulation`, so that a request can already choose between the chart with its five files and one workbook. The same choice for the single files remains.

- [ ] **Make the schedule simulation faster** *(2026-10-18, from the review of the known gaps)*

  Phase 0 found where the time goes, and the maintainer decided that phase 2 builds none of the speed work. [The plan](SIMULATION-PLAN.md#performance-work-to-explore-later) lists four possibilities, with their rough effort and their effect on the accuracy:

  - A mode that keeps the resource order of the compiled plan. It takes 1 to 2 days and is 430 to 1,400 times faster. The results change for a plan that the resources decide.
  - A faster incremental update in the critical-path engine. It takes about a week and is up to 2 to 3 times faster. The results do not change.
  - A flat kernel for the priority list and the scheduler. It takes 2 to 4 weeks for each language. Nobody measured the gain.
  - An approximate priority list. The team does not recommend it, because the loss is not predictable.

  The efforts are estimates, and nobody measured them.

  Start with a measurement when speed becomes a problem. Each exact change must be made in the Rust port too, because the same seed must give the same iterations in both languages.
