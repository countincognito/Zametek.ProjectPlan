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

        bool ViewProjections { get; set; }

        ICommand SaveEarnedValueChartImageFileCommand { get; }

        Task SaveEarnedValueChartImageFileAsync(string? filename, int width, int height);

        void BuildEarnedValueChartPlotModel();
    }
}
