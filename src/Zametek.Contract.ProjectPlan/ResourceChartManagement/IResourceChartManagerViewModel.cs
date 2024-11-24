using System.Windows.Input;

namespace Zametek.Contract.ProjectPlan
{
    public interface IResourceChartManagerViewModel
        : IKillSubscriptions
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        string SelectedTheme { get; }

        ICommand SaveResourceChartImageFileCommand { get; }

        Task SaveResourceChartImageFileAsync(string? filename, int width, int height);

        void BuildResourceChartPlotModel();
    }
}
