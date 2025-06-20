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
            oxyplot.ActualController.UnbindMouseDown(OxyMouseButton.Left);
            oxyplot.ActualController.BindMouseDown(OxyMouseButton.Left, PlotCommands.PanAt);
            //oxyplot.ActualController.BindMouseDown(OxyMouseButton.Left, OxyModifierKeys.Control, PlotCommands.Track);
            //oxyplot.ActualController.UnbindMouseWheel();
        }
    }
}
