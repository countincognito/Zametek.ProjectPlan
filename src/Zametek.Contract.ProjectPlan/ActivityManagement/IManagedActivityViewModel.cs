using System.ComponentModel;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IManagedActivityViewModel
        : IDependentActivity, IDisposable, INotifyPropertyChanged, IStartSubscriptions, IKillSubscriptions, IMuteEdits
    {
        // The optimistic and the pessimistic duration that the activity has: the ones the user gave
        // it, and where the user gave none, its most likely duration. The rule is
        // 0 <= O <= ML <= P, and 0 and 0 when ML is 0. A value outside the rule is moved to the
        // nearest one inside it, and the activity reports an error for that property.
        int OptimisticDuration { get; set; }

        int PessimisticDuration { get; set; }

        // The PERT standard deviation of the estimates, (P - O) / 6.
        double StandardDeviation { get; }

        // Whether the user gave the activity an optimistic or a pessimistic duration of its own.
        bool HasOwnEstimates { get; }

        // Takes the optimistic and the pessimistic duration back to the most likely duration, so
        // that they follow it again.
        void ResetEstimates();

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

        // Likewise for the date-derived compiler inputs. The minimum earliest start time
        // and maximum latest finish time the compiler reads are integers measured from
        // the project start along the working calendar, so a change to either invalidates
        // them; these two recalculate them where the change is made, before the same
        // caller arms a compile.
        void SetProjectStart(DateTimeOffset projectStart);

        void UpdateEarliestStartAndLatestFinishDateTimes();

        // The two halves of a compilation: CloneObject (from IHaveCloneableObject) hands
        // the compiler an independent copy of this activity, and this applies the results
        // back once it has finished, so the compiler never works on live state.
        void SetCompiledValues(IDependentActivity compiledActivity);

        DependentActivityModel DeepCopy();
    }
}
