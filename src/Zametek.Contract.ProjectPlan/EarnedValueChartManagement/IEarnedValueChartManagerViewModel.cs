using System.Windows.Input;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IEarnedValueChartManagerViewModel
        : IKillSubscriptions, IDisposable
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        bool ShowProjections { get; set; }

        bool ShowToday { get; set; }

        bool ShowMilestones { get; set; }

        bool CombineResources { get; set; }

        bool ScaleToOwnPlan { get; set; }

        bool HasResources { get; }

        bool HasSingleTrackingSeriesSet { get; }

        IResourceSelectorViewModel ResourceSelector { get; }

        ICommand ResetEarnedValueChartCommand { get; }

        ICommand SaveEarnedValueChartImageFileCommand { get; }

        // Writes the chart to the stream at the given size. The caller owns the stream, which is left open.
        Task WriteEarnedValueChartImageAsync(Stream stream, ChartImageFormat format, int width, int height);

        void BuildEarnedValueChartPlotModel();
    }
}
