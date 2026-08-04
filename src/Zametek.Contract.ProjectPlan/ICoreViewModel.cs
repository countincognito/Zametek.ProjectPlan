using System.Collections.ObjectModel;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Contract.ProjectPlan
{
    public interface ICoreViewModel
        : IKillSubscriptions, IDisposable
    {
        bool IsBusy { get; }

        bool IsBulkUpdating { get; }

        ReadyToCompile IsReadyToCompile { get; }

        bool IsProjectScenarioUpdated { get; set; }

        bool HasStaleOutputs { get; set; }

        DateTimeOffset ProjectStart { get; set; }

        DateTimeOffset Today { get; set; }

        string ProjectFinish { get; }

        IProjectScenarioDisplaySettingsViewModel DisplaySettingsViewModel { get; }

        bool DefaultShowDates { get; set; }

        bool DefaultUseClassicDates { get; set; }

        NonWorkingDayMode DefaultNonWorkingDayMode { get; set; }

        bool DefaultHideCost { get; set; }

        bool DefaultHideBilling { get; set; }

        bool AutoCompile { get; set; }

        string SelectedTheme { get; set; }

        BaseTheme BaseTheme { get; set; }

        IReadOnlyList<IManagedActivityViewModel> RawActivities { get; }

        ReadOnlyObservableCollection<IManagedActivityViewModel> Activities { get; }

        ObservableCollection<IManagedActivityViewModel> OrderableActivities { get; }

        GraphSettingsModel GraphSettings { get; set; }

        ResourceSettingsModel ResourceSettings { get; set; }

        WorkStreamSettingsModel WorkStreamSettings { get; set; }

        HolidaySettingsModel HolidaySettings { get; set; }

        MetricsModel Metrics { get; }

        RisksModel RiskMetrics { get; }

        CostsModel CostMetrics { get; }

        BillingsModel BillingMetrics { get; }

        MarginsModel MarginMetrics { get; }

        EffortsModel EffortMetrics { get; }

        NetworkModel NetworkMetrics { get; }

        bool HasActivities { get; }

        bool HasResources { get; }

        bool HasWorkStreams { get; }

        bool HasPhases { get; }

        bool HasCompilationErrors { get; }

        IGraphCompilation<int, int, int, IDependentActivity> GraphCompilation { get; }

        /// <summary>
        /// Increments once each time the full Build* output cascade has settled
        /// after a compilation (or bulk load), so subscribers that need every
        /// output in place can react exactly once per compile. The value is an
        /// opaque change pulse: it wraps to zero at an implementation-defined
        /// boundary, so observe changes only - never compare magnitudes or
        /// treat it as a monotonic counter.
        /// </summary>
        int CompilationOutputRevision { get; }

        ArrowGraphModel ArrowGraph { get; }

        VertexGraphModel VertexGraph { get; }

        GraphLayoutModel ArrowGraphLayout { get; set; }

        GraphLayoutModel VertexGraphLayout { get; set; }

        ResourceSeriesSetModel ResourceSeriesSet { get; }

        TrackingSeriesSetModel TrackingSeriesSet { get; }

        int TrackerIndex { get; set; }

        ReadyToRevise IsReadyToReviseTrackers { get; set; }

        int GetNextActivityId();

        ProjectScenarioModel CreateEmptyProjectScenario();

        void ClearSettings();

        void ResetProjectScenario();

        ProjectScenarioImportModel ImportProjectScenarioFile(string filename);

        void ExportProjectScenarioFile(ProjectScenarioModel projectScenarioModel, ResourceSeriesSetModel resourceSeriesSetModel, TrackingSeriesSetModel trackingSeriesSetModel, bool showDates, string filename);

        void ProcessProjectScenarioImport(ProjectScenarioImportModel projectScenarioImportModel, Guid projectScenarioId, string projectScenarioTitle);

        void ProcessProjectScenario(ProjectScenarioModel projectScenarioModel, Guid projectScenarioId, string projectScenarioTitle);

        ProjectScenarioModel BuildProjectScenario();

        int AddManagedActivity(int displayOrder);

        void AddManagedActivities(IEnumerable<DependentActivityModel> dependentActivityModels);

        void RemoveManagedActivities(IEnumerable<int> dependentActivities);

        void UpdateManagedActivities(IEnumerable<UpdateDependentActivityModel> updateModels);

        void AddMilestone(IEnumerable<int> dependentActivities);

        void UpdateActivityDisplayOrders();

        void UpdateManagedActivityIds(IEnumerable<(int OldId, int NewId)> idMaps);

        void UpdateManagedResourceIds(IEnumerable<(int OldId, int NewId)> idMaps);

        void UpdateManagedWorkStreamIds(IEnumerable<(int OldId, int NewId)> idMaps);

        void ClearManagedActivities();

        void SetActivityDuration(int activityId, int newDuration);

        void RunCompile();

        void RunAutoCompile();

        void RunTransitiveReduction();

        void BuildArrowGraph();

        void BuildVertexGraph();

        void BuildResourceSeriesSet();

        void BuildTrackingSeriesSet();

        void BuildNetworkMetrics();

        void BuildRiskMetrics();

        void BuildFinancialMetrics();
    }
}
