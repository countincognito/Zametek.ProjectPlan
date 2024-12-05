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

        DateTime ProjectStartDateTime { get; set; }

        TimeSpan ProjectStartTimeOffset { get; }

        bool ShowDates { get; set; }

        bool UseClassicDates { get; set; }

        bool UseBusinessDays { get; set; }

        bool ViewEarnedValueProjections { get; set; }

        GroupByMode GanttChartGroupByMode { get; set; }

        AnnotationStyle GanttChartAnnotationStyle { get; set; }

        bool ViewGanttChartGroupLabels { get; set; }

        bool ViewGanttChartProjectFinish { get; set; }

        bool ViewGanttChartTracking { get; set; }

        bool AutoCompile { get; set; }

        string SelectedTheme { get; set; }

        BaseTheme BaseTheme { get; set; }

        ReadOnlyObservableCollection<IManagedActivityViewModel> Activities { get; }

        ArrowGraphSettingsModel ArrowGraphSettings { get; set; }

        ResourceSettingsModel ResourceSettings { get; set; }

        WorkStreamSettingsModel WorkStreamSettings { get; set; }

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