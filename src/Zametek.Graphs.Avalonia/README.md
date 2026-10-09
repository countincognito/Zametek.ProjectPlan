# Zametek.Graphs.Avalonia

Zametek.Graphs.Avalonia is a reusable **interactive graph control** that you can embed in an [Avalonia](https://avaloniaui.net/) application. One generic family of controls draws directed graphs: activity-on-**arrow** graphs and activity-on-**vertex** graphs. The controls give you dragging, highlighting of the selected item with a click, tooltips on hover, unbounded pan and zoom, and automatic layout with [MSAGL](https://github.com/microsoft/automatic-graph-layout). They also export PNG, JPEG, PDF and SVG images, and GraphML and GraphViz data.

The application supplies everything that is specific to the application through one thin interface. This includes your domain graph, the settings, and the dialogs for save and error. Thus, a consumer writes an adapter and one line to connect it. The consumer does not write the logic for interaction, layout or export.

- **Target framework:** `net10.0`
- **Key dependencies:** Avalonia 12, ReactiveUI.Avalonia 12, SkiaSharp (through Svg.Skia), MSAGL (`AutomaticGraphLayout.Drawing`) and `Xaml.Behaviors.Avalonia`.
- **Root namespace and XAML namespace:** `Zametek.Graphs.Avalonia`
- Compiled bindings are on by default (`AvaloniaUseCompiledBindingsByDefault=true`).

---

## Contents

1. [Structure of the library](#structure-of-the-library)
2. [Quick start](#quick-start)
3. [Data formats](#data-formats)
4. [Presentation style](#presentation-style)
   - [Re-skin with `GraphAppearance`](#re-skin-with-graphappearance)
   - [Custom node / edge templates](#custom-node--edge-templates)
5. [Export and export style](#export-and-export-style)
   - [Vector or high-fidelity (raster)](#vector-or-high-fidelity-raster)
   - [`GraphVectorExportStyle`](#graphvectorexportstyle)
   - [Select a mode, and connect copy and save](#select-a-mode-and-connect-copy-and-save)
6. [Persistence of the arrangement](#persistence-of-the-arrangement)
7. [Built-in interactions](#built-in-interactions)
8. [Threading and known problems](#threading-and-known-problems)

---

## Structure of the library

The library uses the MVVM pattern. You provide data and services. The library owns the view-model, the layout and the UI.

```
 Your app                         Zametek.Graphs.Avalonia
 ────────                         ───────────────────────
 domain graph ──► IGraphHost ──►  InteractiveGraphViewModel ──►  InteractiveGraphView
 (whatever         (thin adapter   • runs MSAGL layout            (the control you place
  you already       you write)     • builds node/edge VMs          in your XAML; binds to
  have)                            • drag / select / zoom          IInteractiveGraph)
                                   • reroute edges
                                   • copy / save / export
       BuildDiagram(...) ─────────► DiagramGraphModel ──► [MSAGL] ──► GraphLayoutModel ──► node/edge VMs
       (coordinate-free "what to draw")                    (adds positions)     (what the control renders)
```

These are the key types:

| Type | Role | You supply? |
|---|---|---|
| **`InteractiveGraphView`** | The Avalonia `UserControl` that you put in XAML. | - (use it) |
| **`IInteractiveGraph`** | The contract that the control binds to. | - |
| **`InteractiveGraphViewModel`** | The reusable implementation of `IInteractiveGraph`. It runs the layout, holds the interactive node and edge view-models, and drives the export. | make it |
| **`IGraphHost`** | A thin adapter to *your* application: theme, data, rebuild signal, and the dialogs for save and error. | **implement it** |
| **`IGraphLayoutEngine`** → `MsaglGraphLayoutEngine` | It runs the MSAGL layout and makes the SVG. | use the default |
| **`IGraphSerializer`** → `GraphSerializer` | It makes the GraphML and GraphViz output. | use the default |
| **`GraphConfiguration`** (+ `GraphConfigurations.Arrow` / `.Vertex`) | The layout tuning for each graph: the sizes of nodes and labels, and the routing. | pick a preset |
| **`GraphAppearance`** | The theme of the presentation: brushes, fonts and opacities. *Optional.* | optional re-skin |
| **`IGraphDispatcher`** → `AvaloniaGraphDispatcher` / `InlineGraphDispatcher` | It sends the work that must run on the UI thread to that thread. *Optional.* | the default, or inline for a host that has no UI thread |

The same `InteractiveGraphViewModel` is the view-model for arrow graphs and vertex graphs. The only differences are the `GraphConfiguration` preset and, optionally, a `GraphAppearance` or templates.

---

## Quick start

### 1. Implement `IGraphHost`

This is the only interface that you must write. It adapts your application to the library:

```csharp
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Zametek.Graphs.Avalonia;

public sealed class MyGraphHost : IGraphHost, IDisposable
{
    // Seeded so subscribing (the view-model does so in its ctor) produces the FIRST layout.
    private readonly BehaviorSubject<Unit> _rebuild = new(Unit.Default);
    private GraphTheme _theme = GraphTheme.Light;
    private bool _showNames;

    public GraphTheme Theme => _theme;

    public bool ShowNames
    {
        get => _showNames;
        set { if (_showNames != value) { _showNames = value; _rebuild.OnNext(Unit.Default); } }
    }

    // True when there is nothing valid to draw (e.g. your model doesn't compile).
    public bool HasCompilationErrors => false;

    // Translate YOUR domain graph into the library-neutral diagram (see "Data formats").
    public DiagramGraphModel BuildDiagram(bool multiLineEdgeLabels) =>
        MyDomain.ToDiagram(_showNames, multiLineEdgeLabels);

    // Observe OFF the UI thread so the MSAGL layout the view-model runs never blocks it.
    public IObservable<Unit> RebuildRequested => _rebuild.ObserveOn(TaskPoolScheduler.Default);

    public Task<string?> PickSaveFileAsync() => /* your save-file dialog, or null if cancelled */;
    public Task ReportErrorAsync(string message) => /* your error dialog */;

    // Call whenever your data/theme changes so the graph rebuilds.
    public void SetTheme(GraphTheme theme)
    {
        if (_theme != theme) { _theme = theme; _rebuild.OnNext(Unit.Default); }
    }
    public void Rebuild() => _rebuild.OnNext(Unit.Default);

    public void Dispose() { _rebuild.OnCompleted(); _rebuild.Dispose(); }
}
```

The library rebuilds the graph **each time `RebuildRequested` fires**. You own the throttling and the scheduling. The example observes on the task pool. The seed of the `BehaviorSubject` produces the initial layout.

### 2. Make the view-model

```csharp
var host = new MyGraphHost();

var interactive = new InteractiveGraphViewModel(
    host,
    new MsaglGraphLayoutEngine(),   // default layout engine
    new GraphSerializer(),          // default GraphML/GraphViz serializer
    GraphConfigurations.Arrow);     // or .Vertex, or your own GraphConfiguration
    // optional 5th arg: a GraphAppearance to re-skin (see below)
    // optional named arg dispatcher: an IGraphDispatcher (see Threading and known problems)

// Expose it to your view as IInteractiveGraph:
public IInteractiveGraph Interactive => interactive;
```

`InteractiveGraphViewModel` is `IDisposable`. Dispose it and your host when your own view-model, which owns them, ends.

### 3. Put the control in XAML

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:g="using:Zametek.Graphs.Avalonia"
             x:Class="MyApp.Views.MyGraphView">

    <!-- Default look. No templates, no appearance = the built-in presentation. -->
    <g:InteractiveGraphView DataContext="{Binding Interactive}"/>

</UserControl>
```

These three steps are the full integration. The control gives you dragging, selection, pan and zoom, the context menu, and export. The context menu has the routing modes, *Fit to View*, *Reset Layout*, and the items for copy and save.

---

## Data formats

You do not give the library coordinates or Avalonia objects. You make a **`DiagramGraphModel`**. This is a flat description of *what* to draw, with no coordinates. The presentation is in hex colors and simple enums. MSAGL calculates the positions.

```csharp
var diagram = new DiagramGraphModel
{
    Nodes =
    {
        new DiagramNodeModel
        {
            Id = 1,                          // unique int; edges reference it
            Text = "1",                      // the node's label
            FillColorHexCode = "#EAF1FB",    // "#RRGGBB" or "#AARRGGBB"
            BorderColorHexCode = "#33475B",
            BorderThickness = 1.2,
            BorderDashStyle = GraphDashStyle.Normal,   // or Dashed
            Tooltip = "Event 1",             // hover tooltip (optional)
            // Width/Height default from the GraphConfiguration; X/Y only matter for GraphML output.
        },
        // ...
    },
    Edges =
    {
        new DiagramEdgeModel
        {
            Id = 1,
            SourceId = 1,                    // must match a node Id
            TargetId = 2,
            ForegroundColorHexCode = "#C0392B",
            StrokeThickness = 1.5,
            DashStyle = GraphDashStyle.Normal,
            Label = "A",                     // edge label text
            ShowLabel = true,                // arrow graphs show labels; vertex graphs typically don't
            Tooltip = "Activity A",
        },
        // ...
    },
};
```

### Model reference

**`DiagramNodeModel`** has these members: `Id`, `X`, `Y`, `Width`, `Height`, `FillColorHexCode?`, `BorderColorHexCode?`, `BorderDashStyle` (`GraphDashStyle`), `BorderThickness`, `Text?`, `Name?` and `Tooltip?`. `X` and `Y` have a meaning only in the GraphML export. The positions on the screen come from the layout pass.

**`DiagramEdgeModel`** has these members: `Id`, `Name?`, `SourceId`, `TargetId`, `DashStyle` (`GraphDashStyle`), `ForegroundColorHexCode?`, `StrokeThickness`, `Label?`, `ShowLabel` and `Tooltip?`.

**Enums and helper types**

- `GraphDashStyle` has the values `Normal` and `Dashed`.
- `GraphTheme` has the values `Light` and `Dark`. Map your own theme to it in `IGraphHost.Theme`. The canvas background and the export background follow it.
- Colors are hex strings that `Color.Parse` of Avalonia reads (`#RRGGBB` or `#AARRGGBB`). For `null`, the library uses the fallback brushes of `GraphAppearance`.

### Choose and tune the configuration

Give the view-model a preset:

- **`GraphConfigurations.Arrow`** has event nodes with single-line labels and edges with labels. It has a toggle that shows the names.
- **`GraphConfigurations.Vertex`** has activity nodes with a three-line label box and edges with no labels.

You can also make your own `GraphConfiguration` (a `record`). With it, you can change the sizes of the node and label boxes, the font, the default `EdgeRoutingMode`, and the `InteractiveLayoutScalingFactor`. These values tune the **MSAGL layout**. They set the size of the boxes that MSAGL lays out, and this also sets the sizes of the interactive nodes. Thus, treat them as a matched set. Start from a preset and adjust it with `with { ... }`.

### Edge routing

`GraphEdgeRoutingMode` has these values: `None`, `Spline`, `SplineBundling`, `StraightLine`, `SugiyamaSplines`, `Rectilinear` and `RectilinearToCenter`. The presets use `SugiyamaSplines` for arrow graphs and `Spline` for vertex graphs. The user can switch the mode in the context menu. The fixed-layout SVG export uses each mode fully. The live canvas draws a fast local approximation.

---

## Presentation style

Two independent methods are available. You can use one method, or the two together:

- **`GraphAppearance`** changes the appearance of the *built-in* templates (brushes, fonts, opacities and metrics) without XAML.
- **`NodeTemplate` and `EdgeTemplate`** replace the drawn **body** of the node or edge with your own Avalonia visuals.

### Re-skin with `GraphAppearance`

Give a `GraphAppearance` to the constructor of the view-model. Start from `Default` and override the members that you want to change:

```csharp
using Avalonia.Media;
using Avalonia.Media.Immutable;

var appearance = GraphAppearance.Default with
{
    SelectionBrush     = new ImmutableSolidColorBrush(Color.Parse("#FF7A00")),   // NOTE: immutable, not
    NodeLabelBrush     = new ImmutableSolidColorBrush(Colors.White),             // SolidColorBrush - see
    NodeLabelFontFamily = new FontFamily("Cascadia Code"),                       // Threading and known problems
    NodeCornerRadius   = 6.0,
    EdgeDefaultBrush   = new ImmutableSolidColorBrush(Color.Parse("#667085")),
    DashPattern        = new double[] { 4.0, 2.0 },
};

var interactive = new InteractiveGraphViewModel(
    host, new MsaglGraphLayoutEngine(), new GraphSerializer(),
    GraphConfigurations.Vertex, appearance);
```

The members of `GraphAppearance` are all `init`. The defaults give the standard appearance of the library:

| Group | Members |
|---|---|
| Selection | `SelectionBrush`, `HighlightStrokeThickness` |
| Nodes | `NodeFillFallbackBrush`, `NodeBorderFallbackBrush`, `NodeCornerRadius`, `DefaultNodeBorderThickness`, `NodeDimmedOpacity`, `NodeLabelFontFamily`, `NodeLabelFontSize`, `NodeLabelBrush` |
| Edges | `EdgeDefaultBrush`, `DefaultEdgeStrokeThickness`, `EdgeDimmedOpacity`, `EdgeLightLabelBrush`, `EdgeDarkLabelBrush`, `EdgeLabelFontFamily`, `EdgeLabelFontSize`, `ArrowLength`, `ArrowHalfWidth`, `DashPattern` |

> The library bundles the label font. The two label font families use **Cascadia Mono** by default. The library supplies this font as an Avalonia resource (SIL Open Font License, `Assets/Fonts/OFL.txt`). It does not look for the font on the computer. Thus, the labels look the same on all computers.
>
> The screen, the canvas export and the rasterized fixed layout all draw from the bundled files. This is true also where Cascadia Mono is not installed.
>
> The fixed-layout SVG names `Cascadia Mono, Consolas, monospace`, for viewers that cannot see the bundled copy. For each other font family that you set, the library looks on the system, as usual. The label factors of the presets match the metrics of Cascadia Mono. Thus, it can be necessary to set other factors for a different font.

> ⚠️ **Font-family properties must have the type `Avalonia.Media.FontFamily`, and not `string`.** With compiled bindings, the binding of a `string` to `FontFamily` throws an exception at runtime, and the library silently uses the default font. This applies to `GraphAppearance` and to the template contract that follows.

### Custom node / edge templates

Set `NodeTemplate`, `EdgeTemplate` or both on the control to replace the drawn **body**. The control still owns the position, the drag, the selection ring, the dimming, the wide invisible area that receives pointer input, and the tooltip. Your template draws only the visible node or edge. Bind to the stable contract interfaces (`x:DataType`) and not to the concrete view-model:

- The node body uses **`IGraphNodeViewModel`**.
- The edge body uses **`IGraphEdgeViewModel`**.

```xml
<g:InteractiveGraphView DataContext="{Binding Interactive}">

    <g:InteractiveGraphView.NodeTemplate>
        <DataTemplate x:DataType="g:IGraphNodeViewModel">
            <Grid>
                <Ellipse Fill="{Binding FillBrush}"
                         Stroke="{Binding BorderBrush}"
                         StrokeThickness="{Binding BorderThickness}"/>
                <TextBlock Text="{Binding Label}"
                           FontFamily="{Binding LabelFontFamily}"
                           FontSize="{Binding LabelFontSize}"
                           Foreground="{Binding LabelBrush}"
                           HorizontalAlignment="Center" VerticalAlignment="Center"
                           IsHitTestVisible="False"/>
            </Grid>
        </DataTemplate>
    </g:InteractiveGraphView.NodeTemplate>

    <g:InteractiveGraphView.EdgeTemplate>
        <DataTemplate x:DataType="g:IGraphEdgeViewModel">
            <Canvas>
                <Path Data="{Binding EdgeGeometry}"
                      Stroke="{Binding Stroke}"
                      StrokeThickness="{Binding StrokeThickness}"
                      StrokeDashArray="{Binding StrokeDashArray}"
                      Opacity="{Binding EdgeOpacity}"
                      IsHitTestVisible="False"/>
                <Polygon Points="{Binding ArrowPoints}"
                         Fill="{Binding Stroke}"
                         Opacity="{Binding EdgeOpacity}"/>
                <TextBlock Canvas.Left="{Binding LabelX}" Canvas.Top="{Binding LabelY}"
                           Text="{Binding Label}" IsVisible="{Binding ShowLabel}"
                           FontFamily="{Binding LabelFontFamily}"
                           FontSize="{Binding LabelFontSize}"
                           Foreground="{Binding LabelBrush}"/>
            </Canvas>
        </DataTemplate>
    </g:InteractiveGraphView.EdgeTemplate>

</g:InteractiveGraphView>
```

**The contract that you can bind to:**

- `IGraphNodeViewModel` has these members: `Id`, `Width`, `Height`, `Label`, `Tooltip`, `FillBrush`, `BorderBrush`, `BorderThickness`, `StrokeDashArray`, `CornerRadius`, `LabelFontFamily`, `LabelFontSize`, `LabelBrush`, `SelectionBrush`, `IsSelected`, `IsDimmed` and `NodeOpacity`. You can read and write `X` and `Y`, but the control owns them for the drag and the positioning. Do not bind them for layout.
- `IGraphEdgeViewModel` has these members: `Id`, `EdgeGeometry`, `Stroke`, `StrokeThickness`, `StrokeDashArray`, `EdgeOpacity`, `ArrowPoints`, `Label`, `ShowLabel`, `LabelBrush`, `LabelFontFamily`, `LabelFontSize`, `LabelX`, `LabelY` and `Tooltip`.

The library supplies a `g:HalfNegativeConverter`. It moves a control that has a position on a canvas by minus half of its own size. Use it to center an edge label or chip on its `LabelX` and `LabelY` anchor. The library keeps the built-in body for each item that you leave unset.

---

## Export and export style

The control can put the graph on the clipboard. It can also save images (PNG, JPEG, PDF and SVG) and data (GraphML and `.dot`). Image export has **two modes**.

### Vector or high-fidelity (raster)

`GraphExportMode` has two values:

| Mode | What it draws | Best for |
|---|---|---|
| **`Vector`** (default) | The control draws crisp shapes imperatively in SkiaSharp, as a `GraphVectorExportStyle` describes. The shapes are true vector in SVG and PDF. This mode **does not** read your custom templates. | Scalable, razor-sharp output, and an approximation of a custom appearance in vector form |
| **`Raster`** ("High Fidelity") | The control renders the **real** `NodeTemplate` and `EdgeTemplate` to a bitmap (2× supersampled) and embeds it into the SVG or PDF. It reproduces gradients, shadows and arbitrary shapes exactly. | Pixel-exact reproduction of a bespoke template |

The vector renderer cannot read an arbitrary template. Thus, you *describe* the appearance that you want with a `GraphVectorExportStyle`. The raster path needs no configuration, because it uses the templates directly.

### `GraphVectorExportStyle`

`GraphVectorExportStyle` is an immutable record. Start from `Default`. Set it on the control with `VectorExportStyle`:

```csharp
using Avalonia.Media;

graphView.ExportMode = GraphExportMode.Raster;  // default copy/save/menu to high-fidelity if you like

graphView.VectorExportStyle = GraphVectorExportStyle.Default with
{
    NodeShape = GraphExportNodeShape.Ellipse,

    // Honours gradient brushes as a true vector gradient (or a solid brush as a flat colour):
    NodeFillOverride = new RadialGradientBrush
    {
        GradientStops =
        {
            new GradientStop(Color.Parse("#6D8BFF"), 0.0),
            new GradientStop(Color.Parse("#2A3F9D"), 1.0),
        },
    },
    NodeBorderThicknessOverride = 2.0,          // override the data border weight
    NodeLabelFontWeight = FontWeight.Bold,

    ShowNodeGlow = true,                         // soft outer halo (an SVG blur)
    NodeGlowBrush = new ImmutableSolidColorBrush(Color.Parse("#6D8BFF")),
    NodeGlowBlurRadius = 16.0,
    NodeGlowOpacity = 0.75,

    ShowEdgeLabelChip = true,                    // rounded background behind edge labels
    EdgeLabelChipBorderBrush = new ImmutableSolidColorBrush(Color.Parse("#3B82F6")),
    EdgeLabelChipTextBrush = new ImmutableSolidColorBrush(Color.Parse("#EAF1FB")),
};
```

The table that follows groups the members:

| Group | Members |
|---|---|
| Node shape/fill | `NodeShape` (`GraphExportNodeShape`: `RoundedRectangle`, `Ellipse`, `Rectangle`, `Capsule`), `NodeFillOverride` (solid **or** gradient brush) |
| Node border | `NodeBorderOverride`, `NodeBorderThicknessOverride` |
| Node label | `NodeLabelFontWeight` |
| Node glow | `ShowNodeGlow`, `NodeGlowBrush`, `NodeGlowBlurRadius`, `NodeGlowOpacity` |
| Node accent stripe | `ShowNodeAccentStripe`, `NodeAccentStripeWidth`, `NodeAccentStripeSource` (`GraphExportStripeSource`: `BorderColour`, `FillColour`, `Custom`), `NodeAccentStripeBrush` |
| Edge label | `EdgeLabelFontWeight` |
| Edge glow | `ShowEdgeGlow`, `EdgeGlowBrush`, `EdgeGlowBlurRadius`, `EdgeGlowOpacity` |
| Edge label chip | `ShowEdgeLabelChip`, `EdgeLabelChipBrush`, `EdgeLabelChipBorderBrush`, `EdgeLabelChipBorderThickness`, `EdgeLabelChipCornerRadius`, `EdgeLabelChipPaddingX`, `EdgeLabelChipPaddingY`, `EdgeLabelChipTextBrush` |

`GraphVectorExportStyle.Default` reproduces the original rounded-rectangle appearance. Thus, the vector export of a consumer that sets nothing does not change. The export uses the colors, fonts, dash styles and arrowheads that the node and edge data carry. This type adds only what the imperative renderer cannot infer.

> The node **glow** becomes a blur filter in SVG and PDF. This softens the crisp vector output a little. It is the one trade-off against the flat vector appearance. Everything else stays sharp.

### Select a mode, and connect copy and save

**The mode that the built-in copy and save use** is the `ExportMode` of the control. The default is `Vector`.

**Context menu entries.** Copy and Save each offer the sub-items *Vector* and *High Fidelity*. Hide the item that you do not want:

```xml
<g:InteractiveGraphView DataContext="{Binding Interactive}"
                        ExportMode="Raster"
                        ShowVectorExportOptions="True"
                        ShowRasterExportOptions="True"/>
```

**Copy in source code** (all of the graph, cropped like the saved image):

```csharp
await graphView.CopyImageAsync(GraphExportMode.Raster);  // or omit the arg to use ExportMode
```

**Save in source code, or with a binding.** The view-model has commands for this. They call your `IGraphHost.PickSaveFileAsync` and then write by file extension:

- `SaveGraphImageFileCommand` saves in the default mode.
- `SaveGraphImageWithModeCommand` saves in an explicit `GraphExportMode`. Give the mode as the command parameter.

```xml
<Button Content="Save (vector)"
        Command="{Binding Interactive.SaveGraphImageWithModeCommand}"
        CommandParameter="{x:Static g:GraphExportMode.Vector}"/>
```

**To a stream, with no control on the screen.** You can write an image with no control on the screen, for example in a command line tool or a service. The fixed-layout source builds directly from the diagram. Thus, no interactive surface is necessary:

```csharp
await using FileStream stream = File.Create("graph.svg");
await interactive.WriteImageAsync(
    stream,
    GraphFileFormat.Svg,
    GraphImageSource.FixedLayout,
    FixedLayoutGraphType.Arrow);   // or .Vertex
```

`WriteImageAsync` writes the `GraphFileFormat` that you give it. `Png`, `Jpeg`, `Pdf` and `Svg` make images. `GraphML` and `GraphViz` (Dot) make data. The caller owns the stream, and the method leaves it open. The method throws an exception to the caller. It does not send the failure to `IGraphHost.ReportErrorAsync`.

`GraphImageSource.InteractiveCanvas` exports the current arrangement after dragging. `GraphImageSource.FixedLayout` exports the default MSAGL layout.

> The control implements `IGraphImageProvider` and registers itself on the view-model. Thus, the save path of the view-model can render through your templates and mode. But **only a SkiaSharp picture crosses that seam**. No control or template object reaches the view-model. If the view-model has no control, for example in a host that has no UI, Save uses the vector renderer.

---

## Persistence of the arrangement

You can save and restore the node drags and the routing mode. The members for this are on the concrete `InteractiveGraphViewModel`. Hold the concrete type in your host, and expose `IInteractiveGraph` to the UI:

- `IReadOnlyList<GraphNodePosition> GetNodeLayout()` gives the current arrangement (in layout space).
- `void SeedNodeLayout(IReadOnlyList<GraphNodePosition>)` puts an overlay by node `Id` on the next build, on a best-effort basis. The library drops ids that no longer exist. Nodes with no seed keep the new layout.
- `bool HasManualLayout` is true after the user drags a node. Thus, you save the live arrangement and not a seed that went through a round trip.
- `event EventHandler LayoutChanged` shows that the end of a drag or a reset changed the arrangement. Seeding does *not* raise it. Capture the arrangement here for persistence.
- `void ApplyEdgeRoutingMode(GraphEdgeRoutingMode)` restores a saved routing mode.
- `void ResetView()` removes the saved zoom and pan. Then the next graph frames itself from the start, for example when a project closes. The control keeps the viewport transform (`ViewZoom`, `ViewPanX`, `ViewPanY` and `HasViewState`) when it re-materializes, with no extra work from you.

---

## Built-in interactions

The control provides these items with no extra work:

- **Drag** nodes. The workspace grows, and thus the control does not cut off a dragged node.
- **Click** a node to highlight it, its edges and its neighbors. Everything else dims. Click empty space to remove the highlight.
- **Pan** (drag empty space) and **zoom** (mouse wheel or the slider) with no limits.
- **Context menu:** The menu has the edge routing modes, *Fit to View*, *Reset Layout*, *Copy Image* and *Save As…*. Copy and Save each offer Vector and High Fidelity. When the configuration supports it, the menu also has the toggle for the names.
- The control frames the graph when it loads. It keeps the pan and zoom of the user through each new layout.

---

## Threading and known problems

- **The layout runs off the UI thread.** Observe `IGraphHost.RebuildRequested` on a background scheduler, for example `TaskPoolScheduler.Default`. Then the MSAGL pass does not block the UI. The view-model itself moves the results back to the UI thread.
- **Exports render on the UI thread.** The rasterization of the templates must run on it. The built-in copy and save paths do this for you.
- **Dispatcher.** The view-model reaches the UI thread through an `IGraphDispatcher`. By default, this is `AvaloniaGraphDispatcher` (`Dispatcher.UIThread`).
- **A host that has no UI thread** passes `InlineGraphDispatcher` instead. Examples are an automated export and a server. This dispatcher runs the work at the point of the call. Nothing pumps a dispatcher there. Thus, work that you post to a dispatcher does not run, and it holds the graph for the life of the process. A refresh that is in progress when you dispose the view-model does nothing.
- **Use immutable brushes only.** Avalonia ties a mutable brush (an `AvaloniaObject`) to the dispatcher of the thread that creates it. The compositor verifies this ownership the first time that it draws the brush. A brush that you create off the UI thread causes a crash of the render loop with the message "The calling thread cannot access this object because a different thread owns it". Each brush that the library creates is an `ImmutableSolidColorBrush`. Also supply immutable brushes in your `GraphAppearance` and `GraphVectorExportStyle` overrides. This matters because a thread other than the UI thread can make your host view-model and its appearance, for example behind a splash screen.
- **Use `FontFamily` and not `string`** for all font-family properties (refer to the appearance note above).
- **Dispose** the `InteractiveGraphViewModel` and your `IGraphHost` when your own view-model, which owns them, ends.
- Set `IsHitTestVisible="False"` on the bodies of custom `NodeTemplate` and `EdgeTemplate` where this is applicable. The control owns the pointer input, the drag and the tooltip.
