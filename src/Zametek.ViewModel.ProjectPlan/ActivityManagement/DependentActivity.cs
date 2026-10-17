using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    public class DependentActivity
        : DependentActivity<int, int, int>, IDependentActivity
    {
        public DependentActivity(int id, int duration, bool canBeRemoved)
            : base(id, duration, canBeRemoved)
        {
            MostLikelyDuration = duration;
            ColorFormat = ColorHelper.None();
            Trackers = [];
        }

        // The three estimates come last, and they are optional: an activity with no most likely duration has
        // the duration it is given, and so the same call as before the estimates gives the same activity.
        public DependentActivity(int id, int displayOrder, string name, string notes, IEnumerable<int> targetWorkStreams, IEnumerable<int> targetResources, IEnumerable<int> dependencies, IEnumerable<int> planningDependencies, IEnumerable<int> resourceDependencies, IEnumerable<int> successors, LogicalOperator targetLogicalOperator, IEnumerable<int> allocatedToResources, bool canBeRemoved, bool hasNoCost, bool hasNoBilling, bool hasNoEffort, bool hasNoRisk, int duration, int? freeSlack, int? earliestStartTime, int? latestFinishTime, int? minimumFreeSlack, int? minimumEarliestStartTime, int? maximumLatestFinishTime, bool overrideColor, ColorFormatModel colorFormat, IEnumerable<ActivityTrackerModel> trackers, int? mostLikelyDuration = null, int? ownOptimisticDuration = null, int? ownPessimisticDuration = null)
            : base(id, name, notes, targetWorkStreams, targetResources, dependencies, planningDependencies, resourceDependencies, successors, targetLogicalOperator, allocatedToResources, canBeRemoved, hasNoCost, hasNoBilling, hasNoEffort, duration, freeSlack, earliestStartTime, latestFinishTime, minimumFreeSlack, minimumEarliestStartTime, maximumLatestFinishTime)
        {
            DisplayOrder = displayOrder;
            HasNoRisk = hasNoRisk;
            MostLikelyDuration = mostLikelyDuration ?? duration;
            OwnOptimisticDuration = ownOptimisticDuration;
            OwnPessimisticDuration = ownPessimisticDuration;
            OverrideColor = overrideColor;
            ColorFormat = colorFormat;
            Trackers = [.. trackers];
        }

        public int DisplayOrder { get; set; }

        public bool HasNoRisk { get; set; }

        public int MostLikelyDuration { get; set; }

        public int? OwnOptimisticDuration { get; set; }

        public int? OwnPessimisticDuration { get; set; }

        public bool OverrideColor { get; set; }

        public ColorFormatModel ColorFormat { get; set; }

        public List<ActivityTrackerModel> Trackers { get; init; }

        public override object CloneObject()
        {
            return new DependentActivity(Id, DisplayOrder, Name ?? string.Empty, Notes ?? string.Empty, TargetWorkStreams, TargetResources, Dependencies, PlanningDependencies, ResourceDependencies, Successors, TargetResourceOperator, AllocatedToResources, CanBeRemoved, HasNoCost, HasNoBilling, HasNoEffort, HasNoRisk, Duration, FreeSlack, EarliestStartTime, LatestFinishTime, MinimumFreeSlack, MinimumEarliestStartTime, MaximumLatestFinishTime, OverrideColor, ColorFormat, Trackers, MostLikelyDuration, OwnOptimisticDuration, OwnPessimisticDuration);
        }
    }
}
