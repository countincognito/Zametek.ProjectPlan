using Prism.Interactivity.InteractionRequest;
using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IMainViewModel
        : INamed
    {
        IInteractionRequest ConfirmationInteractionRequest { get; }

        IInteractionRequest NotificationInteractionRequest { get; }

        IInteractionRequest ResourceSettingsManagerInteractionRequest { get; }

        IInteractionRequest ArrowGraphSettingsManagerInteractionRequest { get; }

        IInteractionRequest AboutInteractionRequest { get; }

        bool IsBusy { get; }

        string Title { get; }

        bool IsProjectUpdated { get; }

        DateTime ProjectStart { get; set; }

        bool ShowDates { get; set; }

        bool UseBusinessDays { get; set; }

        bool AutoCompile { get; set; }

        ArrowGraphSettingsModel ArrowGraphSettings { get; }

        ResourceSettingsModel ResourceSettings { get; }

        IApplicationCommands ApplicationCommands { get; }

        ICommand OpenProjectPlanFileCommand { get; }

        ICommand SaveProjectPlanFileCommand { get; }

        ICommand SaveAsProjectPlanFileCommand { get; }

        ICommand ImportProjectCommand { get; }

        ICommand CloseProjectCommand { get; }

        ICommand OpenResourceSettingsCommand { get; }

        ICommand OpenArrowGraphSettingsCommand { get; }

        ICommand ToggleShowDatesCommand { get; }

        ICommand ToggleUseBusinessDaysCommand { get; }

        ICommand CalculateResourcedCyclomaticComplexityCommand { get; }

        ICommand CompileCommand { get; }

        ICommand TransitiveReductionCommand { get; }

        ICommand OpenHyperLinkCommand { get; }

        ICommand OpenAboutCommand { get; }

        ICommand ExportScenariosCommand { get; }

        Task DoOpenProjectPlanFileAsync(string fileName = null);
    }
}
