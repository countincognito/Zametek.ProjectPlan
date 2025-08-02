namespace Zametek.Data.ProjectPlan.v0_4_4
{
    [Serializable]
    public record ScheduledActivityModel
    {
        public int Id { get; init; }

        public string? Name { get; init; }

        public bool HasNoCost { get; init; }

        public bool HasNoBilling { get; init; }

        public bool HasNoEffort { get; init; }

        public int Duration { get; init; }

        public int StartTime { get; init; }

        public int FinishTime { get; init; }
    }
}
