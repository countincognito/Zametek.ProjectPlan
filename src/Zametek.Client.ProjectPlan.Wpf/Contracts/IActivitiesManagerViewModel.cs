using Prism.Interactivity.InteractionRequest;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public interface IActivitiesManagerViewModel
    {
        IInteractionRequest NotificationInteractionRequest { get; }

        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool ShowDates { get; }

        bool ShowDays { get; }

        bool HasCompilationErrors { get; }

        string CompilationOutput { get; }

        ObservableCollection<ManagedActivityViewModel> Activities { get; }

        ObservableCollection<ManagedActivityViewModel> SelectedActivities { get; }

        ManagedActivityViewModel SelectedActivity { get; }

        ICommand SetSelectedManagedActivitiesCommand { get; }

        ICommand AddManagedActivityCommand { get; }

        ICommand RemoveManagedActivityCommand { get; }
    }
}
