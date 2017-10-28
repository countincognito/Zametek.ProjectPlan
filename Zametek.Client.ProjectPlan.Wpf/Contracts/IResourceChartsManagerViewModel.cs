using OxyPlot;
using System.Windows.Input;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public interface IResourceChartsManagerViewModel
    {
        bool ExportResourceChartAsCosts
        {
            get;
            set;
        }

        PlotModel ResourceChartPlotModel
        {
            get;
        }

        int ResourceChartOutputWidth
        {
            get;
            set;
        }

        int ResourceChartOutputHeight
        {
            get;
            set;
        }

        ICommand CopyResourceChartToClipboardCommand
        {
            get;
        }

        ICommand ExportResourceChartToCsvCommand
        {
            get;
        }
    }
}
