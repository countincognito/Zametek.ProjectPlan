using System.Collections.ObjectModel;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IProjectScenarioManagerViewModel
        : IKillSubscriptions
    {
        bool IsBusy { get; }

        ReadyToRevise IsReadyToReviseTitle { get; set; }

        bool IsLoading { get; }

        bool IsCreating { get; }

        bool IsRenaming { get; }

        bool IsRemoving { get; }

        bool IsProjectUpdated { get; set; }

        bool IsProjectScenarioUpdated { get; }

        bool ProjectHasChanges { get; }

        IManagedNodeViewModel Root { get; }

        IReadOnlyList<IManagedNodeViewModel> RawNodes { get; }

        ReadOnlyObservableCollection<IManagedNodeViewModel> Nodes { get; }

        ObservableCollection<IManagedNodeViewModel> SelectedNodes { get; }

        IManagedNodeViewModel? SelectedNode { get; }

        SortMode SelectedSortMode { get; set; }

        SortDirection SelectedSortDirection { get; set; }

        ICommand SetSelectedManagedNodesCommand { get; }

        ICommand LoadProjectScenarioFileCommand { get; }

        ICommand LoadSelectedProjectScenarioFileCommand { get; }

        ICommand CreateEmptyProjectScenarioFileCommand { get; }

        ICommand CreateEmptyProjectScenarioFolderCommand { get; }

        ICommand RenameProjectScenarioNodeCommand { get; }

        ICommand RemoveProjectScenarioNodeCommand { get; }

        ICommand CutProjectScenarioNodeCommand { get; }

        ICommand CopyProjectScenarioNodeCommand { get; }

        ICommand PasteProjectScenarioNodeCommand { get; }

        ICommand AddNodeTagCommand { get; }

        ICommand RemoveNodeTagCommand { get; }

        ICommand ChangeSortModeCommand { get; }

        ICommand ChangeSortDirectionCommand { get; }

        void ResetProject();

        void ProcessProject(ProjectModel projectModel);

        IManagedNodeViewModel? GetNode(Guid nodeId);

        IManagedNodeViewModel? GetNodeParent(Guid nodeId);

        ProjectModel BuildProject();
    }
}
