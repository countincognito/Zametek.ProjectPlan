using Shouldly;
using System.Collections.Generic;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Property-based style invariant tests for MetricsHelper using [Theory]+[InlineData].
    /// These verify mathematical contracts that must hold for all valid inputs:
    ///   • Risk values are always in [0, 1]
    ///   • Efficiency with equal arguments is always 1.0
    ///   • Margin = (billing - cost) / billing
    ///   • ActivityRisk is monotonically non-increasing as slack increases (for fixed activity set shape)
    /// </summary>
    public class MetricsHelperInvariantTests
    {
        #region Helpers

        private static ActivitySeverityLookup DefaultLookup() =>
            new ActivitySeverityLookup(
            [
                new ActivitySeverityModel { SlackLimit = 0,  CriticalityWeight = 1.0,  FibonacciWeight = 1.0  },
                new ActivitySeverityModel { SlackLimit = 5,  CriticalityWeight = 0.8,  FibonacciWeight = 0.5  },
                new ActivitySeverityModel { SlackLimit = 10, CriticalityWeight = 0.6,  FibonacciWeight = 0.25 },
            ]);

        private static ActivityModel Activity(int slack) =>
            new ActivityModel { Id = 1, TotalSlack = slack };

        private static IEnumerable<ActivityModel> Activities(int slack) => [Activity(slack)];

        #endregion

        #region Risk invariants - result is always in [0, 1] for valid inputs

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(10)]
        [InlineData(100)]
        public void CalculateActivityRisk_IsAlwaysInZeroToOneRange(int slack)
        {
            double? risk = MetricsHelper.CalculateActivityRisk(Activities(slack));
            risk.ShouldNotBeNull();
            risk!.Value.ShouldBeInRange(0.0, 1.0);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(10)]
        [InlineData(50)]
        public void CalculateCriticalityRisk_IsAlwaysInZeroToOneRange(int slack)
        {
            var lookup = DefaultLookup();
            double? risk = MetricsHelper.CalculateCriticalityRisk(Activities(slack), lookup);
            risk.ShouldNotBeNull();
            risk!.Value.ShouldBeInRange(0.0, 1.0);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(10)]
        [InlineData(50)]
        public void CalculateFibonacciRisk_IsAlwaysInZeroToOneRange(int slack)
        {
            var lookup = DefaultLookup();
            double? risk = MetricsHelper.CalculateFibonacciRisk(Activities(slack), lookup);
            risk.ShouldNotBeNull();
            risk!.Value.ShouldBeInRange(0.0, 1.0);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(20)]
        public void CalculateActivityRiskWithStdDevCorrection_IsAlwaysInZeroToOneRange(int slack)
        {
            // Multiple activities with the same slack => stddev = 0.
            ActivityModel[] activities =
            [
                new ActivityModel { Id = 1, TotalSlack = slack },
                new ActivityModel { Id = 2, TotalSlack = slack },
                new ActivityModel { Id = 3, TotalSlack = slack },
            ];
            double? risk = MetricsHelper.CalculateActivityRiskWithStdDevCorrection(activities);
            risk.ShouldNotBeNull();
            risk!.Value.ShouldBeInRange(0.0, 1.0);
        }

        #endregion

        #region Efficiency invariant - equal inputs always produce 1.0

        [Theory]
        [InlineData(1.0)]
        [InlineData(10.0)]
        [InlineData(100.0)]
        [InlineData(0.001)]
        [InlineData(999999.9)]
        public void CalculateEfficiency_EqualInputs_AlwaysReturnsOne(double value)
        {
            MetricsHelper.CalculateEfficiency(value, value).ShouldBe(1.0);
        }

        #endregion

        #region Efficiency invariant - result = activityEffort / totalEffort

        [Theory]
        [InlineData(1.0, 2.0, 0.5)]
        [InlineData(3.0, 4.0, 0.75)]
        [InlineData(7.0, 10.0, 0.7)]
        [InlineData(1.0, 1.0, 1.0)]
        public void CalculateEfficiency_ReturnsCorrectRatio(double activity, double total, double expected)
        {
            double? result = MetricsHelper.CalculateEfficiency(activity, total);
            result.ShouldNotBeNull();
            result!.Value.ShouldBe(expected, tolerance: 1e-9);
        }

        #endregion

        #region Margin invariant - margin = (billing - cost) / billing

        [Theory]
        [InlineData(80.0, 100.0, 0.2)]
        [InlineData(50.0, 100.0, 0.5)]
        [InlineData(0.0,  100.0, 1.0)]   // zero cost => abs = billing => margin = 1
        [InlineData(100.0, 100.0, 0.0)]
        public void CalculateMargin_SatisfiesBillingMinusCostOverBillingFormula(double cost, double billing, double expected)
        {
            double? margin = MetricsHelper.CalculateMargin(cost, billing);
            margin.ShouldNotBeNull();
            margin!.Value.ShouldBe(expected, tolerance: 1e-9);
        }

        #endregion

        #region ActivityRisk monotonicity - more slack = lower (or equal) risk

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 5)]
        [InlineData(5, 10)]
        [InlineData(10, 20)]
        [InlineData(0, 50)]
        public void CalculateActivityRisk_IncreasedSlack_Produces_LowerOrEqualRisk(int lowerSlack, int higherSlack)
        {
            // Build a two-activity set: one at slack=0 (fixed) and one at the varying slack.
            // Increasing the varying slack should not increase the risk.
            ActivityModel[] lowerSet =
            [
                new ActivityModel { Id = 1, TotalSlack = 0 },
                new ActivityModel { Id = 2, TotalSlack = lowerSlack },
            ];
            ActivityModel[] higherSet =
            [
                new ActivityModel { Id = 1, TotalSlack = 0 },
                new ActivityModel { Id = 2, TotalSlack = higherSlack },
            ];

            double? riskLower  = MetricsHelper.CalculateActivityRisk(lowerSet);
            double? riskHigher = MetricsHelper.CalculateActivityRisk(higherSet);

            riskLower.ShouldNotBeNull();
            riskHigher.ShouldNotBeNull();
            riskHigher!.Value.ShouldBeLessThanOrEqualTo(riskLower!.Value);
        }

        #endregion

        #region MarginAbsolute invariant - always = billing - cost

        [Theory]
        [InlineData(80.0, 100.0, 20.0)]
        [InlineData(120.0, 100.0, -20.0)]
        [InlineData(100.0, 100.0, 0.0)]
        [InlineData(0.0, 50.0, 50.0)]
        public void CalculateMarginAbsolute_AlwaysBillingMinusCost(double cost, double billing, double expected)
        {
            double? abs = MetricsHelper.CalculateMarginAbsolute(cost, billing);
            abs.ShouldNotBeNull();
            abs!.Value.ShouldBe(expected, tolerance: 1e-9);
        }

        #endregion

        #region CalculateProjectCosts - totals are sum of parts

        [Fact]
        public void CalculateProjectCosts_Total_Equals_Sum_Of_Direct_Indirect_Other()
        {
            var resourceSeries = new List<ResourceSeriesModel>();
            var result = MetricsHelper.CalculateProjectCosts(resourceSeries);
            result.Total.ShouldBe(result.Direct + result.Indirect + result.Other);
        }

        [Fact]
        public void CalculateProjectBillings_Total_Equals_Sum_Of_Direct_Indirect_Other()
        {
            var resourceSeries = new List<ResourceSeriesModel>();
            var result = MetricsHelper.CalculateProjectBillings(resourceSeries);
            result.Total.ShouldBe(result.Direct + result.Indirect + result.Other);
        }

        [Fact]
        public void CalculateProjectEfforts_Total_Equals_Sum_Of_Direct_Indirect_Other()
        {
            var resourceSeries = new List<ResourceSeriesModel>();
            var result = MetricsHelper.CalculateProjectEfforts(resourceSeries);
            result.Total.ShouldBe(result.Direct + result.Indirect + result.Other);
        }

        #endregion
    }
}
