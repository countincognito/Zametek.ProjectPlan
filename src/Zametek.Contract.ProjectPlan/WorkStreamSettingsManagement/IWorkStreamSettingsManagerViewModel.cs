using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Zametek.Contract.ProjectPlan
{
    public interface IWorkStreamSettingsManagerViewModel
        : IDisposable
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        bool HasSelectedWorkStreams { get; }

        bool AreSettingsUpdated { get; set; }

        IReadOnlyList<IManagedWorkStreamViewModel> RawWorkStreams { get; }

        ReadOnlyObservableCollection<IManagedWorkStreamViewModel> WorkStreams { get; }

        ObservableCollection<IManagedWorkStreamViewModel> OrderableWorkStreams { get; }

        ICommand SetSelectedManagedWorkStreamsCommand { get; }

        ICommand AddManagedWorkStreamCommand { get; }

        ICommand RemoveManagedWorkStreamsCommand { get; }

        ICommand RenumberWorkStreamsCommand { get; }
    }
}
