using OxyPlot;
using System.Windows.Input;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public interface IEarnedValueChartsManagerViewModel
    {        
        PlotModel EarnedValueChartPlotModel
        {
            get;
        }

        int EarnedValueChartOutputWidth
        {
            get;
            set;
        }

        int EarnedValueChartOutputHeight
        {
            get;
            set;
        }

        ICommand CopyEarnedValueChartToClipboardCommand
        {
            get;
        }

        ICommand ExportEarnedValueChartToCsvCommand
        {
            get;
        }
    }
}
