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

        ReadyToRevise IsReadyToReviseTrackedMetrics { get; set; }

        bool IsLoading { get; }

        bool IsCreating { get; }

        bool IsRenaming { get; }

        bool IsRemoving { get; }

        bool IsProjectUpdated { get; set; }

        bool IsProjectScenarioUpdated { get; }

        bool ProjectHasChanges { get; }

        IProjectDisplaySettingsViewModel DisplaySettingsViewModel { get; }

        SortMode ProjectScenarioSortMode { get; set; }

        SortDirection ProjectScenarioSortDirection { get; set; }

        bool ScenarioChartShowNames { get; set; }

        TrackedMetrics ScenarioChartTrackedMetricXAxis { get; set; }

        TrackedMetrics ScenarioChartTrackedMetricYAxis { get; set; }

        CurveFittingType ScenarioChartCurveFittingType { get; set; }

        IManagedNodeViewModel Root { get; }

        IReadOnlyList<IManagedNodeViewModel> RawNodes { get; }

        ReadOnlyObservableCollection<IManagedNodeViewModel> Nodes { get; }

        IReadOnlyList<IManagedNodeViewModel> RawFlattenedNodes { get; }

        ReadOnlyObservableCollection<IManagedNodeViewModel> FlattenedNodes { get; }

        ObservableCollection<IManagedNodeViewModel> SelectedNodes { get; }

        IManagedNodeViewModel? SelectedNode { get; }

        TrackedMetricsSetModel TrackedMetricsSet { get; }

        ICommand SetSelectedManagedNodesCommand { get; }

        ICommand SetNoSelectedManagedNodesCommand { get; }

        ICommand LoadProjectScenarioFileCommand { get; }

        ICommand LoadSelectedProjectScenarioFileCommand { get; }

        ICommand CreateEmptyProjectScenarioFileCommand { get; }

        ICommand CreateEmptyProjectScenarioFolderCommand { get; }

        ICommand RenameProjectScenarioNodeCommand { get; }

        ICommand RemoveProjectScenarioNodeCommand { get; }

        ICommand CutProjectScenarioNodeCommand { get; }

        ICommand CopyProjectScenarioNodeCommand { get; }

        ICommand DuplicateProjectScenarioNodeCommand { get; }

        ICommand PasteProjectScenarioNodeCommand { get; }

        ICommand AddNodeTagCommand { get; }

        ICommand RemoveNodeTagCommand { get; }

        ICommand ChangeSortModeCommand { get; }

        ICommand ChangeSortDirectionCommand { get; }

        void ResetManagedNodes();

        void ResetProject();

        void ProcessProject(ProjectModel projectModel);

        IManagedNodeViewModel? GetNode(Guid nodeId);

        IManagedNodeViewModel? GetNodeParent(Guid nodeId);

        ProjectModel BuildProject();
    }
}
