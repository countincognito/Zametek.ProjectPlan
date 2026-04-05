using System.Windows.Input;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IMainViewModel
        : IKillSubscriptions, IDisposable
    {
        string ProjectTitle { get; }

        bool IsBusy { get; }

        bool IsOpening { get; }

        bool IsSaving { get; }

        bool IsSavingAs { get; }

        bool IsImporting { get; }

        bool IsExporting { get; }

        bool IsClosing { get; }

        bool IsProjectUpdated { get; }

        bool IsProjectScenarioUpdated { get; }

        bool ProjectHasChanges { get; }

        DateTimeOffset ProjectStart { get; set; }

        DateTimeOffset Today { get; set; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        bool ShowDates { get; set; }

        bool UseClassicDates { get; set; }

        NonWorkingDayMode NonWorkingDayMode { get; set; }

        bool DefaultShowDates { get; set; }

        bool DefaultUseClassicDates { get; set; }

        NonWorkingDayMode DefaultNonWorkingDayMode { get; set; }

        bool DefaultHideCost { get; set; }

        bool DefaultHideBilling { get; set; }

        bool AutoCompile { get; set; }

        string SelectedTheme { get; set; }

        BaseTheme BaseTheme { get; set; }

        ICommand OpenProjectFileCommand { get; }

        ICommand SaveProjectFileCommand { get; }

        ICommand SaveAsProjectFileCommand { get; }

        ICommand ImportProjectScenarioFileCommand { get; }

        ICommand ExportProjectScenarioFileCommand { get; }

        ICommand CloseProjectCommand { get; }

        ICommand ToggleShowDatesCommand { get; }

        ICommand ToggleUseClassicDatesCommand { get; }

        ICommand ToggleHideCostCommand { get; }

        ICommand ToggleHideBillingCommand { get; }

        ICommand ToggleDefaultShowDatesCommand { get; }

        ICommand ToggleDefaultUseClassicDatesCommand { get; }

        ICommand ToggleDefaultHideCostCommand { get; }

        ICommand ToggleDefaultHideBillingCommand { get; }

        ICommand ChangeThemeCommand { get; }

        ICommand CompileCommand { get; }

        ICommand ToggleAutoCompileCommand { get; }

        ICommand TransitiveReductionCommand { get; }

        ICommand OpenDocumentationCommand { get; }

        ICommand OpenDonateCommand { get; }

        ICommand OpenMainPageCommand { get; }

        ICommand OpenReportIssueCommand { get; }

        ICommand OpenViewLicenseCommand { get; }

        ICommand OpenAboutCommand { get; }

        void CloseLayout();

        void ResetLayout();

        Task OpenProjectFileAsync();

        Task OpenProjectFileAsync(string? filename);

        Task SaveProjectFileAsync();

        Task SaveAsProjectFileAsync();

        Task ImportProjectScenarioFileAsync();

        Task ExportProjectScenarioFileAsync();

        Task CloseProjectAsync();

        Task OpenDocumentationAsync();

        Task OpenDonateAsync();

        Task OpenMainPageAsync();

        Task OpenReportIssueAsync();

        Task OpenViewLicenseAsync();

        Task OpenAboutAsync();
    }
}
