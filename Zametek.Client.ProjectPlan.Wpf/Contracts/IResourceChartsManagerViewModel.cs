using OxyPlot;
using System.Windows.Input;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public interface IResourceChartsManagerViewModel
    {
        bool ExportChartAsCosts
        {
            get;
            set;
        }

        PlotModel ChartPlotModel
        {
            get;
        }

        int ChartOutputWidth
        {
            get;
            set;
        }

        int ChartOutputHeight
        {
            get;
            set;
        }

        ICommand CopyChartToClipboardCommand
        {
            get;
        }

        ICommand ExportChartToCsvCommand
        {
            get;
        }
    }
}
