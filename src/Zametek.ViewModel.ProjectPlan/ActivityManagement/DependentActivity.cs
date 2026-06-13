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
            ColorFormat = ColorHelper.None();
            Trackers = [];
        }

        public DependentActivity(int id, int displayOrder, string name, string notes, IEnumerable<int> targetWorkStreams, IEnumerable<int> targetResources, IEnumerable<int> dependencies, IEnumerable<int> planningDependencies, IEnumerable<int> resourceDependencies, IEnumerable<int> successors, LogicalOperator targetLogicalOperator, IEnumerable<int> allocatedToResources, bool canBeRemoved, bool hasNoCost, bool hasNoBilling, bool hasNoEffort, bool hasNoRisk, int duration, int? freeSlack, int? earliestStartTime, int? latestFinishTime, int? minimumFreeSlack, int? minimumEarliestStartTime, int? maximumLatestFinishTime, bool overrideColor, ColorFormatModel colorFormat, IEnumerable<ActivityTrackerModel> trackers)
            : base(id, name, notes, targetWorkStreams, targetResources, dependencies, planningDependencies, resourceDependencies, successors, targetLogicalOperator, allocatedToResources, canBeRemoved, hasNoCost, hasNoBilling, hasNoEffort, duration, freeSlack, earliestStartTime, latestFinishTime, minimumFreeSlack, minimumEarliestStartTime, maximumLatestFinishTime)
        {
            DisplayOrder = displayOrder;
            HasNoRisk = hasNoRisk;
            OverrideColor = overrideColor;
            ColorFormat = colorFormat;
            Trackers = [.. trackers];
        }

        public int DisplayOrder { get; set; }

        public bool HasNoRisk { get; set; }

        public bool OverrideColor { get; set; }

        public ColorFormatModel ColorFormat { get; set; }

        public List<ActivityTrackerModel> Trackers { get; init; }

        public override object CloneObject()
        {
            return new DependentActivity(Id, DisplayOrder, Name, Notes, TargetWorkStreams, TargetResources, Dependencies, PlanningDependencies, ResourceDependencies, Successors, TargetResourceOperator, AllocatedToResources, CanBeRemoved, HasNoCost, HasNoBilling, HasNoEffort, HasNoRisk, Duration, FreeSlack, EarliestStartTime, LatestFinishTime, MinimumFreeSlack, MinimumEarliestStartTime, MaximumLatestFinishTime, OverrideColor, ColorFormat, Trackers);
        }
    }
}
