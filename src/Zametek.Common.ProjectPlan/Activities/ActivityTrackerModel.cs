namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ActivityTrackerModel
    {
        public int Time { get; init; }

        public int ActivityId { get; init; }

        public int PercentageComplete { get; init; }
    }
}
