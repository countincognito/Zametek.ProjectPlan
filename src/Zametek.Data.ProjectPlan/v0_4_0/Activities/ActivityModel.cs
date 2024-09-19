using Zametek.Maths.Graphs;

namespace Zametek.Data.ProjectPlan.v0_4_0
{
    [Serializable]
    public record ActivityModel
    {
        public int Id { get; init; }

        public string? Name { get; init; }

        public string? Notes { get; init; }

        public List<int> TargetWorkStreams { get; init; } = [];

        public List<int> TargetResources { get; init; } = [];

        public LogicalOperator TargetResourceOperator { get; init; }

        public List<int> AllocatedToResources { get; init; } = [];

        public bool CanBeRemoved { get; init; }

        public bool HasNoCost { get; init; }

        public int Duration { get; init; }

        public int? FreeSlack { get; init; }

        public int? EarliestStartTime { get; init; }

        public int? LatestFinishTime { get; init; }

        public int? MinimumFreeSlack { get; init; }

        public int? MinimumEarliestStartTime { get; init; }

        public DateTimeOffset? MinimumEarliestStartDateTime { get; init; }

        public int? MaximumLatestFinishTime { get; init; }

        public DateTimeOffset? MaximumLatestFinishDateTime { get; init; }

        public List<ActivityTrackerModel> Trackers { get; init; } = [];
    }
}
