using Avalonia.Media;

namespace Zametek.Graphs.Avalonia
{
    // The discrete, template-independent styling for the VECTOR export (GraphExportMode.Vector). The
    // vector exporter draws nodes and edges imperatively in SkiaSharp - which is what keeps SVG/PDF crisp -
    // so it cannot reproduce an arbitrary NodeTemplate/EdgeTemplate; instead a consumer describes the
    // silhouette and a couple of decorations here to approximate their bespoke look in vector form. Colours,
    // fonts, dash, arrowheads and corner radius still come from GraphAppearance and the per-node/edge data;
    // this only adds what the imperative renderer could not otherwise infer. GraphVectorExportStyle.Default
    // reproduces the original rounded-rectangle look, so an unset consumer's vector export is unchanged.
    //
    // (For a pixel-exact reproduction of a custom template - gradients, shadows, arbitrary shapes - use
    // GraphExportMode.Raster instead, which renders the real visuals to a bitmap.)
    public record GraphVectorExportStyle
    {
        // The ready-made default (original look). Re-style via `Default with { ... }`.
        public static GraphVectorExportStyle Default { get; } = new();

        // The node silhouette. RoundedRectangle (with GraphAppearance.NodeCornerRadius) is the original look.
        public GraphExportNodeShape NodeShape { get; init; } = GraphExportNodeShape.RoundedRectangle;

        // Optional coloured accent bar down the left edge of the node (best with the rectangular shapes).
        public bool ShowNodeAccentStripe { get; init; }

        public double NodeAccentStripeWidth { get; init; } = 6.0;

        public GraphExportStripeSource NodeAccentStripeSource { get; init; } = GraphExportStripeSource.BorderColour;

        // Used only when NodeAccentStripeSource is Custom.
        public IBrush? NodeAccentStripeBrush { get; init; }

        // Optional filled rounded background drawn behind an edge label (a "chip"), for edges that show one.
        public bool ShowEdgeLabelChip { get; init; }

        public IBrush EdgeLabelChipBrush { get; init; } = new SolidColorBrush(Color.FromArgb(0xCC, 0x1B, 0x2A, 0x4A));

        public double EdgeLabelChipCornerRadius { get; init; } = 8.0;

        // Padding between the label text and the chip edge (horizontal, vertical).
        public double EdgeLabelChipPaddingX { get; init; } = 7.0;

        public double EdgeLabelChipPaddingY { get; init; } = 2.0;

        // When a chip is drawn, the label colour to use on it. If null, the theme-appropriate edge label
        // colour from GraphAppearance is used (which may not read on a dark chip - set this alongside a
        // dark EdgeLabelChipBrush).
        public IBrush? EdgeLabelChipTextBrush { get; init; }
    }
}
