namespace Zametek.Data.ProjectPlan.v0_2_1
{
    [Serializable]
    public record ScheduledActivityModel
    {
        public int Id { get; init; }

        public string? Name { get; init; }

        public bool HasNoCost { get; init; }

        public int Duration { get; init; }

        public int StartTime { get; init; }

        public int FinishTime { get; init; }
    }
}
