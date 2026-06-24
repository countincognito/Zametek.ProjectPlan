using Avalonia.Controls;

namespace Zametek.View.ProjectPlan
{
    // Thin host for the reusable InteractiveGraphView control. All interactive behaviour lives in
    // the Zametek.Graphs.Avalonia library; this view only supplies the docking-tool shell and the
    // data context (ArrowGraphManagerViewModel, which exposes the InteractiveGraphViewModel as Interactive).
    public partial class ArrowGraphManagerView
        : UserControl
    {
        public ArrowGraphManagerView()
        {
            InitializeComponent();
        }
    }
}
