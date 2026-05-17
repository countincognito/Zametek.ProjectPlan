using Shouldly;
using System.Collections.Generic;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    public class MetricsHelperTests
    {
        #region Helpers

        private static ActivitySeverityModel MakeSeverity(int slackLimit, double criticalityWeight, double fibonacciWeight) =>
            new ActivitySeverityModel
            {
                SlackLimit = slackLimit,
                CriticalityWeight = criticalityWeight,
                FibonacciWeight = fibonacciWeight,
            };

        private static ActivityModel MakeActivity(int? totalSlack, bool hasNoRisk = false) =>
            new ActivityModel
            {
                Id = 1,
                TotalSlack = totalSlack,
                HasNoRisk = hasNoRisk,
            };

        private static IEnumerable<ActivitySeverityModel> DefaultSeverities() =>
        [
            MakeSeverity(slackLimit: 0, criticalityWeight: 1.0, fibonacciWeight: 1.0),
            MakeSeverity(slackLimit: 5, criticalityWeight: 0.8, fibonacciWeight: 0.5),
            MakeSeverity(slackLimit: 10, criticalityWeight: 0.6, fibonacciWeight: 0.25),
        ];

        #endregion

        #region CalculateActivityRisk

        [Fact]
        public void CalculateActivityRisk_Given_EmptyList_Then_ReturnsOne()
        {
            var result = MetricsHelper.CalculateActivityRisk([]);
            result.ShouldBe(1.0);
        }

        [Fact]
        public void CalculateActivityRisk_Given_AllZeroSlack_Then_ReturnsOne()
        {
            var activities = new[] { MakeActivity(0), MakeActivity(0), MakeActivity(0) };
            var result = MetricsHelper.CalculateActivityRisk(activities);
            result.ShouldBe(1.0);
        }

        [Fact]
        public void CalculateActivityRisk_Given_MixedSlack_Then_ReturnsExpectedRatio()
        {
            // Activities: slack 0, 0, 10 => numerator = 10, maxSlack = 10, denominator = 10 * 3 = 30
            // result = 1 - 10/30 = 0.667
            var activities = new[] { MakeActivity(0), MakeActivity(0), MakeActivity(10) };
            var result = MetricsHelper.CalculateActivityRisk(activities);
            result.ShouldNotBeNull();
            result!.Value.ShouldBeInRange(0.66, 0.68);
        }

        [Fact]
        public void CalculateActivityRisk_Given_AllNullSlack_Then_ReturnsOne()
        {
            // Activities with null TotalSlack are excluded from numerator/denominator calc
            // denominator = 0 * count = 0, numerator = 0 => return 1.0
            var activities = new[] { MakeActivity(null), MakeActivity(null) };
            var result = MetricsHelper.CalculateActivityRisk(activities);
            result.ShouldBe(1.0);
        }

        [Fact]
        public void CalculateActivityRisk_Given_SingleActivityWithSlack_Then_ReturnsZero()
        {
            // numerator = 5, maxSlack = 5, denominator = 5 * 1 = 5
            // result = 1 - 5/5 = 0
            var activities = new[] { MakeActivity(5) };
            var result = MetricsHelper.CalculateActivityRisk(activities);
            result.ShouldBe(0.0);
        }

        #endregion

        #region CalculateActivityRiskWithStdDevCorrection

        [Fact]
        public void CalculateActivityRiskWithStdDevCorrection_Given_EmptyList_Then_ReturnsOne()
        {
            var result = MetricsHelper.CalculateActivityRiskWithStdDevCorrection([]);
            result.ShouldBe(1.0);
        }

        [Fact]
        public void CalculateActivityRiskWithStdDevCorrection_Given_AllZeroSlack_Then_ReturnsOne()
        {
            var activities = new[] { MakeActivity(0), MakeActivity(0) };
            var result = MetricsHelper.CalculateActivityRiskWithStdDevCorrection(activities);
            result.ShouldBe(1.0);
        }

        [Fact]
        public void CalculateActivityRiskWithStdDevCorrection_Given_UniformSlack_Then_CorrectValueWithStdDevOfZero()
        {
            // All slack = 5, mean = 5, stddev = 0, correction = round(5+0) = 5
            // Each capped at 5, so numerator = 5*3 = 15, maxSlack = 5, denominator = 5*3 = 15
            // result = 1 - 15/15 = 0
            var activities = new[] { MakeActivity(5), MakeActivity(5), MakeActivity(5) };
            var result = MetricsHelper.CalculateActivityRiskWithStdDevCorrection(activities);
            result.ShouldBe(0.0);
        }

        #endregion

        #region CalculateCriticalityRisk

        [Fact]
        public void CalculateCriticalityRisk_Given_EmptyList_Then_ReturnsOne()
        {
            var lookup = new ActivitySeverityLookup(DefaultSeverities());
            var result = MetricsHelper.CalculateCriticalityRisk([], lookup);
            result.ShouldBe(1.0);
        }

        [Fact]
        public void CalculateCriticalityRisk_Given_AllCriticalActivities_Then_ReturnsOne()
        {
            var lookup = new ActivitySeverityLookup(DefaultSeverities());
            // SlackLimit 0 => criticalityWeight 1.0. Critical weight = 1.0.
            // numerator = 1.0 * 2 = 2, denominator = 1.0 * 2 = 2 => result = 1.0
            var activities = new[] { MakeActivity(0), MakeActivity(0) };
            var result = MetricsHelper.CalculateCriticalityRisk(activities, lookup);
            result.ShouldBe(1.0);
        }

        [Fact]
        public void CalculateCriticalityRisk_Given_MixedSlack_Then_ReturnsExpectedRatio()
        {
            var lookup = new ActivitySeverityLookup(DefaultSeverities());
            // slack=0 => weight 1.0, slack=5 => weight 0.8
            // numerator = 1.0 + 0.8 = 1.8, denominator = 1.0 * 2 = 2.0
            var activities = new[] { MakeActivity(0), MakeActivity(5) };
            var result = MetricsHelper.CalculateCriticalityRisk(activities, lookup);
            result.ShouldNotBeNull();
            result!.Value.ShouldBeInRange(0.89, 0.91);
        }

        #endregion

        #region CalculateFibonacciRisk

        [Fact]
        public void CalculateFibonacciRisk_Given_EmptyList_Then_ReturnsOne()
        {
            var lookup = new ActivitySeverityLookup(DefaultSeverities());
            var result = MetricsHelper.CalculateFibonacciRisk([], lookup);
            result.ShouldBe(1.0);
        }

        [Fact]
        public void CalculateFibonacciRisk_Given_AllCriticalActivities_Then_ReturnsOne()
        {
            var lookup = new ActivitySeverityLookup(DefaultSeverities());
            var activities = new[] { MakeActivity(0), MakeActivity(0) };
            var result = MetricsHelper.CalculateFibonacciRisk(activities, lookup);
            result.ShouldBe(1.0);
        }

        #endregion

        #region CalculateGeometricActivityRisk

        [Fact]
        public void CalculateGeometricActivityRisk_Given_EmptyList_Then_ReturnsOne()
        {
            var result = MetricsHelper.CalculateGeometricActivityRisk([]);
            result.ShouldBe(1.0);
        }

        [Fact]
        public void CalculateGeometricActivityRisk_Given_AllZeroSlack_Then_ReturnsOne()
        {
            // totalSlack=0 => (0+1) = 1, numerator = pow(1,1/n) - 1 = 0, denominator = maxSlack = 0
            // both 0 => return 1.0
            var activities = new[] { MakeActivity(0), MakeActivity(0) };
            var result = MetricsHelper.CalculateGeometricActivityRisk(activities);
            result.ShouldBe(1.0);
        }

        #endregion

        #region CalculateEfficiency

        [Fact]
        public void CalculateEfficiency_Given_BothNull_Then_ReturnsNull()
        {
            MetricsHelper.CalculateEfficiency(null, null).ShouldBeNull();
        }

        [Fact]
        public void CalculateEfficiency_Given_ActivityEffortNull_Then_ReturnsNull()
        {
            MetricsHelper.CalculateEfficiency(null, 10.0).ShouldBeNull();
        }

        [Fact]
        public void CalculateEfficiency_Given_TotalEffortNull_Then_ReturnsNull()
        {
            MetricsHelper.CalculateEfficiency(10.0, null).ShouldBeNull();
        }

        [Fact]
        public void CalculateEfficiency_Given_TotalEffortZero_Then_ReturnsNull()
        {
            MetricsHelper.CalculateEfficiency(10.0, 0.0).ShouldBeNull();
        }

        [Fact]
        public void CalculateEfficiency_Given_ActivityEffortZero_Then_ReturnsNull()
        {
            MetricsHelper.CalculateEfficiency(0.0, 10.0).ShouldBeNull();
        }

        [Fact]
        public void CalculateEfficiency_Given_ValidInputs_Then_ReturnsRatio()
        {
            var result = MetricsHelper.CalculateEfficiency(5.0, 10.0);
            result.ShouldBe(0.5);
        }

        [Fact]
        public void CalculateEfficiency_Given_EqualInputs_Then_ReturnsOne()
        {
            var result = MetricsHelper.CalculateEfficiency(10.0, 10.0);
            result.ShouldBe(1.0);
        }

        #endregion

        #region CalculateMarginAbsolute

        [Fact]
        public void CalculateMarginAbsolute_Given_BothNull_Then_ReturnsNull()
        {
            MetricsHelper.CalculateMarginAbsolute(null, null).ShouldBeNull();
        }

        [Fact]
        public void CalculateMarginAbsolute_Given_CostNull_Then_ReturnsNull()
        {
            MetricsHelper.CalculateMarginAbsolute(null, 100.0).ShouldBeNull();
        }

        [Fact]
        public void CalculateMarginAbsolute_Given_BillingNull_Then_ReturnsNull()
        {
            MetricsHelper.CalculateMarginAbsolute(100.0, null).ShouldBeNull();
        }

        [Fact]
        public void CalculateMarginAbsolute_Given_ValidInputs_Then_ReturnsBillingMinusCost()
        {
            MetricsHelper.CalculateMarginAbsolute(80.0, 100.0).ShouldBe(20.0);
        }

        [Fact]
        public void CalculateMarginAbsolute_Given_CostExceedsBilling_Then_ReturnsNegative()
        {
            MetricsHelper.CalculateMarginAbsolute(120.0, 100.0).ShouldBe(-20.0);
        }

        [Fact]
        public void CalculateMarginAbsolute_Given_EqualValues_Then_ReturnsZero()
        {
            MetricsHelper.CalculateMarginAbsolute(100.0, 100.0).ShouldBe(0.0);
        }

        #endregion

        #region CalculateMargin

        [Fact]
        public void CalculateMargin_Given_BothNull_Then_ReturnsZero()
        {
            // abs=null, billing=null => return 0
            MetricsHelper.CalculateMargin(null, null).ShouldBe(0.0);
        }

        [Fact]
        public void CalculateMargin_Given_ZeroCostAndZeroBilling_Then_ReturnsZero()
        {
            // abs=0, billing=0 => return 0
            MetricsHelper.CalculateMargin(0.0, 0.0).ShouldBe(0.0);
        }

        [Fact]
        public void CalculateMargin_Given_CostNullAndPositiveBilling_Then_ReturnsOne()
        {
            // abs=null, billing=100 (non-null, non-zero) => return 1.0
            MetricsHelper.CalculateMargin(null, 100.0).ShouldBe(1.0);
        }

        [Fact]
        public void CalculateMargin_Given_PositiveCostAndBilling_Then_ReturnsRatio()
        {
            // abs = 100 - 80 = 20, billing = 100 => margin = 20/100 = 0.2
            MetricsHelper.CalculateMargin(80.0, 100.0).ShouldBe(0.2);
        }

        [Fact]
        public void CalculateMargin_Given_NonZeroAbsAndZeroBilling_Then_ReturnsNull()
        {
            // abs = 0 - 80 = -80 (non-zero), billing = 0 => null
            MetricsHelper.CalculateMargin(80.0, 0.0).ShouldBeNull();
        }

        #endregion

        #region CalculateProjectRisks

        [Fact]
        public void CalculateProjectRisks_Given_EmptyActivities_Then_ReturnsAllOnes()
        {
            var result = MetricsHelper.CalculateProjectRisks([], DefaultSeverities());
            result.Criticality.ShouldBe(1.0);
            result.Fibonacci.ShouldBe(1.0);
            result.Activity.ShouldBe(1.0);
            result.ActivityStdDevCorrection.ShouldBe(1.0);
            result.GeometricCriticality.ShouldBe(1.0);
            result.GeometricFibonacci.ShouldBe(1.0);
            result.GeometricActivity.ShouldBe(1.0);
        }

        [Fact]
        public void CalculateProjectRisks_Given_ActivitiesWithNoRisk_Then_ExcludesThemFromCalc()
        {
            // Activities with HasNoRisk=true should be excluded
            var activities = new[]
            {
                MakeActivity(0, hasNoRisk: false),
                MakeActivity(100, hasNoRisk: true),  // excluded
            };
            var result = MetricsHelper.CalculateProjectRisks(activities, DefaultSeverities());
            // Only the critical activity (slack=0) is included
            result.Criticality.ShouldBe(1.0);
            result.Activity.ShouldBe(1.0);
        }

        #endregion
    }
}
