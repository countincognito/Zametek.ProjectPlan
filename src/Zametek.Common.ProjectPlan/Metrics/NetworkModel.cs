namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record NetworkModel
    {
        public int? CyclomaticComplexity { get; init; }

        public int? Duration { get; init; }

        public double? DurationManMonths { get; init; }
    }
}
