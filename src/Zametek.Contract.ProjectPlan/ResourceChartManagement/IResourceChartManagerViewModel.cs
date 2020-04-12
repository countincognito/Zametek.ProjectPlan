using OxyPlot;
using Prism.Interactivity.InteractionRequest;
using System.Windows.Input;

namespace Zametek.Contract.ProjectPlan
{
    public interface IResourceChartManagerViewModel
        : INamed
    {
        IInteractionRequest NotificationInteractionRequest { get; }

        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool ExportResourceChartAsCosts { get; set; }

        PlotModel ResourceChartPlotModel { get; }

        int ResourceChartOutputWidth { get; set; }

        int ResourceChartOutputHeight { get; set; }

        ICommand CopyResourceChartToClipboardCommand { get; }

        ICommand ExportResourceChartToCsvCommand { get; }
    }
}
