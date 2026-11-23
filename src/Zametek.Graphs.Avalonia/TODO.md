# TODO - Zametek.Graphs.Avalonia

This file lists the items for the library only. It stays with the folder when the library moves to its own repository. Items that apply to all of the repository belong in the file `docs/TODO.md`. Put a date on each entry when you add it. Delete the entry when you complete the work.

- [ ] **Move the library to a dedicated repository** *(2026-08-02)*:
  - Keep the name `Zametek.Graphs.Avalonia`. Do not use `Zametek.Avalonia.Graphs`. That name captures the `Avalonia` namespace and breaks unqualified references.
  - Make a new `.slnx` file and a CI workflow for the library. Add the NuGet packaging metadata to the csproj file: PackageId, license, readme, icon and source link.
  - Move `test/Zametek.Graphs.Avalonia.Tests` and `test/Zametek.Graphs.Avalonia.TestApp` with the library. The `InternalsVisibleTo("Zametek.Graphs.Avalonia.Tests")` declaration continues to work if the name of the test assembly does not change.
  - Change Zametek.ProjectPlan to use the published package and not the project reference.

- [ ] **Give the background of the fixed-layout SVG the namespace of SVG** *(2026-11-23, from the review of `src`, `test` and `pkg`)*

  `MsaglSvgRenderer` makes the `rect` of the background with `doc.CreateElement("rect")`, and it puts the element first in the document. Such an element has no namespace. The review reported that a graph file from `zpp` thus starts with `<rect ... xmlns="" />` inside the `svg` element.

  An element with an empty namespace is not an SVG element, and a browser does not draw it. With the dark theme, the labels of the edges are white, and the page of a browser is white too. The PNG and PDF exports look correct, and thus Svg.Skia probably accepts the element. Nobody looked at such a file in a browser.

  Make the element in the namespace of the document (`doc.DocumentElement.NamespaceURI`). This changes the bytes of each fixed-layout SVG.

- [ ] **Remove the characters that XML does not permit from the labels of an export** *(2026-11-23, from the review of `src`, `test` and `pkg`)*

  `MsaglSvgGraphWriter` makes the text of a node safe with `new XText(text).ToString()`, and `GraphSerializer` writes GraphML with `XmlSerializer`. The review reported the result for a label with a character that XML 1.0 does not permit, for example U+000B. The fixed-layout SVG, PNG and PDF exports fail with a message of the XML library. The GraphML export writes `&#xB;`, and a reader then refuses the file.

  A project file can give such a character with an escape of JSON, and an import can give one too. The escape of `&`, `<`, `>` and `"` is complete in the three formats, and the Dot export is complete. Nobody ran an export with such a label.

  Remove these characters in one helper, before the two writers get the text.

- [ ] **Stop a drag when the window loses the pointer capture** *(2026-11-23, from the review of `src`, `test` and `pkg`)*

  No handler of the library listens to `PointerCaptureLost`. `GraphNodeDragBehavior` clears `m_IsDragging` only when the user releases the button, and `OnPointerMoved` does not make sure that the button is down. The pan in `InteractiveGraphView` (`m_IsPanning`) has the same form.

  If the window loses the capture during a drag, for example with Alt+Tab, no release arrives. The review reported that a node then follows the pointer with no button down. Nobody did this in an application.

  Handle `PointerCaptureLost` in the two places, and stop the drag.

- [ ] **Do seven things before the library has consumers** *(2026-11-23, from the review of `src`, `test` and `pkg`)*

  1. **Classes with no namespace.** `Serialization/Schema/GraphmlSchemaClass.cs` has no namespace. It gives each consumer 23 public classes in the global namespace, with names such as `data`, `graphml` and `ShapeNode`. `GraphMLBuilder.ToGraphML` is public and returns `graphml`. Put the file in a namespace, and make the classes and the builders internal. `IGraphSerializer` is the public way to the format.
  2. **The font cache.** `InteractiveGraphRenderer.s_FontCache` is a static `Dictionary`, and its comment says that the render is on the UI thread only. The README offers the renderer to a host with no UI thread, for example a server. Two jobs can then write the dictionary at the same time, and they share `SKFont` objects. Use a `ConcurrentDictionary`, or a lock.
  3. **An export that writes nothing.** The fixed-layout export of `InteractiveGraphViewModel` returns with no write in three cases. The host has compilation errors, the SVG has no picture, or the graph type is `None`. The README says that `WriteImageAsync` throws and does not write an empty image. Throw `InvalidOperationException` in these cases.
  4. **A diagram with a fault.** `MsaglGraphLayoutEngine` does no check of the diagram. An edge to a node that does not exist gives a `KeyNotFoundException`, and two nodes with one id give an `ArgumentException`. `NodeLabelFontSize` divides by the length of the text, and thus an empty text gives a font size with no limit. Do one check at the start of the layout, with clear messages.
  5. **The format of a color.** The README says that a color is `#RRGGBB` or `#AARRGGBB`. `ColorHelper` reads the alpha value last (`#RRGGBBAA`). For a text that it cannot read, it returns a color that is fully transparent. Thus, a consumer who follows the README gets a wrong color, or a node that the user cannot see. Correct the README, and use the fallback brush for a text that `ColorHelper` cannot read.
  6. **The ids in GraphML.** `GraphMLBuilder.FormatGraphNodeId` and `FormatGraphEdgeId` write the id in the current culture. Some cultures have a minus sign that is not `-`, and an id can be negative. The comment of `FormatNumber` in the same file says that the file is the same in each culture. Use the invariant culture.
  7. **Long work on the UI thread.** For a High Fidelity export, `InteractiveGraphRasterRenderer` encodes the bitmap as PNG and decodes it again on the UI thread. The bitmap can have 67 megapixels. `ResetLayout` of `InteractiveGraphViewModel` runs the MSAGL layout on the UI thread, and `Refresh` runs the same layout on a different thread. Nobody measured the two.

- [ ] **Report the 32-bit byte count of a bitmap to Avalonia** *(2026-11-04)*

  Avalonia 12.1.3 sizes the buffer of a `RenderTargetBitmap` with `SKImageInfo.BytesSize` of SkiaSharp, in `WriteableBitmapImpl`. That value is an `int` (width × height × 4), and it wraps above 2 GiB. A negative value causes an `ArgumentException`. A positive value gives a buffer that is too small, and `SKBitmap.Erase` then writes past its end. The result is an access violation, which the debugger of Visual Studio shows as `ExecutionEngineException`.

  Version 12.1.4 and the main branch of Avalonia have the same code. A minimal reproduction is `new RenderTargetBitmap(new PixelSize(296369, 52223), new Vector(192, 192))`. The library does not get to this size, because `RasterSizeLimit` keeps each bitmap at 256 MiB or less. Keep the limit after a fix in Avalonia, because it also keeps the memory of an export small.

  The JPEG encoder of SkiaSharp also stops the process on a side longer than 65,500 pixels. A report to SkiaSharp can be useful too.

- [ ] **System.Reactive is an explicit dependency by design** *(2026-08-02)* - Since version 24, ReactiveUI does not supply it transitively. The public API of the library exposes Rx types directly (`IGraphHost.RebuildRequested` is `IObservable<Unit>`). Thus, the removal of System.Reactive is a breaking change of the API for consumers. Review this item only together with the item for all of the repository in `docs/TODO.md`. That item cannot start until DynamicData and Dock stop their dependency on Rx.
