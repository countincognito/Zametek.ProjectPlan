using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Zametek.Contract.ProjectPlan
{
    public interface IResourceSettingsManagerViewModel
    {
        double DefaultUnitCost { get; set; }

        bool DisableResources { get; set; }

        bool ActivateResources { get; }

        ObservableCollection<IManagedResourceViewModel> Resources { get; }

        IManagedResourceViewModel SelectedResource { get; }

        ICommand SetSelectedManagedResourcesCommand { get; }

        ICommand AddManagedResourceCommand { get; }

        ICommand RemoveManagedResourceCommand { get; }
    }
}
