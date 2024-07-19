using System.Windows.Input;

namespace Zametek.Contract.ProjectPlan
{
    public interface IEarnedValueChartManagerViewModel
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        bool ViewProjections { get; set; }

        ICommand SaveEarnedValueChartImageFileCommand { get; }

        Task SaveEarnedValueChartImageFileAsync(string? filename, int width, int height);
    }
}
