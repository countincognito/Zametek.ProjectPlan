using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Zametek.Contract.ProjectPlan
{
    public interface IActivitiesManagerViewModel
        : IDisposable
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool ShowDates { get; }

        bool HasCompilationErrors { get; }

        bool HasSelectedActivity { get; }

        bool HasSelectedActivities { get; }

        bool HideCost { get; }

        bool HideBilling { get; }

        IReadOnlyList<IManagedActivityViewModel> RawActivities { get; }

        ReadOnlyObservableCollection<IManagedActivityViewModel> Activities { get; }

        ObservableCollection<IManagedActivityViewModel> OrderableActivities { get; }

        ICommand SetSelectedManagedActivitiesCommand { get; }

        ICommand AddManagedActivityCommand { get; }

        ICommand InsertManagedActivityCommand { get; }

        ICommand RemoveManagedActivitiesCommand { get; }

        ICommand EditManagedActivitiesCommand { get; }

        ICommand DuplicateManagedActivityCommand { get; }

        ICommand RenumberActivitiesCommand { get; }

        ICommand AddMilestoneCommand { get; }
    }
}
