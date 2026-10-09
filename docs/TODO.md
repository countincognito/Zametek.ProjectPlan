# TODO

This file lists the maintainer-internal engineering intents that must version with the source code. GitHub issues take the bugs and the feature requests of users. Items that stay valid after the spin-out of Zametek.Graphs.Avalonia belong in [src/Zametek.Graphs.Avalonia/TODO.md](../src/Zametek.Graphs.Avalonia/TODO.md). Put a date on each entry when you add it. Delete the entry when you complete the work.

- [ ] **Remove System.Reactive when DynamicData and Dock have no dependency on Rx** *(2026-08-02, last checked 2026-08-15)*

  ReactiveUI 24 no longer uses System.Reactive. But removal has no value while other packages still depend on it. Thus, the assembly ships in all cases.

  **Dock.** At the last check, Dock was in the middle of a split. Version 12.1.0.1 introduced the suffixed System.Reactive lane (`Dock.Model.ReactiveUI.Reactive`, with `ReactiveUI.Reactive`). It documents the unsuffixed family as the ReactiveUI.Primitives lane (`docfx/articles/dock-reactiveui.md` in the Dock repository).

  The shipped unsuffixed `Dock.Model.ReactiveUI` 12.1.0.1 still declares `System.Reactive` 7.0.0. But Dock master no longer has the reference. Thus, the next unsuffixed release is expected to open the Dock gate.

  **DynamicData.** `DynamicData` is the remaining hard blocker in released packages. Versions 9.4.33 and 9.5.0-preview.15 both declare `System.Reactive` 6.1.0, and no suffixed twin exists. But the direction is certain. PR [reactivemarbles/DynamicData#1116](https://github.com/reactivemarbles/DynamicData/pull/1116) migrates DynamicData to ReactiveUI.Primitives.

  The maintainer plans to ship it in a 10.0.0-preview. At the last check, the naming of the packages was still undecided: a suffixed convention like ReactiveUI and Dock, or explicit dual names. Thus, the Rx-free DynamicData arrives as a MAJOR bump (10.x), with previews first.

  **Watch.** Confirm that the next unsuffixed Dock release drops `System.Reactive`. Then watch for a stable DynamicData 10.x without it.

  **Caution for version bumps.** This repository is deliberately in the unsuffixed Primitives lane. Do not adopt a package with the suffix `.Reactive`. The two families have distinct type identities, and you must not mix them.

  **After both gates open.** Examine the migration of the own Rx use of this repository to `ReactiveUI.Primitives`. The migration must close these gaps:

  - `FromEventPattern` does not exist. Two uses are the collection-changed bridges in the effort tracking manager.
  - `Subject` and `BehaviorSubject` do not exist. Primitives has different "Signal" abstractions.
  - `Observable.Create` does not exist. `MuteWhile` uses it.

  **Starting points.** Comments above the `System.Reactive` reference in `Zametek.Graphs.Avalonia.csproj` and the Dock and DynamicData block in `Zametek.ViewModel.ProjectPlan.csproj` mark the places. The bridge `ObservableExtensions.ObserveOn(ISequencer)` is the seam where the unwinding starts.

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
  - The move of the core callbacks of the display settings (`SetIsProjectScenarioUpdated` and `IsReadyToCompile`) out of all the lock blocks of the setters.
  - Ref-counted guards for revision in the Gantt and earned-value selectors. A shared bool let the `finally` of one reviser clear the guard of the other reviser during the revision, across threads.

  The REMAINING sites that call out under a lock are not live bugs, because every remaining edge is one-way with the point fixes in place. A fix for them is hardening. These sites form the deferred surgery. The list that follows has the items in descending order of value:

  1. `CoreViewModel.ProcessProjectScenario` and `ResetProjectScenario` hold `m_Lock` across the whole fan-out of a load. The fan-out raises the settings setters, raises `IsReadyToReviseTrackers`, and calls `DisplaySettingsViewModel.SetValues`. The `Scheduler.CurrentThread` subscriptions of `IsReadyToReviseTrackers` run the selector revisions synchronously ON the raising thread. Thus, they take selector locks under the core lock. This is the Core→Selector edge in the first dump. The call to `SetValues` is the Core→DS edge in the second dump.

     The surgery splits each method into two phases. The state phase runs under the lock. The fan-out phase runs after the release. The state phase collects the pending raises and signals, and the fan-out phase emits them outside the lock. §7 rule 7 makes this safe. Handlers read the live state again, because payloads are stale snapshots. The window of `BeginBulkUpdate` and `EndBulkUpdate` wraps both phases, and thus the semantics of the suppression survive.
  2. `ProjectScenarioDisplaySettingsViewModel.SetValues` assigns about 30 raising setters under the DS lock. Thus, every synchronous observer (the sink gates of ReactiveUI) runs under that lock. The surgery sets the fields and collects the names of the changed properties under the lock. Then it raises all of them after the release. This also removes the re-entrant locking of the setters.
  3. The five selector view models change `ObservableCollection`s inside their locked `Set*` methods. Thus, the `CollectionChanged` handlers run under the selector lock. These handlers raise notifications. In the Gantt and earned-value selectors, they also write through to the DS and the core. The surgery has two options. One option detaches the handler around the locked changes and does one manual refresh and raise after the release. The other option computes the change set under the lock and applies it outside.
  4. The plot-model setters of the chart managers (the earned-value, Gantt and resource charts) and `ResourceTrackerSetViewModel.RefreshIndex` raise inside their locks. These are trivial splits: set under the lock, and raise after the release.
  5. Document the global DAG of lock acquisition in ARCHITECTURE §7: Manager→Core→DisplaySettings and Core→Selector, with the node and tracker view models as leaves. Verify that the core never calls back into the scenario manager under its lock. Then future calls across view models have an order to follow.

  **Related problems.** The investigation found two more problems in the same area. First, `GanttChartShowConnections` and `EarnedValueShowResources` are plain `List<int>` objects. UI handlers change them without the DS lock, while `SetValues` seeds them under it. This is a risk of torn reads. Second, the DS setters mark the scenario as modified, also when `RaiseAndSetIfChanged` writes nothing.

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

  The scheduler now detects the dead state upstream. Zametek.Maths.Graphs 3.2.0 reports compilation error C0020 and does not spin, and `Compile` takes a cancellation token. The compile watchdog of the application (`CompilationTimeoutMilliseconds`, 2026-08-19) limits all that the scheduler cannot prove dead. One structural item remains from this paragraph: run the compile outside the lock, against a snapshot. It has its own entry below (Phase 2b).

  **Size, and a recommendation not to do the surgery as written** *(2026-08-21)*

  The measurement is against the source code as it is now. The surgery spans these parts:

  - `CoreViewModel`: 2,453 lines and 66 `lock (m_Lock)` sites. The fan-out of the load is about ninety lines under one lock. These lines contain two `SetValues` calls, four assignments of settings that each raise and fan out, and the assignment to `IsReadyToReviseTrackers`. The `Scheduler.CurrentThread` subscriptions of this assignment run selector revisions synchronously on the raising thread.
  - `ProjectScenarioDisplaySettingsViewModel`: 735 lines and 33 lock sites. Its `SetValues` makes sixty property assignments, and each one uses a locked raising setter.
  - The five selector view models: 1,455 lines in total.
  - A handful of trivial splits in the chart managers.
  - The documentation item.

  This is the largest open entry in this file. The risk is higher than the number of lines shows. The change affects WHEN notifications fire in a cascade that already produced three dumps. Verification needs the diff of the CLI worktree and checks of the GUI. The compile work taught that naive concurrency tests have no teeth.

  Two reasons argue against the surgery in this form. First, there is no live bug. The point fixes are in place, and every remaining edge is one-way. Thus, the surgery improves safety and does not fix a bug.

  Second, this entry argues against itself. The direction to explore above makes items 1 to 4 unnecessary and not only done. It also makes item 5 unnecessary: with no locks, there is no DAG of acquisition to document. If the surgery for the lock order comes first, the cost is full for something that the better end state discards.

  Better value is nearby. Phase 2b (run the compile outside `m_Lock`) is small and concrete now that compilation works on private copies.

  **The browser head changes the economics** *(2026-08-21)*

  Single-threaded WebAssembly runs everything on the UI thread in all cases. Thus, "UI-thread affine, no locks, background work on immutable snapshots" is not a costly migration there. The platform already imposes it.

  If the web app becomes worth shipping, this direction is no longer speculative. It becomes the natural shape of the application. Then you must delete this entry. Do not do the work that it lists.

- [ ] **Run the compile outside `m_Lock`** *(2026-08-19, Phase 2b of the work on the data race of the live activity)*

  Phases 1 and 2 both landed on 2026-08-19. The deferred callbacks of the settings that corrupted the input of a compilation are gone (ARCHITECTURE §7 rule 10). A compilation now makes a snapshot of the plan and compiles a throwaway copy of the graph. It publishes the results back through `SetCompiledValues`. A leaf lock makes the snapshot and publish passes exclusive with the own writes of the activities (§7 rule 11).

  One piece remains from the original design, and the design split it out on purpose. `RunCompile` still holds `m_Lock` across the compile itself. Thus, a long compile still blocks every UI binding that touches the core. A compile that never finishes still wedges the application. (Only the watchdog limits this now.)

  The compile no longer needs the lock, because it runs entirely on private copies. Thus, the change is now small. Take `m_Lock` for the snapshot. Release it for the compile. Take it again to publish the results and to assign `GraphCompilation`.

  Compilations can overlap when the lock no longer serializes them. This case needs thought. There are two options. One option drops a second compilation while one is in progress. `IsReadyToCompile` already arms and does not queue, and thus this option is close to the behavior today.

  The other option lets the later result win and discards the earlier one. In both options, `CompilationOutputRevision` and `HasStaleOutputs` must still describe the compilation that the application published.

  Consider the seam for the cancellation of the compile. The token is already in place. Thus, a new compilation can cancel the compilation in progress and not let it run to the end.

  Verify the change with the comparison that Phase 2 used, before and after. Run `zpp -i <file> -o <out>` across all sample projects and several scenarios. Compare the output with a diff that ignores `ModifiedOn`. Also use the concurrency tests in `CoreViewModelCompilationTests`.

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
  7. **Check `System.Drawing.Common`.** `Zametek.ViewModel.ProjectPlan` has a direct `PackageReference` to it. Nothing appears to use it, and it throws on any platform other than Windows. If nothing uses it, delete it.

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

  Read `build-msix.ps1`, the two `.wapproj` files and the WiX project with this specific suspicion. Then ask if a gate can cover any of them.

- [ ] **The MSI installer can have the wrong label, and nothing checks its inputs** *(2026-08-21)*

  `Zametek.ProjectPlan.MsiPackager` declares `<Platforms>x86;x64;ARM64</Platforms>`. It names its output `projectplandotnet.<version>.installer.$(Platform)`. Both component files take their payload from `$(var.<project>.TargetDir)win-x64\publish\`, hard-coded (349baed0, which fixed a path that the publish no longer wrote to). Thus, [Build from source](BUILDING.md#msi-installer-windows) and `readme.txt` tell you to use only the platform `x64`.

  A build of the installer for x86 or ARM64 therefore makes a file with a *name* for that architecture that contains x64 binaries. No error occurs at any point. The packager and WiX both succeed. You can find the wrong label only when you install the file on the computer of the architecture that the name shows.

  There are two ways to fix this. One way drives the RID from `$(Platform)`, and thus the two agree. The other way reduces `<Platforms>` to x64 alone. The present arrangement offers three choices and honors one. If you take the first way, change [Build from source](BUILDING.md#msi-installer-windows) and `readme.txt`. They must then say that the platform is the architecture of the installer.

  A related fact makes the problem easy to miss. The packager uses a publish that nothing enforces. [Build from source](BUILDING.md#msi-installer-windows) and `readme.txt` ask you to run `make publish-desktop publish-cli ARCH=x64 OS=win` first. But the project only reads the files in those folders. A stale publish from a previous version looks the same as a new publish.

  The same is true if there is no new publish and the folder holds an older run. A guard can fail the build when the expected payload is absent or older than the output of the referenced project. The guard changes all of these faults from silent to loud.

  CI does not build the MSI, and the `.slnx` file does not build it (`<Build Project="false" />`). Thus, no gate covers any of this. See the entry for the packaging audit above.

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
  - The four subscriptions for selector revision use the payload only as a gate on a readiness flag that they clear themselves. They take every value from a live read.
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

- [ ] **Hold the API of `zpp serve` to its last release** *(2026-10-04, the end of the API review)*

  The API has no release yet. Thus, `/v1` can still change its shape. The `type` URIs of its problems can still change their names. They are the addresses of the sections of [API.md](API.md), and a client branches on them.

  The first release that contains the server ends this freedom. From then on, nobody renames or removes a type, a status, a member or a code ([VER-3](RESTFUL-API-GUIDE.md#12-versioning-and-evolution) of the [RESTful API Guide](RESTFUL-API-GUIDE.md)). A change that breaks a client is a new version of the path.

  Do these actions before that release:

  1. Add [oasdiff](https://github.com/oasdiff/oasdiff) to CI. It runs `breaking` on `docs/openapi.yaml` against the file in the latest release tag. Thus, a change that breaks a client fails the build, and nobody needs to notice it. `OpenApiContractTests` hold the description to the server, and Spectral holds it to the rules of the guide. But nothing holds it to its past.
  2. Give `info.version` of the description and the heading `Unreleased` of the changelog the version of the release.
  3. Decide if the `type` URIs must point to a copy that cannot move. They point to the copy of API.md on the `develop` branch now. A copy in a release tag, or a page that nobody renames, can be this copy. A client that stored a type must never find that its address is dead.
