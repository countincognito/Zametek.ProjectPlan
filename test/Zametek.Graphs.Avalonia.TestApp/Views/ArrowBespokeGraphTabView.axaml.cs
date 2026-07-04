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

            // The vector-export silhouette that approximates the on-screen neon look: ellipse nodes and a
            // dark chip behind each edge label. The high-fidelity raster export reproduces the real
            // templates exactly (glow, gradient); this is what the crisp VECTOR export draws instead - a
            // GraphVectorExportStyle is supplied precisely because the vector renderer cannot read the
            // arbitrary template.
            GraphView.VectorExportStyle = GraphVectorExportStyle.Default with
            {
                NodeShape = GraphExportNodeShape.Ellipse,
                // The on-screen nodes use a fixed indigo radial gradient (not the data fill), which the
                // vector renderer cannot read, so approximate it with a solid indigo. The white node labels
                // (from the bespoke appearance) then read against it.
                NodeFillOverride = new SolidColorBrush(Color.Parse(@"#2E4194")),
                ShowEdgeLabelChip = true,
                EdgeLabelChipBrush = new SolidColorBrush(Color.FromArgb(0xEE, 0x1B, 0x2A, 0x4A)),
                EdgeLabelChipTextBrush = new SolidColorBrush(Color.Parse(@"#EAF1FB")),
                EdgeLabelChipCornerRadius = 9.0,
            };
        }
    }
}
