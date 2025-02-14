using System.Windows.Input;

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

        ICommand SaveEarnedValueChartImageFileCommand { get; }

        Task SaveEarnedValueChartImageFileAsync(string? filename, int width, int height);

        void BuildEarnedValueChartPlotModel();
    }
}
