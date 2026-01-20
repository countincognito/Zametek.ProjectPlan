using System.Collections.ObjectModel;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Contract.ProjectPlan
{
    public interface ICoreViewModel
        : IKillSubscriptions, IDisposable
    {
        bool IsBusy { get; }

        ReadyToCompile IsReadyToCompile { get; }

        bool IsProjectPlanUpdated { get; set; }

        bool HasStaleOutputs { get; set; }

        DateTimeOffset ProjectStart { get; set; }

        DateTimeOffset Today { get; set; }

        IDisplaySettingsViewModel DisplaySettingsViewModel { get; }

        bool DefaultShowDates { get; set; }

        bool DefaultUseClassicDates { get; set; }

        bool DefaultUseBusinessDays { get; set; }

        bool DefaultHideCost { get; set; }

        bool DefaultHideBilling { get; set; }

        bool AutoCompile { get; set; }

        string SelectedTheme { get; set; }

        BaseTheme BaseTheme { get; set; }

        IReadOnlyList<IManagedActivityViewModel> RawActivities { get; }

        ReadOnlyObservableCollection<IManagedActivityViewModel> Activities { get; }

        GraphSettingsModel GraphSettings { get; set; }

        ResourceSettingsModel ResourceSettings { get; set; }

        WorkStreamSettingsModel WorkStreamSettings { get; set; }

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

        ArrowGraphModel ArrowGraph { get; }

        VertexGraphModel VertexGraph { get; }

        ResourceSeriesSetModel ResourceSeriesSet { get; }

        TrackingSeriesSetModel TrackingSeriesSet { get; }

        int TrackerIndex { get; set; }

        ReadyToRevise IsReadyToReviseTrackers { get; set; }

        ProjectPlanModel CreateEmptyProjectPlan();

        void ClearSettings();

        void ResetProjectPlan();

        ProjectPlanImportModel ImportProjectPlanFile(string filename);

        void ExportProjectPlanFile(ProjectPlanModel projectPlanModel, ResourceSeriesSetModel resourceSeriesSetModel, TrackingSeriesSetModel trackingSeriesSetModel, bool showDates, string filename);

        void ProcessProjectPlanImport(ProjectPlanImportModel projectPlanImportModel, Guid projectPlanId);

        void ProcessProjectPlan(ProjectPlanModel projectPlanModel, Guid projectPlanId);

        ProjectPlanModel BuildProjectPlan();

        int AddManagedActivity();

        void AddManagedActivities(IEnumerable<DependentActivityModel> dependentActivityModels);

        void RemoveManagedActivities(IEnumerable<int> dependentActivities);

        void UpdateManagedActivities(IEnumerable<UpdateDependentActivityModel> updateModels);

        void AddMilestone(IEnumerable<int> dependentActivities);

        void ClearManagedActivities();

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