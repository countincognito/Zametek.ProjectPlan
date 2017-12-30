using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Zametek.Common.Project;
using Zametek.Maths.Graphs;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public interface ICoreViewModel
        : IPropertyChangedPubSubViewModel
    {
        DateTime ProjectStart
        {
            get;
            set;
        }

        bool IsProjectUpdated
        {
            get;
            set;
        }

        bool ShowDates
        {
            get;
            set;
        }

        bool UseBusinessDays
        {
            get;
            set;
        }

        bool HasStaleOutputs
        {
            get;
            set;
        }

        bool AutoCompile
        {
            get;
            set;
        }

        bool HasCompilationErrors
        {
            get;
            set;
        }

        GraphCompilation<int, IDependentActivity<int>> GraphCompilation
        {
            get;
            set;
        }

        string CompilationOutput
        {
            get;
            set;
        }

        ArrowGraphDto ArrowGraphDto
        {
            get;
            set;
        }

        ObservableCollection<ManagedActivityViewModel> Activities
        {
            get;
        }

        ArrowGraphSettingsDto ArrowGraphSettingsDto
        {
            get;
            set;
        }

        ResourceSettingsDto ResourceSettingsDto
        {
            get;
            set;
        }

        int? CyclomaticComplexity
        {
            get;
            set;
        }

        int? Duration
        {
            get;
            set;
        }

        double? DirectCost
        {
            get;
            set;
        }

        double? IndirectCost
        {
            get;
            set;
        }

        double? OtherCost
        {
            get;
            set;
        }

        double? TotalCost
        {
            get;
            set;
        }

        void AddManagedActivity();

        void AddManagedActivity(IDependentActivity<int> dependentActivity);

        void RemoveManagedActivities(HashSet<int> dependentActivities);

        void ClearManagedActivities();

        void UpdateActivitiesTargetResources();

        void UpdateActivitiesTargetResourceDependencies();

        void UpdateActivitiesProjectStart();

        void UpdateActivitiesUseBusinessDays();

        void RunCompile();

        void RunAutoCompile();

        void SetCompilationOutput();

        void ClearSettings();
    }
}
