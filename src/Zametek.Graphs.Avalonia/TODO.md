# TODO - Zametek.Graphs.Avalonia

This file lists the items for the library only. It stays with the folder when the library moves to its own repository. Items that apply to all of the repository belong in the file `docs/TODO.md`. Put a date on each entry when you add it. Delete the entry when you complete the work.

- [ ] **Move the library to a dedicated repository** *(2026-08-02)*:
  - Keep the name `Zametek.Graphs.Avalonia`. Do not use `Zametek.Avalonia.Graphs`. That name captures the `Avalonia` namespace and breaks unqualified references.
  - Make a new `.slnx` file and a CI workflow for the library. Add the NuGet packaging metadata to the csproj file: PackageId, license, readme, icon and source link.
  - Move `test/Zametek.Graphs.Avalonia.Tests` and `test/Zametek.Graphs.Avalonia.TestApp` with the library. The `InternalsVisibleTo("Zametek.Graphs.Avalonia.Tests")` declaration continues to work if the name of the test assembly does not change.
  - Change Zametek.ProjectPlan to use the published package and not the project reference.

- [ ] **Report the 32-bit byte count of a bitmap to Avalonia** *(2026-11-04)*

  Avalonia 12.1.3 sizes the buffer of a `RenderTargetBitmap` with `SKImageInfo.BytesSize` of SkiaSharp, in `WriteableBitmapImpl`. That value is an `int` (width × height × 4), and it wraps above 2 GiB. A negative value causes an `ArgumentException`. A positive value gives a buffer that is too small, and `SKBitmap.Erase` then writes past its end. The result is an access violation, which the debugger of Visual Studio shows as `ExecutionEngineException`.

  Version 12.1.4 and the main branch of Avalonia have the same code. A minimal reproduction is `new RenderTargetBitmap(new PixelSize(296369, 52223), new Vector(192, 192))`. The library does not get to this size, because `RasterSizeLimit` keeps each bitmap at 256 MiB or less. Keep the limit after a fix in Avalonia, because it also keeps the memory of an export small.

  The JPEG encoder of SkiaSharp also stops the process on a side longer than 65,500 pixels. A report to SkiaSharp can be useful too.

- [ ] **System.Reactive is an explicit dependency by design** *(2026-08-02)* - Since version 24, ReactiveUI does not supply it transitively. The public API of the library exposes Rx types directly (`IGraphHost.RebuildRequested` is `IObservable<Unit>`). Thus, the removal of System.Reactive is a breaking change of the API for consumers. Review this item only together with the item for all of the repository in `docs/TODO.md`. That item cannot start until DynamicData and Dock stop their dependency on Rx.
