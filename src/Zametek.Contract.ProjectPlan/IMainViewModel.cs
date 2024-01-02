using System.Windows.Input;

namespace Zametek.Contract.ProjectPlan
{
    public interface IMainViewModel
    {
        string ProjectTitle { get; }

        bool IsBusy { get; }

        bool IsProjectUpdated { get; }

        DateTimeOffset ProjectStart { get; set; }

        DateTime ProjectStartDateTime { get; set; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        bool ShowDates { get; set; }

        bool UseBusinessDays { get; set; }

        bool AutoCompile { get; set; }

        ICommand OpenProjectPlanFileCommand { get; }

        ICommand SaveProjectPlanFileCommand { get; }

        ICommand SaveAsProjectPlanFileCommand { get; }

        ICommand ImportProjectFileCommand { get; }

        ICommand ExportProjectFileCommand { get; }

        ICommand CloseProjectPlanCommand { get; }

        ICommand ToggleShowDatesCommand { get; }

        ICommand ToggleUseBusinessDaysCommand { get; }

        ICommand CompileCommand { get; }

        ICommand ToggleAutoCompileCommand { get; }

        ICommand TransitiveReductionCommand { get; }

        ICommand OpenHyperLinkCommand { get; }

        ICommand OpenAboutCommand { get; }

        void CloseLayout();

        void ResetLayout();

        Task OpenProjectPlanFileAsync();

        Task OpenProjectPlanFileAsync(string? filename);

        Task SaveProjectPlanFileAsync();

        Task SaveAsProjectPlanFileAsync();

        Task ImportProjectFileAsync();

        Task ExportProjectFileAsync();

        Task CloseProjectPlanAsync();

        Task OpenHyperLinkAsync(string hyperlink);

        Task OpenAboutAsync();
    }
}