namespace Zametek.Data.ProjectPlan.v0_5_0
{
    [Serializable]
    public record NetworkModel
    {
        public int? CyclomaticComplexity { get; init; }

        public int? Duration { get; init; }

        public double? DurationManMonths { get; init; }
    }
}
