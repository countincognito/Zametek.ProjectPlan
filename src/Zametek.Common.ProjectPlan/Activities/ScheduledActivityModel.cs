namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ScheduledActivityModel
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public bool HasNoCost { get; init; }

        public int Duration { get; init; }

        public int StartTime { get; init; }

        public int FinishTime { get; init; }
    }
}
