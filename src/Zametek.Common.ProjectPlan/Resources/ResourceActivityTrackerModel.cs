namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ResourceActivityTrackerModel
    {
        public int Time { get; init; }

        public int ResourceId { get; init; }

        public int ActivityId { get; init; }

        public string ActivityName { get; init; } = string.Empty;

        public int PercentageWorked { get; init; }
    }
}
