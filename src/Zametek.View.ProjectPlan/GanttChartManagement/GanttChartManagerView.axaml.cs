using Avalonia.Controls;
using OxyPlot;

namespace Zametek.View.ProjectPlan
{
    public partial class GanttChartManagerView
        : UserControl
    {
        public GanttChartManagerView()
        {
            InitializeComponent();
            oxyplot.ActualController.UnbindMouseDown(OxyMouseButton.Right);
            oxyplot.ActualController.UnbindMouseDown(OxyMouseButton.Left);
            oxyplot.ActualController.BindMouseDown(OxyMouseButton.Left, PlotCommands.PanAt);
            //oxyplot.ActualController.BindMouseEnter(PlotCommands.HoverTrack);
        }
    }
}
