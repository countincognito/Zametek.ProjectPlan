using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Zametek.Contract.ProjectPlan
{
    public interface IArrowGraphSettingsManagerViewModel
        : IDisposable
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        bool HasActivitySeverities { get; }

        bool AreSettingsUpdated { get; set; }

        ReadOnlyObservableCollection<IManagedActivitySeverityViewModel> ActivitySeverities { get; }

        ICommand SetSelectedManagedActivitySeveritiesCommand { get; }

        ICommand AddManagedActivitySeverityCommand { get; }

        ICommand RemoveManagedActivitySeveritiesCommand { get; }
    }
}
