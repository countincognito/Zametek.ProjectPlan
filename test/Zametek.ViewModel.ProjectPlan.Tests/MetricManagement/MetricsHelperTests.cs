using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
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

        // The severities of a new plan in the application: the weights grow with the criticality of an activity.
        private static IEnumerable<ActivitySeverityModel> ApplicationSeverities() =>
        [
            MakeSeverity(slackLimit: 0, criticalityWeight: 4.0, fibonacciWeight: 4.23606797749979),
            MakeSeverity(slackLimit: 10, criticalityWeight: 3.0, fibonacciWeight: 2.618033988749895),
            MakeSeverity(slackLimit: 25, criticalityWeight: 2.0, fibonacciWeight: 1.618033988749895),
            MakeSeverity(slackLimit: int.MaxValue, criticalityWeight: 1.0, fibonacciWeight: 1.0),
        ];

        // A weight of 4 for a critical activity and of 3 for one with a slack of up to 10, in both kinds of weight. The n-th root
        // of the product of 4 and 3 differs in its last bit from the mean worked out with logarithms.
        private static IEnumerable<ActivitySeverityModel> FourAndThreeSeverities() =>
        [
            MakeSeverity(slackLimit: 0, criticalityWeight: 4.0, fibonacciWeight: 4.0),
            MakeSeverity(slackLimit: 10, criticalityWeight: 3.0, fibonacciWeight: 3.0),
        ];

        private static ActivityModel[] MakeActivities(int count, int totalSlack) =>
            [.. Enumerable.Range(0, count).Select(_ => MakeActivity(totalSlack))];

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

        #region The geometric risks of a large plan

        // The product of one factor for each activity is out of the range of a double for a large plan. These risks had no
        // value then: infinity, or minus infinity. A plan whose product is in range keeps the n-th root of the product.

        [Fact]
        public void CalculateGeometricCriticalityRisk_Given_AThousandCriticalActivities_Then_ReturnsOne()
        {
            // 4 to the power of 1000 is more than a double holds. The mean of the weights is 4, and the critical weight is 4.
            var lookup = new ActivitySeverityLookup(ApplicationSeverities());
            var result = MetricsHelper.CalculateGeometricCriticalityRisk(MakeActivities(1000, 0), lookup);
            result.ShouldNotBeNull();
            result!.Value.ShouldBe(1.0, 1e-12);
        }

        [Fact]
        public void CalculateGeometricFibonacciRisk_Given_AThousandCriticalActivities_Then_ReturnsOne()
        {
            var lookup = new ActivitySeverityLookup(ApplicationSeverities());
            var result = MetricsHelper.CalculateGeometricFibonacciRisk(MakeActivities(1000, 0), lookup);
            result.ShouldNotBeNull();
            result!.Value.ShouldBe(1.0, 1e-12);
        }

        [Fact]
        public void CalculateGeometricActivityRisk_Given_AThousandActivitiesWithTheSameSlack_Then_ReturnsZero()
        {
            // 101 to the power of 1000 is more than a double holds. The mean of (slack + 1) is 101, and so the mean slack
            // is 100, the largest slack: 1 - 100 / 100 = 0.
            var result = MetricsHelper.CalculateGeometricActivityRisk(MakeActivities(1000, 100));
            result.ShouldNotBeNull();
            result!.Value.ShouldBe(0.0, 1e-12);
        }

        [Fact]
        public void CalculateGeometricCriticalityRisk_Given_ALargePlanOfCriticalAndFreeActivities_Then_ReturnsTheMeanOverTheCriticalWeight()
        {
            // 600 weights of 4 and 400 of 1: the mean is 4 to the power of 0.6, and 4 to the power of 600 is out of range.
            var lookup = new ActivitySeverityLookup(ApplicationSeverities());
            ActivityModel[] activities = [.. MakeActivities(600, 0), .. MakeActivities(400, 30)];
            var result = MetricsHelper.CalculateGeometricCriticalityRisk(activities, lookup);
            result.ShouldNotBeNull();
            result!.Value.ShouldBe(Math.Pow(4.0, 0.6) / 4.0, 1e-12);
        }

        [Fact]
        public void CalculateGeometricCriticalityRisk_Given_AProductBelowTheRangeOfADouble_Then_ReturnsTheMeanOverTheCriticalWeight()
        {
            // 0.5 to the power of 2000 is too small for a double, and the product is zero, but no weight is zero.
            var lookup = new ActivitySeverityLookup([MakeSeverity(slackLimit: 0, criticalityWeight: 0.5, fibonacciWeight: 0.5)]);
            var result = MetricsHelper.CalculateGeometricCriticalityRisk(MakeActivities(2000, 0), lookup);
            result.ShouldNotBeNull();
            result!.Value.ShouldBe(1.0, 1e-12);
        }

        [Fact]
        public void CalculateGeometricCriticalityRisk_Given_AWeightOfZero_Then_ReturnsZero()
        {
            // A weight of zero makes the product zero, and the mean is zero too.
            var lookup = new ActivitySeverityLookup(
            [
                MakeSeverity(slackLimit: 0, criticalityWeight: 4.0, fibonacciWeight: 4.0),
                MakeSeverity(slackLimit: 10, criticalityWeight: 0.0, fibonacciWeight: 0.0),
            ]);
            var result = MetricsHelper.CalculateGeometricCriticalityRisk([MakeActivity(0), MakeActivity(5)], lookup);
            result.ShouldBe(0.0);
        }

        [Fact]
        public void CalculateGeometricCriticalityRisk_Given_ASmallPlan_Then_ReturnsTheRootOfTheProductToTheLastBit()
        {
            var lookup = new ActivitySeverityLookup(FourAndThreeSeverities());
            var result = MetricsHelper.CalculateGeometricCriticalityRisk([MakeActivity(0), MakeActivity(5)], lookup);
            result.ShouldBe(Math.Pow(4.0 * 3.0, 1.0 / 2) / 4.0);
        }

        [Fact]
        public void CalculateGeometricFibonacciRisk_Given_ASmallPlan_Then_ReturnsTheRootOfTheProductToTheLastBit()
        {
            var lookup = new ActivitySeverityLookup(FourAndThreeSeverities());
            var result = MetricsHelper.CalculateGeometricFibonacciRisk([MakeActivity(0), MakeActivity(5)], lookup);
            result.ShouldBe(Math.Pow(4.0 * 3.0, 1.0 / 2) / 4.0);
        }

        [Fact]
        public void CalculateGeometricActivityRisk_Given_AnActivityWithNoSlack_Then_ItCountsAsAFactorOfOne()
        {
            // The factors are 4, for a slack of 3, and none for the activity with no slack, but the root is over both
            // activities: the square root of 4 is 2, and 1 - (2 - 1) / 3 = 2/3.
            var result = MetricsHelper.CalculateGeometricActivityRisk([MakeActivity(3), MakeActivity(null)]);
            result.ShouldNotBeNull();
            result!.Value.ShouldBe(2.0 / 3.0, 1e-12);
        }

        [Fact]
        public void CalculateGeometricActivityRisk_Given_ASmallPlan_Then_ReturnsTheRootOfTheProductToTheLastBit()
        {
            // Slacks of 1, 1 and 3: the factors are 2, 2 and 4, and the largest slack is 3.
            var result = MetricsHelper.CalculateGeometricActivityRisk([MakeActivity(1), MakeActivity(1), MakeActivity(3)]);
            result.ShouldBe(1.0 - ((Math.Pow(2.0 * 2.0 * 4.0, 1.0 / 3) - 1.0) / 3.0));
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
