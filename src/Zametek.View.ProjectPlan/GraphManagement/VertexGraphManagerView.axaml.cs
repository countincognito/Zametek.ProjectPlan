using Avalonia.Controls;

namespace Zametek.View.ProjectPlan
{
    // Thin host for the reusable InteractiveVertexGraphView control. All interactive behaviour
    // lives in the Zametek.Graphs.ProjectPlan library; this view only supplies the docking-tool
    // shell and the data context (VertexGraphManagerViewModel, which implements
    // IInteractiveVertexGraph).
    public partial class VertexGraphManagerView
        : UserControl
    {
        public VertexGraphManagerView()
        {
            InitializeComponent();
        }
    }
}
