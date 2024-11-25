using Avalonia.Controls;
using OxyPlot;

namespace Zametek.View.ProjectPlan
{
    public partial class ResourceChartManagerView
        : UserControl
    {
        public ResourceChartManagerView()
        {
            InitializeComponent();
            oxyplot.ActualController.UnbindMouseDown(OxyMouseButton.Right);
            oxyplot.ActualController.UnbindMouseWheel();
        }
    }
}
