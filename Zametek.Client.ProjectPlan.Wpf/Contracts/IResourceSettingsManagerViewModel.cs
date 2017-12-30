using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public interface IResourceSettingsManagerViewModel
    {
        double DefaultUnitCost
        {
            get;
            set;
        }

        bool DisableResources
        {
            get;
            set;
        }

        bool ActivateResources
        {
            get;
        }

        ObservableCollection<ManagedResourceViewModel> Resources
        {
            get;
        }

        ManagedResourceViewModel SelectedResource
        {
            get;
        }

        ICommand SetSelectedManagedResourcesCommand
        {
            get;
        }

        ICommand AddManagedResourceCommand
        {
            get;
        }

        ICommand RemoveManagedResourceCommand
        {
            get;
        }
    }
}
