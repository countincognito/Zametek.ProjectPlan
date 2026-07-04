using Avalonia.Media;

namespace Zametek.Graphs.Avalonia
{
    // The discrete, template-independent styling for the VECTOR export (GraphExportMode.Vector). The
    // vector exporter draws nodes and edges imperatively in SkiaSharp - which is what keeps SVG/PDF crisp -
    // so it cannot reproduce an arbitrary NodeTemplate/EdgeTemplate; instead a consumer describes the
    // silhouette and a handful of decorations here to approximate their bespoke look in vector form. Colours,
    // fonts, dash, arrowheads and corner radius still come from GraphAppearance and the per-node/edge data;
    // this only adds what the imperative renderer could not otherwise infer. GraphVectorExportStyle.Default
    // reproduces the original rounded-rectangle look, so an unset consumer's vector export is unchanged.
    //
    // (For a pixel-exact reproduction of a custom template use GraphExportMode.Raster instead, which renders
    // the real visuals to a bitmap. The knobs here bring the vector form close - gradient fills, a soft glow,
    // heavier borders and bold labels - while staying true vector.)
    public record GraphVectorExportStyle
    {
        // The ready-made default (original look). Re-style via `Default with { ... }`.
        public static GraphVectorExportStyle Default { get; } = new();

        // The node silhouette. RoundedRectangle (with GraphAppearance.NodeCornerRadius) is the original look.
        public GraphExportNodeShape NodeShape { get; init; } = GraphExportNodeShape.RoundedRectangle;

        // Optional node fill for the vector export. By default the vector export fills each node with its
        // own data-driven FillBrush (matching a template that binds FillBrush). Set this when the on-screen
        // template paints a fill unrelated to the data (e.g. a fixed gradient) that the data fill would not
        // approximate - the vector export then fills every node with this brush instead. A gradient brush
        // (LinearGradientBrush / RadialGradientBrush) is honoured as a true vector gradient; any other brush
        // is read as a solid colour.
        public IBrush? NodeFillOverride { get; init; }

        // Optional node border for the vector export. By default the vector export strokes each node with its
        // own data-driven BorderBrush/BorderThickness (matching a template that binds them). Set these when
        // the template's stroke is heavier or a different colour than the data would give (e.g. a fixed 2px
        // stroke). A null brush keeps the data BorderBrush; a null thickness keeps the data BorderThickness.
        public IBrush? NodeBorderOverride { get; init; }

        public double? NodeBorderThicknessOverride { get; init; }

        // Node label weight for the vector export (templates often draw the label bold). Normal is the
        // original look.
        public FontWeight NodeLabelFontWeight { get; init; } = FontWeight.Normal;

        // Optional soft outer glow (a blurred, translucent halo) around each node, approximating a template's
        // DropShadowEffect. In SVG this becomes a blur, so it softens the otherwise-crisp output a little.
        public bool ShowNodeGlow { get; init; }

        // The glow colour. If null, the node's fill colour is used.
        public IBrush? NodeGlowBrush { get; init; }

        // The glow spread; larger is softer / wider. Interpreted like a DropShadowEffect BlurRadius.
        public double NodeGlowBlurRadius { get; init; } = 12.0;

        // The glow strength, 0..1.
        public double NodeGlowOpacity { get; init; } = 0.6;

        // Optional coloured accent bar down the left edge of the node (best with the rectangular shapes).
        public bool ShowNodeAccentStripe { get; init; }

        public double NodeAccentStripeWidth { get; init; } = 6.0;

        public GraphExportStripeSource NodeAccentStripeSource { get; init; } = GraphExportStripeSource.BorderColour;

        // Used only when NodeAccentStripeSource is Custom.
        public IBrush? NodeAccentStripeBrush { get; init; }

        // Edge label weight for the vector export. Normal is the original look.
        public FontWeight EdgeLabelFontWeight { get; init; } = FontWeight.Normal;

        // Optional soft glow along each edge line, approximating a template's DropShadowEffect on the edge.
        public bool ShowEdgeGlow { get; init; }

        // The glow colour. If null, the edge's stroke colour is used.
        public IBrush? EdgeGlowBrush { get; init; }

        public double EdgeGlowBlurRadius { get; init; } = 6.0;

        public double EdgeGlowOpacity { get; init; } = 0.4;

        // Optional filled rounded background drawn behind an edge label (a "chip"), for edges that show one.
        public bool ShowEdgeLabelChip { get; init; }

        public IBrush EdgeLabelChipBrush { get; init; } = new SolidColorBrush(Color.FromArgb(0xCC, 0x1B, 0x2A, 0x4A));

        // Optional border stroked around the chip. Drawn only when the brush is set (the default chip has no
        // border, preserving the original look).
        public IBrush? EdgeLabelChipBorderBrush { get; init; }

        public double EdgeLabelChipBorderThickness { get; init; } = 1.0;

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
