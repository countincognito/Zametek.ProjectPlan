using Zametek.Maths.Graphs;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ActivityModel
    {
        public int Id { get; init; }

        public int DisplayOrder { get; init; }

        public string Name { get; init; } = string.Empty;

        public string Notes { get; init; } = string.Empty;

        public List<int> TargetWorkStreams { get; init; } = [];

        public List<int> TargetResources { get; init; } = [];

        public LogicalOperator TargetResourceOperator { get; init; }

        public List<int> AllocatedToResources { get; init; } = [];

        public bool CanBeRemoved { get; init; }

        public bool HasNoCost { get; init; }

        public bool HasNoBilling { get; init; }

        public bool HasNoEffort { get; init; }

        public bool HasNoRisk { get; init; }

        // The duration that the compiler schedules. When an activity has estimates, this is
        // the PERT expected duration of them, rounded to a whole day. Everything that reads
        // a compiled activity reads this member, and thus it keeps its meaning.
        public int Duration { get; init; }

        // The three estimates of the duration. A member that is null follows the one before
        // it: no most likely duration is the Duration, and no optimistic or pessimistic
        // duration is the most likely duration. Thus a model that sets only Duration, such
        // as one from an older file or an import, is an activity with no uncertainty.
        public int? MostLikelyDuration { get; init; }

        public int? OptimisticDuration { get; init; }

        public int? PessimisticDuration { get; init; }

        public int? FreeSlack { get; init; }

        public int? TotalSlack { get; init; }

        public int? EarliestStartTime { get; init; }

        public int? LatestStartTime { get; init; }

        public int? EarliestFinishTime { get; init; }

        public int? LatestFinishTime { get; init; }

        public int? MinimumFreeSlack { get; init; }

        public int? MinimumEarliestStartTime { get; init; }

        public DateTimeOffset? MinimumEarliestStartDateTime { get; init; }

        public int? MaximumLatestFinishTime { get; init; }

        public DateTimeOffset? MaximumLatestFinishDateTime { get; init; }

        public bool OverrideColor { get; init; }

        public ColorFormatModel ColorFormat { get; init; } = new ColorFormatModel();

        public List<ActivityTrackerModel> Trackers { get; init; } = [];
    }
}
