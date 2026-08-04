using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Zametek.Graphs.Avalonia
{
    // The interactive graph's themable appearance: the brushes, fonts, opacities and shape metrics used
    // to draw nodes and edges on the interactive canvas. Passed to InteractiveGraphViewModel (like the
    // per-graph GraphConfiguration, which by contrast tunes the MSAGL layout); the node/edge view-models
    // read their presentation from it, and the default node/edge templates bind the rest. A consumer of
    // the library re-skins the graph by passing an instance (typically `GraphAppearance.Default with
    // { ... }`); passing nothing keeps the original look. Pure routing/geometry maths (arrowhead tangent
    // span, axis-flip hysteresis, edge label lift, the wide edge hit area) are deliberately NOT here -
    // they shape behaviour, not theme.
    //
    // THREADING: every brush below is an ImmutableSolidColorBrush, never a mutable SolidColorBrush.
    // A mutable brush is an AvaloniaObject, and Avalonia ties an AvaloniaObject to the dispatcher of
    // the thread that constructs it - the compositor then verifies that ownership the first time the
    // brush is drawn. This record is constructed on whatever thread first touches it (an application
    // that builds its view-model graph behind a splash screen constructs Default on a background
    // thread), so a mutable default would be foreign-owned and crash the UI thread's render loop with
    // "The calling thread cannot access this object because a different thread owns it". Immutable
    // brushes carry no thread ownership, and as a bonus can be read off the UI thread (the exporters
    // read brush colours). Consumers re-skinning via `Default with { ... }` should supply immutable
    // brushes for the same reason.
    public record GraphAppearance
    {
        // The ready-made default appearance (the original hard-coded look). Re-skin via `Default with { ... }`.
        public static GraphAppearance Default { get; } = new();

        // Selection / highlight, shared by the node selection ring and the highlighted-edge stroke.
        public IBrush SelectionBrush { get; init; } = new ImmutableSolidColorBrush(Color.Parse(@"#0078D4"));

        public double HighlightStrokeThickness { get; init; } = 2.5;

        // Nodes.
        public IBrush NodeFillFallbackBrush { get; init; } = new ImmutableSolidColorBrush(Colors.LightGray);

        public IBrush NodeBorderFallbackBrush { get; init; } = new ImmutableSolidColorBrush(Colors.Black);

        public double NodeCornerRadius { get; init; } = 3.0;

        public double DefaultNodeBorderThickness { get; init; } = 1.0;

        public double NodeDimmedOpacity { get; init; } = 0.25;

        public FontFamily NodeLabelFontFamily { get; init; } = new(@"Consolas");

        public double NodeLabelFontSize { get; init; } = 11.0;

        public IBrush NodeLabelBrush { get; init; } = new ImmutableSolidColorBrush(Colors.Black);

        // Edges.
        public IBrush EdgeDefaultBrush { get; init; } = new ImmutableSolidColorBrush(Colors.Gray);

        public double DefaultEdgeStrokeThickness { get; init; } = 1.0;

        public double EdgeDimmedOpacity { get; init; } = 0.15;

        public IBrush EdgeLightLabelBrush { get; init; } = new ImmutableSolidColorBrush(Colors.Black);

        public IBrush EdgeDarkLabelBrush { get; init; } = new ImmutableSolidColorBrush(Colors.White);

        public FontFamily EdgeLabelFontFamily { get; init; } = new(@"Consolas");

        public double EdgeLabelFontSize { get; init; } = 12.0;

        public double ArrowLength { get; init; } = 9.0;

        public double ArrowHalfWidth { get; init; } = 4.5;

        // The dash pattern applied to dashed (critical / dummy) node borders and edges.
        public IReadOnlyList<double> DashPattern { get; init; } = [3.0, 2.0];
    }
}
