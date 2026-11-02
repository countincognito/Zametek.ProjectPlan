using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IResourceActivitySelectorViewModel
        : IDisposable
    {
        int ResourceId { get; }

        int Time { get; }

        // Each list is an array that a change replaces as a whole. A revision changes a day on the thread that asks for it, and
        // the UI thread reads the day at the same time (ARCHITECTURE section 7 rule 17).
        IReadOnlyList<ISelectableResourceActivityViewModel> TargetResourceActivities { get; }

        IReadOnlyList<ISelectableResourceActivityViewModel> SelectedTargetResourceActivities { get; }

        IList<int> SelectedResourceActivityIds { get; }

        void SetTargetResourceActivities(IEnumerable<ResourceActivityTrackerModel> targetResourceActivities, HashSet<int> selectedTargetResourceActivities);

        // Books the activity on this day with this percentage, or takes its booking away when there is no percentage. False if
        // the activity is not a target of this day.
        bool SetPercentageWorked(int activityId, int? percentageWorked);
    }
}
