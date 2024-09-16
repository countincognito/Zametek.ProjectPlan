namespace Zametek.Data.ProjectPlan.v0_4_0
{
    [Serializable]
    public record ResourceTrackerModel
    {
        public int Index { get; init; }

        public int Time { get; init; }

        public int ResourceId { get; init; }

        public int PercentageComplete { get; init; }
    }
}
