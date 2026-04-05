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

        bool IsProjectScenarioUpdated { get; set; }

        bool HasStaleOutputs { get; set; }

        DateTimeOffset ProjectStart { get; set; }

        DateTimeOffset Today { get; set; }

        string ProjectFinish { get; }

        IDisplaySettingsViewModel DisplaySettingsViewModel { get; }

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

        ArrowGraphModel ArrowGraph { get; }

        VertexGraphModel VertexGraph { get; }

        ResourceSeriesSetModel ResourceSeriesSet { get; }

        TrackingSeriesSetModel TrackingSeriesSet { get; }

        int TrackerIndex { get; set; }

        ReadyToRevise IsReadyToReviseTrackers { get; set; }

        ProjectScenarioModel CreateEmptyProjectScenario();

        void ClearSettings();

        void ResetProjectScenario();

        ProjectScenarioImportModel ImportProjectScenarioFile(string filename);

        void ExportProjectScenarioFile(ProjectScenarioModel projectScenarioModel, ResourceSeriesSetModel resourceSeriesSetModel, TrackingSeriesSetModel trackingSeriesSetModel, bool showDates, string filename);

        void ProcessProjectScenarioImport(ProjectScenarioImportModel projectScenarioImportModel, Guid projectScenarioId, string projectScenarioTitle);

        void ProcessProjectScenario(ProjectScenarioModel projectScenarioModel, Guid projectScenarioId, string projectScenarioTitle);

        ProjectScenarioModel BuildProjectScenario();

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