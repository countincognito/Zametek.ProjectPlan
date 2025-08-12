using System.Windows.Input;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IMainViewModel
        : IDisposable
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

        DateTimeOffset ProjectStart { get; set; }

        DateTimeOffset Today { get; set; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        bool ShowDates { get; set; }

        bool UseClassicDates { get; set; }

        bool UseBusinessDays { get; set; }

        bool DefaultShowDates { get; set; }

        bool DefaultUseClassicDates { get; set; }

        bool DefaultUseBusinessDays { get; set; }

        bool DefaultHideCost { get; set; }

        bool DefaultHideBilling { get; set; }

        bool AutoCompile { get; set; }

        string SelectedTheme { get; set; }

        BaseTheme BaseTheme { get; set; }

        ICommand OpenProjectPlanFileCommand { get; }

        ICommand SaveProjectPlanFileCommand { get; }

        ICommand SaveAsProjectPlanFileCommand { get; }

        ICommand ImportProjectFileCommand { get; }

        ICommand ExportProjectFileCommand { get; }

        ICommand CloseProjectPlanCommand { get; }

        ICommand ToggleShowDatesCommand { get; }

        ICommand ToggleUseClassicDatesCommand { get; }

        ICommand ToggleUseBusinessDaysCommand { get; }

        ICommand ToggleHideCostCommand { get; }

        ICommand ToggleHideBillingCommand { get; }

        ICommand ToggleDefaultShowDatesCommand { get; }

        ICommand ToggleDefaultUseClassicDatesCommand { get; }

        ICommand ToggleDefaultUseBusinessDaysCommand { get; }

        ICommand ToggleDefaultHideCostCommand { get; }

        ICommand ToggleDefaultHideBillingCommand { get; }

        ICommand ChangeThemeCommand { get; }

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