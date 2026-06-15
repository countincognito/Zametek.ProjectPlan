using Avalonia.Controls;

namespace Zametek.View.ProjectPlan
{
    // Thin host for the reusable InteractiveArrowGraphView control. All interactive behaviour
    // lives in the Zametek.Graphs.ProjectPlan library; this view only supplies the docking-tool
    // shell and the data context (ArrowGraphManagerViewModel, which implements
    // IInteractiveArrowGraph).
    public partial class ArrowGraphManagerView
        : UserControl
    {
        public ArrowGraphManagerView()
        {
            InitializeComponent();
        }
    }
}
