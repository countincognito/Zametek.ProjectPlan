using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using OxyPlot.Avalonia;
using OxyPlot;

namespace Zametek.View.ProjectPlan
{
    public partial class GanttChartManagerView
        : UserControl
    {
        public GanttChartManagerView()
        {
            InitializeComponent();
            var oxyplot = this.FindControl<PlotView>("oxyplot");
            oxyplot.ActualController.UnbindMouseDown(OxyMouseButton.Right);
            oxyplot.ActualController.BindMouseDown(OxyMouseButton.Left, PlotCommands.PanAt);
            oxyplot.ActualController.BindMouseEnter(PlotCommands.HoverTrack);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
