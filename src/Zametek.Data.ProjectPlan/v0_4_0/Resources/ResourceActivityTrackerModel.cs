namespace Zametek.Data.ProjectPlan.v0_4_0
{
    [Serializable]
    public record ResourceActivityTrackerModel
    {
        public int Time { get; init; }

        public int ResourceId { get; init; }

        public int ActivityId { get; init; }

        public int PercentageWorked { get; init; }
    }
}
