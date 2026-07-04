using Avalonia.Controls;
using Avalonia.Media;
using Zametek.Graphs.Avalonia;

namespace Zametek.Graphs.Avalonia.TestApp.Views
{
    public partial class ArrowBespokeGraphTabView : UserControl
    {
        public ArrowBespokeGraphTabView()
        {
            InitializeComponent();

            // The vector-export silhouette that approximates the on-screen neon look in true vector form:
            // ellipse nodes with the same indigo radial gradient, a soft glow, a heavier border, bold labels
            // and a dark, bordered chip behind each edge label (plus an edge glow). The high-fidelity raster
            // export reproduces the real templates exactly; this is what the crisp VECTOR export draws
            // instead - a GraphVectorExportStyle is supplied precisely because the vector renderer cannot read
            // the arbitrary template, so the on-screen decorations are described here.
            GraphView.VectorExportStyle = GraphVectorExportStyle.Default with
            {
                NodeShape = GraphExportNodeShape.Ellipse,

                // The same radial gradient the on-screen template paints (fixed, not data-driven), honoured
                // as a true vector gradient. The white node labels then read against it.
                NodeFillOverride = new RadialGradientBrush
                {
                    GradientStops =
                    {
                        new GradientStop(Color.Parse(@"#6D8BFF"), 0.0),
                        new GradientStop(Color.Parse(@"#2A3F9D"), 1.0),
                    },
                },

                // The template strokes the node border at 2px (the data thickness is only 1.2), so bump it
                // to match; the data border brush (#33475B) is kept.
                NodeBorderThicknessOverride = 2.0,

                // The template draws the node number bold.
                NodeLabelFontWeight = FontWeight.Bold,

                // The template's DropShadowEffect glow (colour #6D8BFF, blur 16, opacity 0.75).
                ShowNodeGlow = true,
                NodeGlowBrush = new SolidColorBrush(Color.Parse(@"#6D8BFF")),
                NodeGlowBlurRadius = 16.0,
                NodeGlowOpacity = 0.75,

                // The edge's soft glow (template DropShadowEffect colour #3B82F6, blur 7, opacity 0.4).
                ShowEdgeGlow = true,
                EdgeGlowBrush = new SolidColorBrush(Color.Parse(@"#3B82F6")),
                EdgeGlowBlurRadius = 7.0,
                EdgeGlowOpacity = 0.4,

                // The label chip, with the same border the on-screen chip carries.
                ShowEdgeLabelChip = true,
                EdgeLabelChipBrush = new SolidColorBrush(Color.FromArgb(0xEE, 0x1B, 0x2A, 0x4A)),
                EdgeLabelChipBorderBrush = new SolidColorBrush(Color.Parse(@"#3B82F6")),
                EdgeLabelChipBorderThickness = 1.0,
                EdgeLabelChipTextBrush = new SolidColorBrush(Color.Parse(@"#EAF1FB")),
                EdgeLabelChipCornerRadius = 9.0,
            };
        }
    }
}
