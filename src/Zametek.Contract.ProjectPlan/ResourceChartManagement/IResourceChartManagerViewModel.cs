using System.Windows.Input;

namespace Zametek.Contract.ProjectPlan
{
    public interface IResourceChartManagerViewModel
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        ICommand SaveResourceChartImageFileCommand { get; }

        Task SaveResourceChartImageFileAsync(string? filename, int width, int height);
    }
}
