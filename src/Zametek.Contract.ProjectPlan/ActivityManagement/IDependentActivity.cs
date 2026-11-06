using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Contract.ProjectPlan
{
    public interface IDependentActivity
        : IDependentActivity<int, int, int>
    {
        int DisplayOrder { get; set; }

        bool HasNoRisk { get; set; }

        // The three estimates of the duration. Duration (from IActivity) stays what it was: the
        // duration that the compiler schedules. For an activity with estimates it is their PERT
        // expected duration, rounded to a whole day, and the managed activity keeps it so.
        //
        // The most likely duration is the one the user sets. The optimistic and the pessimistic
        // durations that the user gave are held as they were given, and null means the user gave
        // none, so the activity takes ML in its place.
        int MostLikelyDuration { get; set; }

        int? OwnOptimisticDuration { get; set; }

        int? OwnPessimisticDuration { get; set; }

        bool OverrideColor { get; set; }

        ColorFormatModel ColorFormat { get; set; }

        List<ActivityTrackerModel> Trackers { get; }
    }
}
