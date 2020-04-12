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

        ArrowGraphSettingsModel ArrowGraphSettings { get; set; }

        ResourceSettingsModel ResourceSettings { get; set; }

        IApplicationCommands ApplicationCommands { get; }

        int? CyclomaticComplexity { get; set; }

        int? Duration { get; set; }

        double? DirectCost { get; set; }

        double? IndirectCost { get; set; }

        double? OtherCost { get; set; }

        double? TotalCost { get; set; }

        void RecordRedoUndo(Action action);

        DependentActivityModel AddManagedActivity();

        HashSet<DependentActivityModel> AddManagedActivities(HashSet<DependentActivityModel> dependentActivities);

        HashSet<DependentActivityModel> RemoveManagedActivities(HashSet<int> dependentActivities);

        void ClearManagedActivities();

        void UpdateResourceSettings(ResourceSettingsModel resourceSettings);

        void UpdateActivitiesTargetResourceDependencies();

        void UpdateActivitiesAllocatedToResources();

        void UpdateActivitiesProjectStart();

        void UpdateActivitiesUseBusinessDays();

        int RunCalculateResourcedCyclomaticComplexity();

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
