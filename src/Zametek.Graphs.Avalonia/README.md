# Zametek.Graphs.Avalonia

A reusable, embeddable **interactive graph control** for [Avalonia](https://avaloniaui.net/). One
generic control family draws directed graphs - activity‑on‑**arrow** and activity‑on‑**vertex**
alike - with dragging, click‑to‑select highlighting, hover tooltips, unbounded pan/zoom, automatic
[MSAGL](https://github.com/microsoft/automatic-graph-layout) layout, and image/vector export (PNG,
JPEG, PDF, SVG, plus GraphML and GraphViz).

Everything application‑specific (your domain graph, settings, save/error dialogs) is supplied through
one thin interface, so a consumer writes an adapter and a single wiring line - not the interactive,
layout, or export machinery.

- **Target framework:** `net10.0`
- **Key dependencies:** Avalonia 12, ReactiveUI.Avalonia 12, SkiaSharp (via Svg.Skia), MSAGL
  (`AutomaticGraphLayout.Drawing`), `Xaml.Behaviors.Avalonia`.
- **Root namespace / XAML namespace:** `Zametek.Graphs.Avalonia`
- Compiled bindings are on by default (`AvaloniaUseCompiledBindingsByDefault=true`).

---

## Contents

1. [How it works](#how-it-works)
2. [Quick start](#quick-start)
3. [Data formats](#data-formats)
4. [Presentation styling](#presentation-styling)
   - [Re‑skinning with `GraphAppearance`](#re-skinning-with-graphappearance)
   - [Custom node / edge templates](#custom-node--edge-templates)
5. [Export & export styling](#export--export-styling)
   - [Vector vs. high‑fidelity (raster)](#vector-vs-high-fidelity-raster)
   - [`GraphVectorExportStyle`](#graphvectorexportstyle)
   - [Choosing modes & wiring copy/save](#choosing-modes--wiring-copysave)
6. [Persisting the arrangement](#persisting-the-arrangement)
7. [Built‑in interactions](#built-in-interactions)
8. [Threading & gotchas](#threading--gotchas)

---

## How it works

The library is MVVM. You provide data and services; the library owns the view‑model, the layout, and
the view.

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

Key types:

| Type | Role | You supply? |
|---|---|---|
| **`InteractiveGraphView`** | The Avalonia `UserControl` you place in XAML. | - (use it) |
| **`IInteractiveGraph`** | The contract the control binds to. | - |
| **`InteractiveGraphViewModel`** | The reusable implementation of `IInteractiveGraph`. Runs layout, holds the interactive node/edge view‑models, drives export. | construct it |
| **`IGraphHost`** | Thin adapter to *your* app: theme, data, rebuild signal, save/error dialogs. | **implement it** |
| **`IGraphLayoutEngine`** → `MsaglGraphLayoutEngine` | Runs the MSAGL layout / SVG. | use the default |
| **`IGraphSerializer`** → `GraphSerializer` | GraphML / GraphViz output. | use the default |
| **`GraphConfiguration`** (+ `GraphConfigurations.Arrow` / `.Vertex`) | Per‑graph layout tuning (node/label sizes, routing). | pick a preset |
| **`GraphAppearance`** | Themable presentation (brushes, fonts, opacities). *Optional.* | optional re‑skin |

The same `InteractiveGraphViewModel` serves both arrow and vertex graphs - the difference is just the
`GraphConfiguration` preset and (optionally) a `GraphAppearance` / templates.

---

## Quick start

### 1. Implement `IGraphHost`

This is the only interface you must write. It adapts your application to the library:

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

The library rebuilds the graph **every time `RebuildRequested` fires**. You own throttling/scheduling
(the example observes on the task pool). The `BehaviorSubject` seed produces the initial layout.

### 2. Construct the view‑model

```csharp
var host = new MyGraphHost();

var interactive = new InteractiveGraphViewModel(
    host,
    new MsaglGraphLayoutEngine(),   // default layout engine
    new GraphSerializer(),          // default GraphML/GraphViz serializer
    GraphConfigurations.Arrow);     // or .Vertex, or your own GraphConfiguration
    // optional 5th arg: a GraphAppearance to re-skin (see below)

// Expose it to your view as IInteractiveGraph:
public IInteractiveGraph Interactive => interactive;
```

`InteractiveGraphViewModel` is `IDisposable` - dispose it (and your host) with the owning view‑model.

### 3. Place the control

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:g="using:Zametek.Graphs.Avalonia"
             x:Class="MyApp.Views.MyGraphView">

    <!-- Default look. No templates, no appearance = the built-in presentation. -->
    <g:InteractiveGraphView DataContext="{Binding Interactive}"/>

</UserControl>
```

That's the whole integration. Drag, select, pan/zoom, the context menu (routing modes, fit‑to‑view,
reset layout, copy/save) and export all come from the control.

---

## Data formats

You never hand the library coordinates or Avalonia objects - you produce a **`DiagramGraphModel`**: a
flat, coordinate‑free description of *what* to draw, with presentation already resolved to hex colours
and simple enums. MSAGL computes the positions.

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

**`DiagramNodeModel`** - `Id`, `X`, `Y`, `Width`, `Height`, `FillColorHexCode?`, `BorderColorHexCode?`,
`BorderDashStyle` (`GraphDashStyle`), `BorderThickness`, `Text?`, `Name?`, `Tooltip?`.
*(`X`/`Y` are only meaningful in the GraphML export; on‑screen positions come from the layout pass.)*

**`DiagramEdgeModel`** - `Id`, `Name?`, `SourceId`, `TargetId`, `DashStyle` (`GraphDashStyle`),
`ForegroundColorHexCode?`, `StrokeThickness`, `Label?`, `ShowLabel`, `Tooltip?`.

**Enums / helper types**

- `GraphDashStyle` - `Normal`, `Dashed`.
- `GraphTheme` - `Light`, `Dark`. Map your own theme onto this in `IGraphHost.Theme`; the canvas
  background and export background follow it.
- Colours are hex strings parsed by Avalonia's `Color.Parse` (`#RRGGBB` or `#AARRGGBB`). `null` falls
  back to the `GraphAppearance` fallback brushes.

### Choosing / tuning the configuration

Pass a preset:

- **`GraphConfigurations.Arrow`** - event nodes with single‑line labels and labelled edges; offers the
  *show names* toggle.
- **`GraphConfigurations.Vertex`** - activity nodes with a three‑line label box, unlabelled edges.

Or build your own `GraphConfiguration` (a `record`) to change node/label box sizes, font, the default
`EdgeRoutingMode`, and the `InteractiveLayoutScalingFactor`. These values tune the **MSAGL layout**
(they size the boxes MSAGL lays out, which also sets the interactive node sizes), so treat them as a
matched set - start from a preset and adjust with `with { ... }`.

### Edge routing

`GraphEdgeRoutingMode`: `None`, `Spline`, `SplineBundling`, `StraightLine`, `SugiyamaSplines`,
`Rectilinear`, `RectilinearToCenter`. The presets use `SugiyamaSplines` (arrow) / `Spline` (vertex).
The user can switch modes from the context menu; the fixed‑layout SVG export honours each fully, while
the live canvas draws a fast local approximation.

---

## Presentation styling

Two independent levers, use either or both:

- **`GraphAppearance`** - re‑skin the *built‑in* templates (brushes, fonts, opacities, metrics) without
  writing XAML.
- **`NodeTemplate` / `EdgeTemplate`** - replace the drawn node/edge **body** entirely with your own
  Avalonia visuals.

### Re‑skinning with `GraphAppearance`

Pass a `GraphAppearance` to the view‑model constructor. Start from `Default` and override what you need:

```csharp
using Avalonia.Media;
using Avalonia.Media.Immutable;

var appearance = GraphAppearance.Default with
{
    SelectionBrush     = new ImmutableSolidColorBrush(Color.Parse("#FF7A00")),   // NOTE: immutable, not
    NodeLabelBrush     = new ImmutableSolidColorBrush(Colors.White),             // SolidColorBrush - see
    NodeLabelFontFamily = new FontFamily("Cascadia Code"),                       // Threading & gotchas
    NodeCornerRadius   = 6.0,
    EdgeDefaultBrush   = new ImmutableSolidColorBrush(Color.Parse("#667085")),
    DashPattern        = new double[] { 4.0, 2.0 },
};

var interactive = new InteractiveGraphViewModel(
    host, new MsaglGraphLayoutEngine(), new GraphSerializer(),
    GraphConfigurations.Vertex, appearance);
```

`GraphAppearance` members (all `init`, defaults reproduce the original look):

| Group | Members |
|---|---|
| Selection | `SelectionBrush`, `HighlightStrokeThickness` |
| Nodes | `NodeFillFallbackBrush`, `NodeBorderFallbackBrush`, `NodeCornerRadius`, `DefaultNodeBorderThickness`, `NodeDimmedOpacity`, `NodeLabelFontFamily`, `NodeLabelFontSize`, `NodeLabelBrush` |
| Edges | `EdgeDefaultBrush`, `DefaultEdgeStrokeThickness`, `EdgeDimmedOpacity`, `EdgeLightLabelBrush`, `EdgeDarkLabelBrush`, `EdgeLabelFontFamily`, `EdgeLabelFontSize`, `ArrowLength`, `ArrowHalfWidth`, `DashPattern` |

> ⚠️ **Font‑family properties must be typed `Avalonia.Media.FontFamily`, not `string`.** With compiled
> bindings, binding a `string` to `FontFamily` throws at runtime (and silently falls back to the
> default font). This applies to `GraphAppearance` and the template contract below.

### Custom node / edge templates

Set `NodeTemplate` and/or `EdgeTemplate` on the control to replace the drawn **body**. The control keeps
ownership of positioning, dragging, the selection ring, dimming, the wide invisible hit area and the
tooltip - your template only draws the visible node/edge. Bind against the stable contract interfaces
(`x:DataType`), never the concrete view‑model:

- Node body → **`IGraphNodeViewModel`**
- Edge body → **`IGraphEdgeViewModel`**

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

**Contract you can bind to:**

- `IGraphNodeViewModel` - `Id`, `Width`, `Height`, `Label`, `Tooltip`, `FillBrush`, `BorderBrush`,
  `BorderThickness`, `StrokeDashArray`, `CornerRadius`, `LabelFontFamily`, `LabelFontSize`, `LabelBrush`,
  `SelectionBrush`, `IsSelected`, `IsDimmed`, `NodeOpacity` (`X`/`Y` are read‑write but owned by the
  control's drag/positioning - don't bind them for layout).
- `IGraphEdgeViewModel` - `Id`, `EdgeGeometry`, `Stroke`, `StrokeThickness`, `StrokeDashArray`,
  `EdgeOpacity`, `ArrowPoints`, `Label`, `ShowLabel`, `LabelBrush`, `LabelFontFamily`, `LabelFontSize`,
  `LabelX`, `LabelY`, `Tooltip`.

The library ships a `g:HalfNegativeConverter` (shifts a canvas‑positioned control by minus half its own
size) for centring an edge label/chip on its `LabelX`/`LabelY` anchor. Anything you leave unset keeps
the built‑in body.

---

## Export & export styling

The control can copy to the clipboard and save to PNG, JPEG, PDF, SVG (images) and GraphML / `.dot`
(data). Image export has **two modes**.

### Vector vs. high‑fidelity (raster)

`GraphExportMode`:

| Mode | What it draws | Best for |
|---|---|---|
| **`Vector`** (default) | Crisp shapes drawn imperatively in SkiaSharp, described by a `GraphVectorExportStyle`. True vector in SVG/PDF. **Does not** read your custom templates. | scalable, razor‑sharp output; approximating a custom look in vector form |
| **`Raster`** ("High Fidelity") | The **real** `NodeTemplate` / `EdgeTemplate` rendered to a bitmap (2× supersampled), embedded into SVG/PDF. Reproduces gradients, shadows, arbitrary shapes exactly. | pixel‑exact reproduction of a bespoke template |

Because the vector renderer can't read an arbitrary template, you *describe* the look you want with a
`GraphVectorExportStyle`; the raster path needs no configuration (it uses the templates directly).

### `GraphVectorExportStyle`

An immutable record (start from `Default`). Set it on the control via `VectorExportStyle`:

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

Members, grouped:

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

`GraphVectorExportStyle.Default` reproduces the original rounded‑rectangle look, so an unset consumer's
vector export is unchanged. Colours/fonts/dash/arrowheads that the node/edge data already carry flow
through automatically - this record only adds what the imperative renderer cannot otherwise infer.

> The node **glow** becomes a blur filter in SVG/PDF, which softens the otherwise‑crisp vector output a
> little - the one trade‑off versus the flat vector look. Everything else stays sharp.

### Choosing modes & wiring copy/save

**Which mode the built‑in copy/save use** is the control's `ExportMode` (default `Vector`).

**Context‑menu entries** - Copy/Save each offer *Vector* and *High Fidelity* sub‑items. Hide whichever
you don't want:

```xml
<g:InteractiveGraphView DataContext="{Binding Interactive}"
                        ExportMode="Raster"
                        ShowVectorExportOptions="True"
                        ShowRasterExportOptions="True"/>
```

**Programmatic copy** (whole graph, cropped like the saved image):

```csharp
await graphView.CopyImageAsync(GraphExportMode.Raster);  // or omit the arg to use ExportMode
```

**Programmatic / bound save** - the view‑model exposes commands (they call your
`IGraphHost.PickSaveFileAsync`, then write by file extension):

- `SaveGraphImageFileCommand` - save at the default mode.
- `SaveGraphImageWithModeCommand` - save at an explicit `GraphExportMode` (pass it as the command
  parameter).

```xml
<Button Content="Save (vector)"
        Command="{Binding Interactive.SaveGraphImageWithModeCommand}"
        CommandParameter="{x:Static g:GraphExportMode.Vector}"/>
```

**Headless / known path** - save without any on‑screen control (e.g. a CLI). The fixed‑layout source
builds straight from the diagram, so no interactive surface is needed:

```csharp
await interactive.SaveImageAsync(
    "graph.svg",
    GraphImageSource.FixedLayout,
    FixedLayoutGraphType.Arrow);   // or .Vertex
```

`SaveImageAsync` chooses the writer from the file extension: `.png` / `.jpeg` / `.pdf` / `.svg` produce
images; `.graphml` / `.dot` produce data. `GraphImageSource.InteractiveCanvas` exports the current
dragged arrangement; `GraphImageSource.FixedLayout` exports the default MSAGL layout.

> Under the hood the control implements `IGraphImageProvider` and registers itself on the view‑model, so
> the view‑model's Save path can render through your templates/mode - but **only a SkiaSharp picture
> crosses that seam** (no view or template object reaches the view‑model). With no control attached
> (headless), Save falls back to the vector renderer.

---

## Persisting the arrangement

Node drags and the routing mode can be saved and restored. These live on the concrete
`InteractiveGraphViewModel` (hold the concrete type in your host, expose `IInteractiveGraph` to the view):

- `IReadOnlyList<GraphNodePosition> GetNodeLayout()` - the current arrangement (layout space).
- `void SeedNodeLayout(IReadOnlyList<GraphNodePosition>)` - best‑effort overlay by node `Id` applied on
  the next build (ids no longer present are dropped; unseeded nodes keep the fresh layout).
- `bool HasManualLayout` - true once the user has actually dragged (so you save the live arrangement,
  not a round‑tripped seed).
- `event EventHandler LayoutChanged` - a drag‑end or reset changed the arrangement (seeding does *not*
  raise it) - capture for persistence here.
- `void ApplyEdgeRoutingMode(GraphEdgeRoutingMode)` - restore a saved routing mode.
- `void ResetView()` - drop the persisted zoom/pan so the next graph re‑frames from scratch (e.g. on
  project close). The viewport transform (`ViewZoom`/`ViewPanX`/`ViewPanY`/`HasViewState`) is persisted
  by the control across re‑materialisation automatically.

---

## Built‑in interactions

Provided by the control with no extra work:

- **Drag** nodes; the workspace grows so dragged nodes never clip.
- **Click** a node to highlight it, its edges and neighbours (everything else dims); click empty space
  to clear.
- **Pan** (drag empty space) and **zoom** (mouse wheel or the slider), unbounded.
- **Context menu:** edge‑routing modes, *Fit to View*, *Reset Layout*, *Copy Image* (Vector / High
  Fidelity), *Save As…* (Vector / High Fidelity), and the *show names* toggle when the configuration
  supports it.
- Automatic re‑framing on load; the user's pan/zoom is preserved across re‑layouts.

---

## Threading & gotchas

- **Layout runs off the UI thread.** Observe `IGraphHost.RebuildRequested` on a background scheduler
  (e.g. `TaskPoolScheduler.Default`) so the MSAGL pass never blocks the UI. The view‑model marshals the
  results back to the UI thread itself.
- **Exports render on the UI thread.** Rasterising the real templates must run on it - the built‑in
  copy/save paths already do (`Dispatcher.UIThread`).
- **Immutable brushes only.** Avalonia ties a mutable brush (an `AvaloniaObject`) to the dispatcher of
  the thread that creates it, and the compositor verifies that ownership the first time the brush is
  drawn - a brush created off the UI thread crashes the render loop with "The calling thread cannot
  access this object because a different thread owns it". Every brush the library creates is an
  `ImmutableSolidColorBrush`; supply immutable brushes in your `GraphAppearance` /
  `GraphVectorExportStyle` overrides too, because your host view‑model - and with it the appearance -
  may well be constructed off the UI thread (e.g. behind a splash screen).
- **`FontFamily`, not `string`** for all font‑family properties (see the appearance note above).
- **Dispose** the `InteractiveGraphViewModel` (and your `IGraphHost`) when the owning view‑model goes
  away.
- Set custom `NodeTemplate` / `EdgeTemplate` bodies to `IsHitTestVisible="False"` where appropriate -
  the control owns the hit area, drag and tooltip.
```
