using Zametek.Maths.Graphs;

namespace Zametek.Data.ProjectPlan.v0_6_2
{
    [Serializable]
    public record ActivityModel
    {
        public int Id { get; init; }

        public int DisplayOrder { get; init; }

        public string? Name { get; init; }

        public string? Notes { get; init; }

        public List<int> TargetWorkStreams { get; init; } = [];

        public List<int> TargetResources { get; init; } = [];

        public LogicalOperator TargetResourceOperator { get; init; }

        public List<int> AllocatedToResources { get; init; } = [];

        public bool CanBeRemoved { get; init; }

        public bool HasNoCost { get; init; }

        public bool HasNoBilling { get; init; }

        public bool HasNoEffort { get; init; }

        public bool HasNoRisk { get; init; }

        // The duration that the compiler schedules: the PERT expected duration of the estimates,
        // rounded to a whole day.
        public int Duration { get; init; }

        // The three estimates of the duration. A member that is null follows the one before it:
        // no most likely duration is the Duration, and no optimistic or pessimistic duration is
        // the most likely duration. They are new in v0.6.2.
        public int? MostLikelyDuration { get; init; }

        public int? OptimisticDuration { get; init; }

        public int? PessimisticDuration { get; init; }

        public int? FreeSlack { get; init; }

        public int? EarliestStartTime { get; init; }

        public int? LatestFinishTime { get; init; }

        public int? MinimumFreeSlack { get; init; }

        public int? MinimumEarliestStartTime { get; init; }

        public DateTimeOffset? MinimumEarliestStartDateTime { get; init; }

        public int? MaximumLatestFinishTime { get; init; }

        public DateTimeOffset? MaximumLatestFinishDateTime { get; init; }

        public bool OverrideColor { get; init; }

        public v0_1_0.ColorFormatModel ColorFormat { get; init; } = new v0_1_0.ColorFormatModel();

        public List<v0_4_0.ActivityTrackerModel> Trackers { get; init; } = [];
    }
}
