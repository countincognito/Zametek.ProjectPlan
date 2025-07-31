using System.Collections.ObjectModel;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Contract.ProjectPlan
{
    public interface ICoreViewModel
        : IKillSubscriptions, IDisposable
    {
        string ProjectTitle { get; }

        bool IsBusy { get; }

        ReadyToCompile IsReadyToCompile { get; }

        bool IsProjectUpdated { get; set; }

        bool HasStaleOutputs { get; set; }

        DateTimeOffset ProjectStart { get; set; }

        DateTimeOffset Today { get; set; }

        IDisplaySettingsViewModel DisplaySettingsViewModel { get; }

        bool DefaultShowDates { get; set; }

        bool DefaultUseClassicDates { get; set; }

        bool DefaultUseBusinessDays { get; set; }

        bool AutoCompile { get; set; }

        string SelectedTheme { get; set; }

        BaseTheme BaseTheme { get; set; }

        ReadOnlyObservableCollection<IManagedActivityViewModel> Activities { get; }

        ArrowGraphSettingsModel ArrowGraphSettings { get; set; }

        ResourceSettingsModel ResourceSettings { get; set; }

        WorkStreamSettingsModel WorkStreamSettings { get; set; }

        bool HasActivities { get; }

        bool HasResources { get; }

        bool HasWorkStreams { get; }

        bool HasPhases { get; }

        bool HasCompilationErrors { get; }

        IGraphCompilation<int, int, int, IDependentActivity> GraphCompilation { get; }

        ArrowGraphModel ArrowGraph { get; }

        ResourceSeriesSetModel ResourceSeriesSet { get; }

        TrackingSeriesSetModel TrackingSeriesSet { get; }

        int? CyclomaticComplexity { get; }

        int? Duration { get; }

        int TrackerIndex { get; set; }

        ReadyToRevise IsReadyToReviseTrackers { get; set; }

        ReadyToRevise IsReadyToReviseSettings { get; set; }

        void ClearSettings();

        void ResetProject();

        void ProcessProjectImport(ProjectImportModel projectImportModel);

        void ProcessProjectPlan(ProjectPlanModel projectPlanModel);

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

        void BuildCyclomaticComplexity();

        void BuildArrowGraph();

        void BuildResourceSeriesSet();

        void BuildTrackingSeriesSet();
    }
}