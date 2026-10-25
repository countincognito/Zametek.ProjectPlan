using System.Collections.ObjectModel;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Contract.ProjectPlan
{
    public interface ICoreViewModel
        : IStartSubscriptions, IKillSubscriptions, IDisposable
    {
        bool IsBusy { get; }

        bool IsBulkUpdating { get; }

        ReadyToCompile IsReadyToCompile { get; }

        bool IsProjectScenarioUpdated { get; set; }

        bool HasStaleOutputs { get; set; }

        DateTimeOffset ProjectStart { get; set; }

        DateTimeOffset Today { get; set; }

        string ProjectFinish { get; }

        int? ProjectFinishDays { get; }

        DateOnly? ProjectFinishDate { get; }

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

        List<ResourceMetricsModel> ResourceMetrics { get; }

        /// <summary>
        /// The settings of the schedule simulation of the scenario. They belong to the scenario, as its display
        /// settings do: a project file holds them. Setting them marks the scenario as modified, except while a
        /// scenario loads or resets.
        /// </summary>
        SimulationSettingsModel SimulationSettings { get; set; }

        /// <summary>
        /// What the last run of the simulation found, or null when the scenario has no result. A project file holds
        /// it, and a result that a file holds is trusted, as the metrics that a file holds are. Setting it marks the
        /// scenario as modified, except while a scenario loads or resets.
        /// </summary>
        SimulationSummaryModel? SimulationSummary { get; set; }

        /// <summary>
        /// How many times a scenario has been loaded or reset (a project that closes or begins is a reset too). A run
        /// of the simulation belongs to one scenario, and it stops when this changes. Observe changes only.
        /// </summary>
        int ProjectScenarioRevision { get; }

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

        ProjectScenarioImportModel ImportProjectScenarioFile(Stream stream, ProjectScenarioImportFormat format);

        void ExportProjectScenarioFile(ProjectScenarioModel projectScenarioModel, ResourceSeriesSetModel resourceSeriesSetModel, TrackingSeriesSetModel trackingSeriesSetModel, bool showDates, Stream stream, ProjectScenarioExportFormat format);

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

        // Takes the optimistic and the pessimistic duration of each of the activities back to its most likely duration.
        void ResetActivityEstimates(IEnumerable<int> activityIds);

        // Copies the plan, as it is at this moment, for a simulation to run on: the activities in the order of the
        // plan, the resources and the work streams that a compile uses, the three estimates of each activity, and
        // the revision of the compilation outputs that the plan was at. The copy is independent of the live
        // activities, and so an edit that is made while the simulation runs cannot reach it.
        SimulationInputModel CaptureSimulationInput();

        // Tells the core that something a compilation reads has changed - the commit of an edit to an activity, for
        // one. A compilation that is running did not see the change, and so its results are out of date as soon as
        // it publishes them: the core marks the outputs as stale and, when auto compilation is on, compiles again.
        // Call it before anything announces the edit.
        void NoteInputsChanged();

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
