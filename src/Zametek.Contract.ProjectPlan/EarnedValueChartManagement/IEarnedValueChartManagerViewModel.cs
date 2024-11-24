using System.Windows.Input;

namespace Zametek.Contract.ProjectPlan
{
    public interface IEarnedValueChartManagerViewModel
        : IKillSubscriptions, IDisposable
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        bool ViewProjections { get; set; }

        string SelectedTheme { get; }

        ICommand SaveEarnedValueChartImageFileCommand { get; }

        Task SaveEarnedValueChartImageFileAsync(string? filename, int width, int height);

        void BuildEarnedValueChartPlotModel();
    }
}
