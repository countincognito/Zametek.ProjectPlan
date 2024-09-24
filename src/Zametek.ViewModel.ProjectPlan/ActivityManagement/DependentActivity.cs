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

        public DependentActivity(int id, string name, string notes, IEnumerable<int> targetWorkStreams, IEnumerable<int> targetResources, IEnumerable<int> dependencies, IEnumerable<int> resourceDependencies, LogicalOperator targetLogicalOperator, IEnumerable<int> allocatedToResources, bool canBeRemoved, bool hasNoCost, int duration, int? freeSlack, int? earliestStartTime, int? latestFinishTime, int? minimumFreeSlack, int? minimumEarliestStartTime, int? maximumLatestFinishTime)
            : base(id, name, notes, targetWorkStreams, targetResources, dependencies, resourceDependencies, targetLogicalOperator, allocatedToResources, canBeRemoved, hasNoCost, duration, freeSlack, earliestStartTime, latestFinishTime, minimumFreeSlack, minimumEarliestStartTime, maximumLatestFinishTime)
        {
            Trackers = [];
        }


        public DependentActivity(int id, string name, string notes, IEnumerable<int> targetWorkStreams, IEnumerable<int> targetResources, IEnumerable<int> dependencies, IEnumerable<int> resourceDependencies, LogicalOperator targetLogicalOperator, IEnumerable<int> allocatedToResources, bool canBeRemoved, bool hasNoCost, int duration, int? freeSlack, int? earliestStartTime, int? latestFinishTime, int? minimumFreeSlack, int? minimumEarliestStartTime, int? maximumLatestFinishTime, IEnumerable<ActivityTrackerModel> trackers)
            : base(id, name, notes, targetWorkStreams, targetResources, dependencies, resourceDependencies, targetLogicalOperator, allocatedToResources, canBeRemoved, hasNoCost, duration, freeSlack, earliestStartTime, latestFinishTime, minimumFreeSlack, minimumEarliestStartTime, maximumLatestFinishTime)
        {
            ArgumentNullException.ThrowIfNull(nameof(trackers));
            Trackers = [.. trackers];
        }

        public List<ActivityTrackerModel> Trackers { get; init; }

        public override object CloneObject()
        {
            return new DependentActivity(Id, Name, Notes, TargetWorkStreams, TargetResources, Dependencies, ResourceDependencies, TargetResourceOperator, AllocatedToResources, CanBeRemoved, HasNoCost, Duration, FreeSlack, EarliestStartTime, LatestFinishTime, MinimumFreeSlack, MinimumEarliestStartTime, MaximumLatestFinishTime, Trackers);
        }
    }
}
