using OxyPlot;
using Prism.Interactivity.InteractionRequest;
using System.Windows.Input;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public interface IEarnedValueChartsManagerViewModel
    {
        IInteractionRequest NotificationInteractionRequest
        {
            get;
        }

        bool IsBusy
        {
            get;
        }

        bool HasStaleOutputs
        {
            get;
        }

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
