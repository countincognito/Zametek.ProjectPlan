using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    public class DependentActivity
        : DependentActivity<int, int, int>, IDependentActivity
    {
        public DependentActivity(int id, int duration)
            : base(id, duration)
        {
            Trackers = [];
        }

        public DependentActivity(int id, int duration, bool canBeRemoved)
            : base(id, duration, canBeRemoved)
        {
            Trackers = [];
        }

        public DependentActivity(int id, int duration, IEnumerable<int> dependencies)
            : base(id, duration, dependencies)
        {
            Trackers = [];
        }

        public DependentActivity(int id, int duration, IEnumerable<int> dependencies, IEnumerable<int> planningDependencies)
            : base(id, duration, dependencies, planningDependencies)
        {
            Trackers = [];
        }

        public DependentActivity(int id, string name, string notes, IEnumerable<int> targetWorkStreams, IEnumerable<int> targetResources, IEnumerable<int> dependencies, IEnumerable<int> planningDependencies, IEnumerable<int> resourceDependencies, IEnumerable<int> successors, LogicalOperator targetLogicalOperator, IEnumerable<int> allocatedToResources, bool canBeRemoved, bool hasNoCost, bool hasNoBilling, bool hasNoEffort, bool hasNoRisk, int duration, int? freeSlack, int? earliestStartTime, int? latestFinishTime, int? minimumFreeSlack, int? minimumEarliestStartTime, int? maximumLatestFinishTime)
            : base(id, name, notes, targetWorkStreams, targetResources, dependencies, planningDependencies, resourceDependencies, successors, targetLogicalOperator, allocatedToResources, canBeRemoved, hasNoCost, hasNoBilling, hasNoEffort, duration, freeSlack, earliestStartTime, latestFinishTime, minimumFreeSlack, minimumEarliestStartTime, maximumLatestFinishTime)
        {
            HasNoRisk = hasNoRisk;
            Trackers = [];
        }


        public DependentActivity(int id, string name, string notes, IEnumerable<int> targetWorkStreams, IEnumerable<int> targetResources, IEnumerable<int> dependencies, IEnumerable<int> planningDependencies, IEnumerable<int> resourceDependencies, IEnumerable<int> successors, LogicalOperator targetLogicalOperator, IEnumerable<int> allocatedToResources, bool canBeRemoved, bool hasNoCost, bool hasNoBilling, bool hasNoEffort, bool hasNoRisk, int duration, int? freeSlack, int? earliestStartTime, int? latestFinishTime, int? minimumFreeSlack, int? minimumEarliestStartTime, int? maximumLatestFinishTime, IEnumerable<ActivityTrackerModel> trackers)
            : base(id, name, notes, targetWorkStreams, targetResources, dependencies, planningDependencies, resourceDependencies, successors, targetLogicalOperator, allocatedToResources, canBeRemoved, hasNoCost, hasNoBilling, hasNoEffort, duration, freeSlack, earliestStartTime, latestFinishTime, minimumFreeSlack, minimumEarliestStartTime, maximumLatestFinishTime)
        {
            ArgumentNullException.ThrowIfNull(nameof(trackers));
            HasNoRisk = hasNoRisk;
            Trackers = [.. trackers];
        }

        public bool HasNoRisk { get; set; }

        public List<ActivityTrackerModel> Trackers { get; init; }

        public override object CloneObject()
        {
            return new DependentActivity(Id, Name, Notes, TargetWorkStreams, TargetResources, Dependencies, PlanningDependencies, ResourceDependencies, Successors, TargetResourceOperator, AllocatedToResources, CanBeRemoved, HasNoCost, HasNoBilling, HasNoEffort, HasNoRisk, Duration, FreeSlack, EarliestStartTime, LatestFinishTime, MinimumFreeSlack, MinimumEarliestStartTime, MaximumLatestFinishTime, Trackers);
        }
    }
}
