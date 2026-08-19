using System.ComponentModel;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IManagedActivityViewModel
        : IDependentActivity, IDisposable, INotifyPropertyChanged, IKillSubscriptions, IMuteEdits
    {
        bool IsIsolated { get; }

        bool IsCompiled { get; }

        bool ShowDates { get; }

        bool HasResources { get; }

        bool HasWorkStreams { get; }

        DateTimeOffset ProjectStart { get; }

        string DependenciesString { get; set; }

        string PlanningDependenciesString { get; set; }

        string ResourceDependenciesString { get; }

        string SuccessorsString { get; }

        string AllocatedToResourcesString { get; }

        DateTimeOffset? EarliestStartDateTimeOffset { get; }

        DateTimeOffset? LatestStartDateTimeOffset { get; }

        DateTimeOffset? EarliestFinishDateTimeOffset { get; }

        DateTimeOffset? LatestFinishDateTimeOffset { get; }

        DateTime? MinimumEarliestStartDateTime { get; set; }

        DateTime? MaximumLatestFinishDateTime { get; set; }

        IResourceSelectorViewModel ResourceSelector { get; }

        IWorkStreamSelectorViewModel WorkStreamSelector { get; }

        IActivityTrackerSetViewModel TrackerSet { get; }

        // Invoked synchronously by the core view model, under its lock, when the
        // corresponding settings change - never deferred to another thread, so the
        // activity's live target sets cannot be mutated while a compile clones them.
        void SetResourceSettings(ResourceSettingsModel resourceSettings);

        void SetWorkStreamSettings(WorkStreamSettingsModel workStreamSettings);

        // The two halves of a compilation: CloneObject (from IHaveCloneableObject) hands
        // the compiler an independent copy of this activity, and this applies the results
        // back once it has finished, so the compiler never works on live state.
        void SetCompiledValues(IDependentActivity compiledActivity);

        DependentActivityModel DeepCopy();
    }
}
