using Avalonia.Controls;
using Zametek.Graphs.Avalonia;

namespace Zametek.Graphs.Avalonia.TestApp.Views
{
    public partial class VertexBespokeGraphTabView : UserControl
    {
        public VertexBespokeGraphTabView()
        {
            InitializeComponent();

            // The vector-export silhouette that approximates the on-screen material-card look: a rounded
            // card with a coloured accent stripe taken from the node's (data-driven) border colour, so
            // critical vs. non-critical activities still read in the crisp VECTOR export. The high-fidelity
            // raster export reproduces the real card templates (shadow, stripe) exactly.
            GraphView.VectorExportStyle = GraphVectorExportStyle.Default with
            {
                NodeShape = GraphExportNodeShape.RoundedRectangle,
                ShowNodeAccentStripe = true,
                NodeAccentStripeWidth = 6.0,
                NodeAccentStripeSource = GraphExportStripeSource.BorderColour,
            };
        }
    }
}
