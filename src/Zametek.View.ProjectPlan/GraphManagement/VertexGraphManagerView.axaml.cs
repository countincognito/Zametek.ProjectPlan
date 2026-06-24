using Avalonia.Controls;

namespace Zametek.View.ProjectPlan
{
    // Thin host for the reusable InteractiveGraphView control. All interactive behaviour lives in
    // the Zametek.Graphs.ProjectPlan library; this view only supplies the docking-tool shell and the
    // data context (VertexGraphManagerViewModel, which exposes the InteractiveGraphViewModel as Interactive).
    public partial class VertexGraphManagerView
        : UserControl
    {
        public VertexGraphManagerView()
        {
            InitializeComponent();
        }
    }
}
