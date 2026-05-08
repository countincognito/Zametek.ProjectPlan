using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Zametek.Contract.ProjectPlan
{
    public interface IResourceSettingsManagerViewModel
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        bool HideCost { get; }

        bool HideBilling { get; }

        bool HasSelectedResources { get; }

        double DefaultUnitCost { get; set; }

        bool DisableResources { get; set; }

        bool AreSettingsUpdated { get; set; }

        IReadOnlyList<IManagedResourceViewModel> RawResources { get; }

        ReadOnlyObservableCollection<IManagedResourceViewModel> Resources { get; }

        ObservableCollection<IManagedResourceViewModel> OrderableResources { get; }

        ICommand SetSelectedManagedResourcesCommand { get; }

        ICommand AddManagedResourceCommand { get; }

        ICommand RemoveManagedResourcesCommand { get; }

        ICommand DuplicateManagedResourceCommand { get; }

        ICommand EditManagedResourcesCommand { get; }

        ICommand RenumberResourcesCommand { get; }
    }
}
