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

        bool HasActivities { get; }

        ReadOnlyObservableCollection<IManagedActivityViewModel> Activities { get; }

        ICommand SetSelectedManagedActivitiesCommand { get; }

        ICommand AddManagedActivityCommand { get; }

        ICommand RemoveManagedActivitiesCommand { get; }

        ICommand AddMilestoneCommand { get; }
    }
}
