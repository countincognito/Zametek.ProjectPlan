using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// The name of each metric that the scenario chart can plot, as the files and the API give it: the words of the metrics
    /// of the API, which name a metric for what it is (DirectCost, and not CostsDirect). The API writes them in lowerCamelCase.
    /// </summary>
    public static class TrackedMetricsNames
    {
        public static string Get(TrackedMetrics metric)
        {
            return metric switch
            {
                TrackedMetrics.None => @"None",
                TrackedMetrics.RisksCriticality => @"CriticalityRisk",
                TrackedMetrics.RisksFibonacci => @"FibonacciRisk",
                TrackedMetrics.RisksActivity => @"ActivityRisk",
                TrackedMetrics.RisksActivityStdDevCorrection => @"ActivityRiskWithStandardDeviationCorrection",
                TrackedMetrics.RisksGeometricCriticality => @"GeometricCriticalityRisk",
                TrackedMetrics.RisksGeometricFibonacci => @"GeometricFibonacciRisk",
                TrackedMetrics.RisksGeometricActivity => @"GeometricActivityRisk",
                TrackedMetrics.CostsDirect => @"DirectCost",
                TrackedMetrics.CostsIndirect => @"IndirectCost",
                TrackedMetrics.CostsOther => @"OtherCost",
                TrackedMetrics.CostsTotal => @"TotalCost",
                TrackedMetrics.BillingsDirect => @"DirectBilling",
                TrackedMetrics.BillingsIndirect => @"IndirectBilling",
                TrackedMetrics.BillingsOther => @"OtherBilling",
                TrackedMetrics.BillingsTotal => @"TotalBilling",
                TrackedMetrics.MarginsDirect => @"DirectMargin",
                TrackedMetrics.MarginsIndirect => @"IndirectMargin",
                TrackedMetrics.MarginsOther => @"OtherMargin",
                TrackedMetrics.MarginsTotal => @"TotalMargin",
                TrackedMetrics.MarginsDirectAbsolute => @"DirectMarginAbsolute",
                TrackedMetrics.MarginsIndirectAbsolute => @"IndirectMarginAbsolute",
                TrackedMetrics.MarginsOtherAbsolute => @"OtherMarginAbsolute",
                TrackedMetrics.MarginsTotalAbsolute => @"TotalMarginAbsolute",
                TrackedMetrics.EffortsDirect => @"DirectEffort",
                TrackedMetrics.EffortsIndirect => @"IndirectEffort",
                TrackedMetrics.EffortsOther => @"OtherEffort",
                TrackedMetrics.EffortsTotal => @"TotalEffort",
                TrackedMetrics.EffortsActivity => @"ActivityEffort",
                TrackedMetrics.EffortsEfficiency => @"EffortEfficiency",
                TrackedMetrics.NetworkCyclomaticComplexity => @"NetworkCyclomaticComplexity",
                TrackedMetrics.NetworkDuration => @"NetworkDuration",
                TrackedMetrics.NetworkDurationManMonths => @"NetworkDurationManMonths",
                TrackedMetrics.HeadcountPeak => @"PeakHeadcount",
                TrackedMetrics.HeadcountAverage => @"AverageHeadcount",
                TrackedMetrics.HeadcountPeakToAverageRatio => @"PeakToAverageHeadcount",
                _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, null),
            };
        }
    }
}
