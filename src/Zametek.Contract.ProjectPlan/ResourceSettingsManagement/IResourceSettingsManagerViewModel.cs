using System.Collections.ObjectModel;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IResourceSettingsManagerViewModel
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        bool HideCost { get; }

        bool HideBilling { get; }

        bool HasSelectedResource { get; }

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

        // Invoked synchronously by the work stream settings manager when the work
        // streams change, mirroring the way CoreViewModel pushes them into the
        // activities. Never observed from here - see the note on the call site.
        void SetWorkStreamSettings(WorkStreamSettingsModel workStreamSettings);

        Task ReportErrorAsync(string message);
    }
}
