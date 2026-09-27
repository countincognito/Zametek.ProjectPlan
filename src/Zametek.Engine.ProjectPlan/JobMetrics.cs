using Zametek.Contract.ProjectPlan;

namespace Zametek.Engine.ProjectPlan
{
    // A compiled plan's metrics, copied out of the metrics view-model before the job's scope - and the view-model with
    // it - goes. Each value keeps the name and type it has there, so a host formats it exactly as it would have
    // formatted the view-model's.
    public record JobMetrics
    {
        public double? ActivityRisk { get; init; }

        public double? ActivityRiskWithStdDevCorrection { get; init; }

        public double? CriticalityRisk { get; init; }

        public double? FibonacciRisk { get; init; }

        public double? GeometricActivityRisk { get; init; }

        public double? GeometricCriticalityRisk { get; init; }

        public double? GeometricFibonacciRisk { get; init; }

        public int? NetworkCyclomaticComplexity { get; init; }

        public int? NetworkDuration { get; init; }

        public double? NetworkDurationManMonths { get; init; }

        public string ProjectFinish { get; init; } = string.Empty;

        public double? EffortEfficiency { get; init; }

        public double? ActivityEffort { get; init; }

        public double? DirectEffort { get; init; }

        public double? IndirectEffort { get; init; }

        public double? OtherEffort { get; init; }

        public double? TotalEffort { get; init; }

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

        public string DisplayDirectMargin { get; init; } = string.Empty;

        public string DisplayIndirectMargin { get; init; } = string.Empty;

        public string DisplayOtherMargin { get; init; } = string.Empty;

        public string DisplayTotalMargin { get; init; } = string.Empty;

        internal static JobMetrics From(IMetricManagerViewModel metrics)
        {
            ArgumentNullException.ThrowIfNull(metrics);

            return new JobMetrics
            {
                ActivityRisk = metrics.ActivityRisk,
                ActivityRiskWithStdDevCorrection = metrics.ActivityRiskWithStdDevCorrection,
                CriticalityRisk = metrics.CriticalityRisk,
                FibonacciRisk = metrics.FibonacciRisk,
                GeometricActivityRisk = metrics.GeometricActivityRisk,
                GeometricCriticalityRisk = metrics.GeometricCriticalityRisk,
                GeometricFibonacciRisk = metrics.GeometricFibonacciRisk,
                NetworkCyclomaticComplexity = metrics.NetworkCyclomaticComplexity,
                NetworkDuration = metrics.NetworkDuration,
                NetworkDurationManMonths = metrics.NetworkDurationManMonths,
                ProjectFinish = metrics.ProjectFinish,
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
                DisplayDirectMargin = metrics.DisplayDirectMargin,
                DisplayIndirectMargin = metrics.DisplayIndirectMargin,
                DisplayOtherMargin = metrics.DisplayOtherMargin,
                DisplayTotalMargin = metrics.DisplayTotalMargin,
            };
        }
    }
}
