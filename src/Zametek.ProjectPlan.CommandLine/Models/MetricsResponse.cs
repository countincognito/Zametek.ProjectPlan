using Zametek.Engine.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    // A compiled project's metrics as zpp serve's API gives them: named in words, in camelCase, and as values rather than
    // as the text zpp prints - nothing here is written in the server's culture. It is the API's own document, which zpp's
    // --metrics-format json is not: that keeps the names and the display strings it was released with.
    public record MetricsResponse
    {
        public double? ActivityRisk { get; init; }

        public double? ActivityRiskWithStandardDeviationCorrection { get; init; }

        public double? CriticalityRisk { get; init; }

        public double? FibonacciRisk { get; init; }

        public double? GeometricActivityRisk { get; init; }

        public double? GeometricCriticalityRisk { get; init; }

        public double? GeometricFibonacciRisk { get; init; }

        public int? NetworkCyclomaticComplexity { get; init; }

        // In days, as is the project's finish.
        public int? NetworkDuration { get; init; }

        public double? NetworkDurationManMonths { get; init; }

        // The finish as the days to it from the project's start - the network's duration - and as the date it falls on, in
        // the working calendar the project keeps. Neither has a value when the project has no duration.
        public int? ProjectFinishDays { get; init; }

        public DateOnly? ProjectFinishDate { get; init; }

        public double? EffortEfficiency { get; init; }

        public double? ActivityEffort { get; init; }

        public double? DirectEffort { get; init; }

        public double? IndirectEffort { get; init; }

        public double? OtherEffort { get; init; }

        public double? TotalEffort { get; init; }

        // The costs, billings and margins are estimates the project works out in floating point, in the unit it keeps its
        // figures in - which has no currency.
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

        public static MetricsResponse From(JobMetrics metrics)
        {
            ArgumentNullException.ThrowIfNull(metrics);

            return new MetricsResponse
            {
                ActivityRisk = metrics.ActivityRisk,
                ActivityRiskWithStandardDeviationCorrection = metrics.ActivityRiskWithStdDevCorrection,
                CriticalityRisk = metrics.CriticalityRisk,
                FibonacciRisk = metrics.FibonacciRisk,
                GeometricActivityRisk = metrics.GeometricActivityRisk,
                GeometricCriticalityRisk = metrics.GeometricCriticalityRisk,
                GeometricFibonacciRisk = metrics.GeometricFibonacciRisk,
                NetworkCyclomaticComplexity = metrics.NetworkCyclomaticComplexity,
                NetworkDuration = metrics.NetworkDuration,
                NetworkDurationManMonths = metrics.NetworkDurationManMonths,
                ProjectFinishDays = metrics.ProjectFinishDays,
                ProjectFinishDate = metrics.ProjectFinishDate,
                EffortEfficiency = metrics.EffortEfficiency,
                ActivityEffort = metrics.ActivityEffort,
                DirectEffort = metrics.DirectEffort,
                IndirectEffort = metrics.IndirectEffort,
                OtherEffort = metrics.OtherEffort,
                TotalEffort = metrics.TotalEffort,
                DirectCost = metrics.DirectCost,
                IndirectCost = metrics.IndirectCost,
                OtherCost = metrics.OtherCost,
                TotalCost = metrics.TotalCost,
                DirectBilling = metrics.DirectBilling,
                IndirectBilling = metrics.IndirectBilling,
                OtherBilling = metrics.OtherBilling,
                TotalBilling = metrics.TotalBilling,
                DirectMargin = metrics.DirectMargin,
                IndirectMargin = metrics.IndirectMargin,
                OtherMargin = metrics.OtherMargin,
                TotalMargin = metrics.TotalMargin,
                DirectMarginAbsolute = metrics.DirectMarginAbsolute,
                IndirectMarginAbsolute = metrics.IndirectMarginAbsolute,
                OtherMarginAbsolute = metrics.OtherMarginAbsolute,
                TotalMarginAbsolute = metrics.TotalMarginAbsolute,
            };
        }
    }
}
