using System.Collections.ObjectModel;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IProjectScenarioManagerViewModel
        : IStartSubscriptions, IKillSubscriptions, IDisposable
    {
        bool IsBusy { get; }

        // Arms the title: it is made again from the settings every time this is set, whatever it was set to before.
        ReadyToRevise IsReadyToReviseTitle { get; set; }

        // How many times the title has been armed. A value that does not change raises no event in WhenAnyValue, and so
        // a flag that is already Yes cannot say that it was armed again. This one changes each time.
        int TitleRevision { get; }

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

        TrackedMetrics ScenarioChartTrackedMetricXAxis { get; set; }

        TrackedMetrics ScenarioChartTrackedMetricY1Axis { get; set; }

        TrackedMetrics ScenarioChartTrackedMetricY2Axis { get; set; }

        CurveFittingType ScenarioChartCurveFittingTypeY1 { get; set; }

        CurveFittingType ScenarioChartCurveFittingTypeY2 { get; set; }

        bool ScenarioChartShowNamesY1 { get; set; }

        bool ScenarioChartShowNamesY2 { get; set; }

        bool ScenarioChartShowDerivativeY1 { get; set; }

        bool ScenarioChartShowDerivativeY2 { get; set; }

        bool ScenarioChartAbsoluteCurveFittingY1 { get; set; }

        bool ScenarioChartAbsoluteCurveFittingY2 { get; set; }

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

        void BuildTrackedMetrics();

        ProjectModel BuildProject();
    }
}
