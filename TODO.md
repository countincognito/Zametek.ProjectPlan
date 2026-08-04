# TODO

Maintainer-internal engineering intents that should version with the code. User-facing
bugs and feature requests belong in GitHub issues; items that would survive the
Zametek.Graphs.Avalonia spin-out belong in [src/Zametek.Graphs.Avalonia/TODO.md](src/Zametek.Graphs.Avalonia/TODO.md).
Date entries when added; delete them when done.

- [ ] **Remove System.Reactive once DynamicData and Dock go Rx-free** *(2026-08-02)* -
  ReactiveUI 24 no longer uses System.Reactive, but removal is pointless while
  `Dock.Model.ReactiveUI` (>= 7.0.0) and `DynamicData` (>= 6.1.0) still hard-depend on
  it, so the assembly ships regardless. When a NuGet update shows **both** have gone
  Rx-free, examine migrating the solution's own Rx usage to `ReactiveUI.Primitives`.
  Known gaps to solve at that point: no `FromEventPattern` (two uses, the
  collection-changed bridges in the effort tracking manager), no `Subject`/`BehaviorSubject` (Primitives has
  different "Signal" abstractions), and no `Observable.Create` (`MuteWhile` is built on
  it). Breadcrumbs: comments above the `System.Reactive` reference in
  `Zametek.Graphs.Avalonia.csproj` and the Dock/DynamicData block in
  `Zametek.ViewModel.ProjectPlan.csproj`; the `ObservableExtensions.ObserveOn(ISequencer)`
  bridge is the seam from which to start unwinding.

- [ ] **Spin out Zametek.Graphs.Avalonia into its own repository** *(2026-08-02)* - the
  library is already framework-decoupled with its own README; the checklist lives in
  [src/Zametek.Graphs.Avalonia/TODO.md](src/Zametek.Graphs.Avalonia/TODO.md) so it
  travels with the folder.

- [ ] **Consider renaming `TimesheetHelper`** *(2026-08-02, low priority)* - its name is
  now slightly broader than "effort timesheet" (it hosts the shared `DayCount` constant
  used by both tracking tabs); something like `TrackingHelper` may fit better.

- [ ] **Include import/export states in the busy-cursor aggregate** *(2026-08-02,
  consolidated from a code TODO)* - `MainView.axaml.cs` stubs out
  `main => main.IsImporting` / `main => main.IsExporting` in the `WhenAnyValue` that
  drives `UpdateCursor`, so the wait cursor does not show during scenario import/export.
  Needs the corresponding properties on `IMainViewModel`/`MainViewModel`, then reinstate
  the commented lines.

- [ ] **Formal drag-and-drop from charts** *(2026-08-02, consolidated from a code
  TODO)* - `ScottPlotUserControl.CheckPointerDrag` detects the click-vs-drag threshold
  but only tracks state; the inline comment marks where `DragDrop.DoDragDropAsync`
  would start a real DND operation (e.g. dragging a chart image into another app).

- [ ] **Resolve the parked members in `UpdateDependentActivityModel`** *(2026-08-02)* -
  `Dependencies`/`IsDependenciesEdited` are commented out in the model
  (`Zametek.Common.ProjectPlan/Dependencies`), meaning dependencies cannot be edited
  through the bulk-update path. Either support them or delete the stubs.

- [ ] **Latent `NotImplementedException` stubs** *(2026-08-02)* - the headless CLI's
  `SettingService.SetDataGridLayout` throws while its sibling members (`DockLayout`,
  `GetDataGridLayout`) gracefully no-op; make it a no-op for consistency so a future
  call path cannot crash the CLI. Related: `ManagedItemDataGridDropHandler.MakeCopy`
  throws (copy-drag is unsupported; only move-reorder is used) - confirm the base
  handler can never route a copy operation there, or implement it.

- [ ] **Sweep remaining commented-out dead members** *(2026-08-02)* - leftovers from
  earlier rounds: the `GetAllocatedToActivitiesString` block in
  `ResourceActivitySelectorViewModel` and the `RawTargetResourceActivities` line in
  `IResourceActivitySelectorViewModel`. Also dead markup left by the chart
  Plot-ownership refactor: the now-unused `xmlns:scottplot` declarations in the four
  chart view axaml files (the AvaPlot host is created in code-behind now) and the empty
  `<local:ScottPlotUserControl.Resources>` element in `EarnedValueChartManagerView.axaml`.

- [ ] **Move the project-open cascade off the UI thread** *(2026-08-04)* - the
  open/load path still runs the compile and several output builds (network metrics,
  arrow graph) on the UI thread, producing the one-off stall of a second or two at
  startup and project open. The edit path now compiles entirely on the taskpool;
  align the load path with it. Identified during the edit-freeze investigation
  (telemetry + dotnet-trace); `PerfTelemetry` remains available for re-measuring.

- [ ] **Deduplicate the Gantt chart double rebuild** *(2026-08-04)* -
  `BuildGanttChartPlotModel` runs roughly twice per compile because its trigger
  fires for more than one compile-driven input (e.g. `ResourceSeriesSet` plus
  `Metrics`); conflate so one compile yields one rebuild. Harmless now that the
  builds are off the UI thread, but still wasted work.

- [ ] **Consider gating the stale-outputs border like the busy overlay**
  *(2026-08-04)* - with the edit freeze fixed, the red border flash per edit is
  purely cosmetic (roughly 0.1-0.3s of honest staleness). If it proves visually
  noisy, the sustained-delay pattern used for the busy signal in `MainViewModel`
  would suppress sub-perceptible flashes without hiding real staleness.

- [ ] **Reduce UI-thread reads of locked view-model getters** *(2026-08-04)* - the
  dotnet-trace profile of an edit burst showed several seconds of Monitor
  contention: bindings re-reading locked getters while the background compile
  cascade held the locks. Most of it vanished with the IsBusy fix; if input
  hitches reappear under heavy background activity, prefer lock-free snapshots
  (volatile fields or immutable models) for hot bound properties.

- [ ] **Consider reporting the Dock float/re-dock behaviour upstream** *(2026-08-02,
  optional)* - under Dock 12.1, dropping a floating tool back into the layout
  materialises the destination view while the closed float window's visual tree is
  still assembled (12.0.0.2 did not surface this). Our crash was ultimately the app's
  own shared-control-as-Content pattern - now fixed by construction in
  `ScottPlotUserControl` - but a minimal repro (any dockable binding a shared control
  instance as Content) may be worth filing against Dock so the cross-window
  materialisation ordering gets a look.
