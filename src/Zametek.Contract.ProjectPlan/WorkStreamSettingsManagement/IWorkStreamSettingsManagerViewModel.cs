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

        bool HasWorkStreams { get; }

        bool AreSettingsUpdated { get; set; }

        ReadOnlyObservableCollection<IManagedWorkStreamViewModel> WorkStreams { get; }

        ICommand SetSelectedManagedWorkStreamsCommand { get; }

        ICommand AddManagedWorkStreamCommand { get; }

        ICommand RemoveManagedWorkStreamsCommand { get; }
    }
}
