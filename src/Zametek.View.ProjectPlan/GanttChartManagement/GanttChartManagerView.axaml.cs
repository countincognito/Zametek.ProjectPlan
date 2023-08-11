using Avalonia.Controls;
using OxyPlot;
using OxyPlot.Avalonia;

namespace Zametek.View.ProjectPlan
{
    public partial class GanttChartManagerView
        : UserControl
    {
        public GanttChartManagerView()
        {
            InitializeComponent();
            var oxyplot = this.FindControl<PlotView>("oxyplot"); // TODO remove and use x:Name reference
            oxyplot.ActualController.UnbindMouseDown(OxyMouseButton.Right);
            oxyplot.ActualController.UnbindMouseDown(OxyMouseButton.Left);
            oxyplot.ActualController.BindMouseDown(OxyMouseButton.Left, PlotCommands.PanAt);
            //oxyplot.ActualController.BindMouseEnter(PlotCommands.HoverTrack);
        }
    }
}
