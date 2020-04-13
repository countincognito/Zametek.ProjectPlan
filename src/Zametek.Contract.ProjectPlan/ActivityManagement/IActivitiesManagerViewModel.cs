using Prism.Interactivity.InteractionRequest;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Zametek.Contract.ProjectPlan
{
    public interface IActivitiesManagerViewModel
        : INamed
    {
        IInteractionRequest NotificationInteractionRequest { get; }

        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool ShowDates { get; }

        bool ShowDays { get; }

        bool HasCompilationErrors { get; }

        string CompilationOutput { get; }

        ObservableCollection<IManagedActivityViewModel> Activities { get; }

        ObservableCollection<IManagedActivityViewModel> SelectedActivities { get; }

        IManagedActivityViewModel SelectedActivity { get; }

        ICommand SetSelectedManagedActivitiesCommand { get; }

        ICommand AddManagedActivityCommand { get; }

        ICommand RemoveManagedActivityCommand { get; }
    }
}
