namespace Zametek.Data.ProjectPlan.v0_4_0
{
    [Serializable]
    public record ActivityTrackerModel
    {
        public int Index { get; init; }

        public int Time { get; init; }

        public int ActivityId { get; init; }

        public int PercentageComplete { get; init; }
    }
}
