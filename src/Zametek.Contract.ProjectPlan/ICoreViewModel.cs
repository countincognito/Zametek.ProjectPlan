using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Contract.ProjectPlan
{
    public interface ICoreViewModel
        : IPropertyChangedPubSubViewModel
    {
        bool IsBusy { get; set; }

        DateTime ProjectStart { get; set; }

        bool IsProjectUpdated { get; set; }

        bool ShowDates { get; set; }

        bool UseBusinessDays { get; set; }

        bool HasStaleOutputs { get; set; }

        bool AutoCompile { get; set; }

        bool HasCompilationErrors { get; set; }

        IGraphCompilation<int, int, IDependentActivity<int, int>> GraphCompilation { get; set; }

        string CompilationOutput { get; set; }

        ArrowGraphModel ArrowGraph { get; set; }

        ObservableCollection<IManagedActivityViewModel> Activities { get; }

        ResourceSeriesSetModel ResourceSeriesSet { get; }

        ArrowGraphSettingsModel ArrowGraphSettings { get; }

        ResourceSettingsModel ResourceSettings { get; }

        MetricsModel Metrics { get; set; }

        IApplicationCommands ApplicationCommands { get; }

        int? CyclomaticComplexity { get; set; }

        int? Duration { get; set; }

        double? DurationManMonths { get; set; }

        double? DirectCost { get; set; }

        double? IndirectCost { get; set; }

        double? OtherCost { get; set; }

        double? TotalCost { get; set; }

        double? Efficiency { get; }

        public CoreStateModel CoreState { get; }

        void RecordCoreState();

        void RecordRedoUndo(Action action);

        void ClearUndoStack();

        void ClearRedoStack();

        void AddManagedActivity();

        void AddManagedActivities(HashSet<DependentActivityModel> dependentActivities);

        void RemoveManagedActivities(HashSet<int> dependentActivities);

        void ClearManagedActivities();

        void UpdateArrowGraphSettings(ArrowGraphSettingsModel arrowGraphSettings);

        void UpdateResourceSettings(ResourceSettingsModel resourceSettings);

        void UpdateActivitiesTargetResourceDependencies();

        void UpdateActivitiesAllocatedToResources();

        void UpdateActivitiesProjectStart();

        void UpdateActivitiesUseBusinessDays();

        int RunCalculateResourcedCyclomaticComplexity();

        double CalculateDurationManMonths();

        void RunCompile();

        void RunAutoCompile();

        void RunTransitiveReduction();

        void SetCompilationOutput();

        void CalculateResourceSeriesSet();

        void ClearResourceSeriesSet();

        void CalculateCosts();

        void ClearCosts();

        void ClearSettings();
    }
}
