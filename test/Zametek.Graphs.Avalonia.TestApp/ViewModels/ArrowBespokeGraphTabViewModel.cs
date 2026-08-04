using Avalonia.Media;
using Avalonia.Media.Immutable;
using Zametek.Graphs.Avalonia;
using Zametek.Graphs.Avalonia.TestApp.Graphs;

namespace Zametek.Graphs.Avalonia.TestApp.ViewModels
{
    // Tab 3: the same activity-on-arrow data, re-skinned into a "neon token" look. Two levers are used
    // together:
    //   * a bespoke GraphAppearance (below) - drives fonts, label colours, the selection ring, the
    //     arrowheads and the dimming, and also flows into the copied / saved image; and
    //   * a bespoke NodeTemplate / EdgeTemplate declared in the paired ArrowBespokeGraphTabView - draws
    //     the glowing circular nodes and the chip-labelled edges on the interactive canvas.
    // The event nodes are uniform in an arrow graph, so the template gives them a fixed vibrant fill and
    // white labels (hence NodeLabelBrush = white here).
    public sealed class ArrowBespokeGraphTabViewModel
        : GraphTabViewModelBase
    {
        public ArrowBespokeGraphTabViewModel(GraphTheme initialTheme)
            : base(
                title: @"Arrow · bespoke",
                buildDiagram: SampleGraphs.BuildArrow,
                configuration: GraphConfigurations.Arrow,
                appearance: BespokeAppearance,
                initialTheme: initialTheme,
                suggestedFileName: @"arrow-graph-bespoke.png")
        {
        }

        private static GraphAppearance BespokeAppearance { get; } = GraphAppearance.Default with
        {
            SelectionBrush = new ImmutableSolidColorBrush(Color.Parse(@"#FFB703")),
            HighlightStrokeThickness = 3.0,
            NodeCornerRadius = 14.0,
            NodeLabelFontFamily = new FontFamily(@"Segoe UI"),
            NodeLabelFontSize = 14.0,
            NodeLabelBrush = new ImmutableSolidColorBrush(Colors.White),
            EdgeLabelFontFamily = new FontFamily(@"Segoe UI"),
            EdgeLabelFontSize = 12.0,
            EdgeLightLabelBrush = new ImmutableSolidColorBrush(Color.Parse(@"#EAF1FB")),
            EdgeDarkLabelBrush = new ImmutableSolidColorBrush(Color.Parse(@"#EAF1FB")),
            ArrowLength = 13.0,
            ArrowHalfWidth = 6.5,
            NodeDimmedOpacity = 0.12,
            EdgeDimmedOpacity = 0.1,
        };
    }
}
