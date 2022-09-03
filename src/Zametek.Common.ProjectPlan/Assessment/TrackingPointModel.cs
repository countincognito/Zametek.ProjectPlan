namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record TrackingPointModel
    {
        public int Time { get; init; }

        public int ActivityId { get; init; }

        public string ActivityName { get; init; } = string.Empty;

        public double Value { get; set; }

        public double ValuePercentage { get; set; }
    }
}
