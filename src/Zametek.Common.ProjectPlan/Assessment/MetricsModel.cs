namespace Zametek.Common.ProjectPlan
{
    public record MetricsModel
    {
        public double? CriticalityRisk { get; init; }

        public double? FibonacciRisk { get; init; }

        public double? ActivityRisk { get; init; }

        public double? ActivityRiskWithStdDevCorrection { get; init; }

        public double? GeometricCriticalityRisk { get; init; }

        public double? GeometricFibonacciRisk { get; init; }

        public double? GeometricActivityRisk { get; init; }

        public int? CyclomaticComplexity { get; init; }

        public int? Duration { get; init; }

        public double? DurationManMonths { get; init; }

        public double? DirectCost { get; init; }

        public double? IndirectCost { get; init; }

        public double? OtherCost { get; init; }

        public double? TotalCost { get; init; }

        public double? DirectBilling { get; init; }

        public double? IndirectBilling { get; init; }

        public double? OtherBilling { get; init; }

        public double? TotalBilling { get; init; }

        public double? DirectMargin { get; init; }

        public double? IndirectMargin { get; init; }

        public double? OtherMargin { get; init; }

        public double? TotalMargin { get; init; }

        public double? DirectMarginAbsolute { get; init; }

        public double? IndirectMarginAbsolute { get; init; }

        public double? OtherMarginAbsolute { get; init; }

        public double? TotalMarginAbsolute { get; init; }

        public double? DirectEffort { get; init; }

        public double? IndirectEffort { get; init; }

        public double? OtherEffort { get; init; }

        public double? TotalEffort { get; init; }

        public double? ActivityEffort { get; init; }

        public double? Efficiency { get; init; }
    }
}
