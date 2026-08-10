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

- [ ] **Formal drag-and-drop from charts** *(2026-08-02, consolidated from a code
  TODO)* - `ScottPlotUserControl.CheckPointerDrag` detects the click-vs-drag threshold
  but only tracks state; the inline comment marks where `DragDrop.DoDragDropAsync`
  would start a real DND operation (e.g. dragging a chart image into another app).

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
