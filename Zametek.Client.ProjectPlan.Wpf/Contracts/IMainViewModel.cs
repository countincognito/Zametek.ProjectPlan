using Prism.Interactivity.InteractionRequest;
using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Zametek.Common.Project;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public interface IMainViewModel
    {
        IInteractionRequest ConfirmationInteractionRequest
        {
            get;
        }

        IInteractionRequest NotificationInteractionRequest
        {
            get;
        }

        IInteractionRequest ResourceSettingsManagerInteractionRequest
        {
            get;
        }

        IInteractionRequest ArrowGraphSettingsManagerInteractionRequest
        {
            get;
        }

        IInteractionRequest AboutInteractionRequest
        {
            get;
        }

        bool IsBusy
        {
            get;
        }

        string ProjectTitle
        {
            get;
        }

        bool IsProjectUpdated
        {
            get;
        }

        DateTime ProjectStart
        {
            get;
            set;
        }

        bool ShowDates
        {
            get;
            set;
        }

        bool UseBusinessDays
        {
            get;
            set;
        }

        bool AutoCompile
        {
            get;
            set;
        }

        ArrowGraphSettingsDto ArrowGraphSettingsDto
        {
            get;
        }

        ResourceSettingsDto ResourceSettingsDto
        {
            get;
        }

        ICommand OpenProjectPlanFileCommand
        {
            get;
        }

        ICommand SaveProjectPlanFileCommand
        {
            get;
        }

        ICommand ImportMicrosoftProjectCommand
        {
            get;
        }

        ICommand CloseProjectCommand
        {
            get;
        }

        ICommand OpenResourceSettingsCommand
        {
            get;
        }

        ICommand OpenArrowGraphSettingsCommand
        {
            get;
        }

        ICommand CompileCommand
        {
            get;
        }

        ICommand OpenHyperLinkCommand
        {
            get;
        }

        ICommand OpenAboutCommand
        {
            get;
        }

        Task DoOpenProjectPlanFileAsync(string fileName = null);
    }
}
