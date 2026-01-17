using System.Collections.ObjectModel;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IProjectManagerViewModel
        : IKillSubscriptions
    {
        bool IsBusy { get; }

        bool IsLoading { get; }

        bool IsCreating { get; }

        bool IsRenaming { get; }

        bool IsRemoving { get; }

        bool IsProjectUpdated { get; set; }

        bool IsProjectPlanUpdated { get; }

        bool ProjectHasChanges { get; }

        IManagedNodeViewModel Root { get; }

        ReadOnlyObservableCollection<IManagedNodeViewModel> Nodes { get; }

        ObservableCollection<IManagedNodeViewModel> SelectedNodes { get; }

        IManagedNodeViewModel? SelectedNode { get; }

        ICommand SetSelectedManagedNodesCommand { get; }

        ICommand LoadProjectPlanFileCommand { get; }

        ICommand LoadSelectedProjectPlanFileCommand { get; }

        ICommand CreateEmptyProjectPlanFileCommand { get; }

        ICommand CreateEmptyProjectPlanFolderCommand { get; }

        ICommand RenameProjectPlanNodeCommand { get; }

        ICommand RemoveProjectPlanNodeCommand { get; }

        ICommand AddNodeTagCommand { get; }

        ICommand RemoveNodeTagCommand { get; }

        void ResetProject();

        void ProcessProject(ProjectModel projectModel);

        IManagedNodeViewModel? GetNode(Guid nodeId);

        IManagedNodeViewModel? GetNodeParent(Guid nodeId);

        ProjectModel BuildProject();
    }
}
