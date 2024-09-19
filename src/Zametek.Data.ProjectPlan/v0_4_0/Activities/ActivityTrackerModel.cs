namespace Zametek.Data.ProjectPlan.v0_4_0
{
    [Serializable]
    public record ActivityTrackerModel
    {
        public int Time { get; init; }

        public int ActivityId { get; init; }

        public int PercentageComplete { get; init; }
    }
}
