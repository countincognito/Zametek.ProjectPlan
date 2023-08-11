using Avalonia.Controls;
using OxyPlot;
using OxyPlot.Avalonia;

namespace Zametek.View.ProjectPlan
{
    public partial class ResourceChartManagerView
        : UserControl
    {
        public ResourceChartManagerView()
        {
            InitializeComponent();
            var oxyplot = this.FindControl<PlotView>("oxyplot"); // TODO remove and use x:Name reference
            oxyplot.ActualController.UnbindMouseDown(OxyMouseButton.Right);
            oxyplot.ActualController.UnbindMouseWheel();
        }
    }
}
