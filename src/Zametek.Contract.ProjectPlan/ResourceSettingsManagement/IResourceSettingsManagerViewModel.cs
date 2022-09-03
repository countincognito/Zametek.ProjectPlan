using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Zametek.Contract.ProjectPlan
{
    public interface IResourceSettingsManagerViewModel
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        bool HasResources { get; }

        double DefaultUnitCost { get; set; }

        bool DisableResources { get; set; }

        bool AreSettingsUpdated { get; set; }

        ReadOnlyObservableCollection<IManagedResourceViewModel> Resources { get; }

        ICommand SetSelectedManagedResourcesCommand { get; }

        ICommand AddManagedResourceCommand { get; }

        ICommand RemoveManagedResourcesCommand { get; }
    }
}
