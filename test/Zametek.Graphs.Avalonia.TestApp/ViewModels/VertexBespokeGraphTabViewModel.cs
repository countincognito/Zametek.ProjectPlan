using Avalonia.Media;
using Avalonia.Media.Immutable;
using Zametek.Graphs.Avalonia;
using Zametek.Graphs.Avalonia.TestApp.Graphs;

namespace Zametek.Graphs.Avalonia.TestApp.ViewModels
{
    // Tab 4: the same activity-on-node data, re-skinned into a "material card" look, distinct from the
    // arrow tab's neon theme. The card template (in the paired VertexBespokeGraphTabView) keeps the
    // data-driven fill and border colours (so critical vs. non-critical activities still read), and adds a
    // rounded body, a drop shadow and a coloured accent stripe. The label font is kept monospace so the
    // three-line ES/EF · name · LS/LF box stays column-aligned.
    public sealed class VertexBespokeGraphTabViewModel
        : GraphTabViewModelBase
    {
        public VertexBespokeGraphTabViewModel(GraphTheme initialTheme)
            : base(
                title: @"Vertex · bespoke",
                buildDiagram: _ => SampleGraphs.BuildVertex(),
                configuration: GraphConfigurations.Vertex,
                appearance: BespokeAppearance,
                initialTheme: initialTheme,
                suggestedFileName: @"vertex-graph-bespoke.png")
        {
        }

        private static GraphAppearance BespokeAppearance { get; } = GraphAppearance.Default with
        {
            SelectionBrush = new ImmutableSolidColorBrush(Color.Parse(@"#0EA5A5")),
            HighlightStrokeThickness = 3.0,
            NodeCornerRadius = 10.0,
            NodeLabelFontFamily = new FontFamily(@"Consolas"),
            NodeLabelFontSize = 12.0,
            NodeLabelBrush = new ImmutableSolidColorBrush(Color.Parse(@"#1F2937")),
            EdgeDefaultBrush = new ImmutableSolidColorBrush(Color.Parse(@"#94A3B8")),
            NodeDimmedOpacity = 0.2,
            EdgeDimmedOpacity = 0.15,
        };
    }
}
