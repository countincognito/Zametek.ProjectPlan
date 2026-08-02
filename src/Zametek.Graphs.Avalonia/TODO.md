# TODO - Zametek.Graphs.Avalonia

Library-scoped items only: this file travels with the folder when the library is spun
out into its own repository. Repo-wide items belong in the root TODO.md. Date entries
when added; delete them when done.

- [ ] **Spin out into a dedicated repository** *(2026-08-02)*:
  - Keep the name `Zametek.Graphs.Avalonia` (NOT `Zametek.Avalonia.Graphs`, which
    captures the `Avalonia` namespace and breaks unqualified references).
  - Own solution + CI, plus NuGet packaging metadata in the csproj (PackageId, license,
    readme, icon, source link).
  - Move `test/Zametek.Graphs.Avalonia.Tests` and `test/Zametek.Graphs.Avalonia.TestApp`
    across with the library; the `InternalsVisibleTo("Zametek.Graphs.Avalonia.Tests")`
    declaration keeps working as long as the test assembly name is unchanged.
  - Switch Zametek.ProjectPlan from the project reference to the published package.

- [ ] **System.Reactive is a deliberate, explicit dependency** *(2026-08-02)* -
  ReactiveUI 24+ no longer ships it transitively, and the library's public API exposes
  Rx types directly (`IGraphHost.RebuildRequested` is `IObservable<Unit>`), so dropping
  System.Reactive would be a breaking API change for consumers. Revisit only alongside
  the solution-wide item in the root TODO.md (blocked until DynamicData and Dock go
  Rx-free there).
