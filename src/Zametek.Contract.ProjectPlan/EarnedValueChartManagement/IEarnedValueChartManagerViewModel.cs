using OxyPlot;
using Prism.Interactivity.InteractionRequest;
using System.Windows.Input;

namespace Zametek.Contract.ProjectPlan
{
    public interface IEarnedValueChartManagerViewModel
        : INamed
    {
        IInteractionRequest NotificationInteractionRequest { get; }

        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        PlotModel EarnedValueChartPlotModel { get; }

        int EarnedValueChartOutputWidth { get; set; }

        int EarnedValueChartOutputHeight { get; set; }

        ICommand CopyEarnedValueChartToClipboardCommand { get; }

        ICommand ExportEarnedValueChartToCsvCommand { get; }
    }
}
