using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Zametek.Contract.ProjectPlan
{
    public interface IWorkStreamSettingsManagerViewModel
        : IStartSubscriptions, IKillSubscriptions, IDisposable
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        bool HasSelectedWorkStream { get; }

        bool HasSelectedWorkStreams { get; }

        bool AreSettingsUpdated { get; set; }

        IReadOnlyList<IManagedWorkStreamViewModel> RawWorkStreams { get; }

        ReadOnlyObservableCollection<IManagedWorkStreamViewModel> WorkStreams { get; }

        ObservableCollection<IManagedWorkStreamViewModel> OrderableWorkStreams { get; }

        ICommand SetSelectedManagedWorkStreamsCommand { get; }

        ICommand AddManagedWorkStreamCommand { get; }

        ICommand RemoveManagedWorkStreamsCommand { get; }

        ICommand DuplicateManagedWorkStreamCommand { get; }

        ICommand EditManagedWorkStreamsCommand { get; }

        ICommand RenumberWorkStreamsCommand { get; }

        // Gives the core the switch IsHiddenFromCharts of each work stream, and nothing else. It starts no compile (see
        // ICoreViewModel.SetWorkStreamChartVisibility). A managed work stream calls it when its switch changes.
        void UpdateChartVisibilityToCore();

        Task ReportErrorAsync(string message);
    }
}
