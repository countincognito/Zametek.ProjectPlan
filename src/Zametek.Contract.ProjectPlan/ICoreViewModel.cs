using System.Collections.ObjectModel;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Contract.ProjectPlan
{
    public interface ICoreViewModel
    {
        string ProjectTitle { get; set; }

        bool IsBusy { get; }

        bool IsProjectUpdated { get; set; }

        bool HasStaleOutputs { get; set; }

        DateTimeOffset ProjectStart { get; set; }

        DateTime ProjectStartDateTime { get; set; }

        TimeSpan ProjectStartTimeOffset { get; }

        bool ShowDates { get; set; }

        bool UseBusinessDays { get; set; }

        bool ViewEarnedValueProjections { get; set; }

        bool AutoCompile { get; set; }

        ReadOnlyObservableCollection<IManagedActivityViewModel> Activities { get; }

        ArrowGraphSettingsModel ArrowGraphSettings { get; set; }

        ResourceSettingsModel ResourceSettings { get; set; }

        bool HasCompilationErrors { get; }

        IGraphCompilation<int, int, IDependentActivity<int, int>> GraphCompilation { get; }

        ArrowGraphModel ArrowGraph { get; }

        ResourceSeriesSetModel ResourceSeriesSet { get; }

        TrackingSeriesSetModel TrackingSeriesSet { get; }

        int? CyclomaticComplexity { get; }

        int? Duration { get; }

        void ClearSettings();

        void ResetProject();

        void ProcessProjectImport(ProjectImportModel projectImportModel);

        void ProcessProjectPlan(ProjectPlanModel projectPlanModel);

        ProjectPlanModel BuildProjectPlan();

        void AddManagedActivity();

        void AddManagedActivities(IEnumerable<DependentActivityModel> dependentActivityModels);

        void RemoveManagedActivities(IEnumerable<int> dependentActivities);

        void ClearManagedActivities();

        void AddTrackers();

        void RemoveTrackers();

        void RunCompile();

        void RunAutoCompile();

        void RunTransitiveReduction();
    }
}