using Shouldly;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for the per-resource financial metric calculations, and for the
    /// invariant that the project-level metrics equal the member-wise sums of
    /// the per-resource results (the project calculations are folds over the
    /// per-resource primitives, so the two must never diverge).
    /// </summary>
    public class ResourceMetricsHelperTests
    {
        #region Helpers

        private static ResourceSeriesModel MakeSeries(
            InterActivityAllocationType interActivityAllocationType,
            ActivityAllocationType activityAllocationType,
            double unitCost = 0.0,
            double unitBilling = 0.0,
            double fixedCost = 0.0,
            double fixedBilling = 0.0,
            bool[]? costAllocation = null,
            bool[]? billingAllocation = null,
            bool[]? effortAllocation = null,
            bool[]? activityAllocation = null,
            ScheduledActivityModel[]? scheduledActivities = null) =>
            new ResourceSeriesModel
            {
                InterActivityAllocationType = interActivityAllocationType,
                ActivityAllocationType = activityAllocationType,
                UnitCost = unitCost,
                UnitBilling = unitBilling,
                FixedCost = fixedCost,
                FixedBilling = fixedBilling,
                ResourceSchedule = new ResourceScheduleModel
                {
                    CostAllocation = [.. costAllocation ?? []],
                    BillingAllocation = [.. billingAllocation ?? []],
                    EffortAllocation = [.. effortAllocation ?? []],
                    ActivityAllocation = [.. activityAllocation ?? []],
                    ScheduledActivities = [.. scheduledActivities ?? []],
                },
            };

        // A mixed set covering every allocation-type combination that buckets
        // differently: direct, indirect and (inter-activity) none resources,
        // with activity portions landing in direct, other and indirect buckets.
        private static List<ResourceSeriesModel> MixedSeriesSet() =>
        [
            MakeSeries(
                InterActivityAllocationType.Direct,
                ActivityAllocationType.Direct,
                unitCost: 10.0,
                unitBilling: 20.0,
                fixedCost: 5.0,
                fixedBilling: 7.0,
                costAllocation: [true, true, true, false],
                billingAllocation: [true, true, false, false],
                effortAllocation: [true, true, true, false],
                activityAllocation: [false, true, false, true],
                scheduledActivities:
                [
                    new ScheduledActivityModel { Duration = 2 },
                    new ScheduledActivityModel { Duration = 5, HasNoEffort = true },
                ]),
            MakeSeries(
                InterActivityAllocationType.Indirect,
                ActivityAllocationType.Other,
                unitCost: 8.0,
                unitBilling: 4.0,
                fixedCost: 2.0,
                fixedBilling: 1.0,
                costAllocation: [true, true],
                billingAllocation: [true, true],
                effortAllocation: [true, false],
                activityAllocation: [true, false],
                scheduledActivities:
                [
                    new ScheduledActivityModel { Duration = 3 },
                ]),
            MakeSeries(
                InterActivityAllocationType.None,
                ActivityAllocationType.Indirect,
                unitCost: 5.0,
                unitBilling: 6.0,
                fixedCost: 1.0,
                fixedBilling: 0.0,
                costAllocation: [true, false, true],
                billingAllocation: [false, true, true],
                effortAllocation: [true, true, true],
                activityAllocation: [false, false, true],
                scheduledActivities:
                [
                    new ScheduledActivityModel { Duration = 1 },
                    new ScheduledActivityModel { Duration = 4 },
                ]),
        ];

        #endregion

        #region CalculateResourceCosts

        [Fact]
        public void CalculateResourceCosts_Given_DirectResource_Then_AllPortionsLandInDirect()
        {
            // Inter-activity portion: cost && !activity = [T,F,T,F] => 2 slots * 10.
            // Activity portion: cost && activity = [F,T,F,F] => 1 slot * 10.
            // Fixed cost: 5. All bucket to direct.
            ResourceSeriesModel series = MakeSeries(
                InterActivityAllocationType.Direct,
                ActivityAllocationType.Direct,
                unitCost: 10.0,
                fixedCost: 5.0,
                costAllocation: [true, true, true, false],
                activityAllocation: [false, true, false, true]);

            ResourceCostsModel result = MetricsHelper.CalculateResourceCosts(series);

            result.Direct.ShouldBe(35.0);
            result.Indirect.ShouldBe(0.0);
            result.Other.ShouldBe(0.0);
            result.Total.ShouldBe(35.0);
        }

        [Fact]
        public void CalculateResourceCosts_Given_IndirectResourceWithOtherActivityAllocation_Then_PortionsSplitAcrossBuckets()
        {
            // Inter-activity portion: cost && !activity = [F,T] => 1 slot * 8 => indirect.
            // Activity portion: cost && activity = [T,F] => 1 slot * 8 => other.
            // Fixed cost: 2 => indirect (keyed off InterActivityAllocationType).
            ResourceSeriesModel series = MakeSeries(
                InterActivityAllocationType.Indirect,
                ActivityAllocationType.Other,
                unitCost: 8.0,
                fixedCost: 2.0,
                costAllocation: [true, true],
                activityAllocation: [true, false]);

            ResourceCostsModel result = MetricsHelper.CalculateResourceCosts(series);

            result.Direct.ShouldBe(0.0);
            result.Indirect.ShouldBe(10.0);
            result.Other.ShouldBe(8.0);
            result.Total.ShouldBe(18.0);
        }

        [Fact]
        public void CalculateResourceCosts_Given_NoneInterActivityAllocation_Then_InterActivityAndFixedLandInOther()
        {
            // Inter-activity portion: cost && !activity = [T] => 1 slot * 5 => other.
            // No activity portion. Fixed cost: 1 => other.
            ResourceSeriesModel series = MakeSeries(
                InterActivityAllocationType.None,
                ActivityAllocationType.Direct,
                unitCost: 5.0,
                fixedCost: 1.0,
                costAllocation: [true],
                activityAllocation: [false]);

            ResourceCostsModel result = MetricsHelper.CalculateResourceCosts(series);

            result.Direct.ShouldBe(0.0);
            result.Indirect.ShouldBe(0.0);
            result.Other.ShouldBe(6.0);
            result.Total.ShouldBe(6.0);
        }

        #endregion

        #region CalculateResourceBillings

        [Fact]
        public void CalculateResourceBillings_Given_IndirectResourceWithOtherActivityAllocation_Then_PortionsSplitAcrossBuckets()
        {
            // Inter-activity portion: billing && !activity = [F,T] => 1 slot * 4 => indirect.
            // Activity portion: billing && activity = [T,F] => 1 slot * 4 => other.
            // Fixed billing: 1 => indirect.
            ResourceSeriesModel series = MakeSeries(
                InterActivityAllocationType.Indirect,
                ActivityAllocationType.Other,
                unitBilling: 4.0,
                fixedBilling: 1.0,
                billingAllocation: [true, true],
                activityAllocation: [true, false]);

            ResourceBillingsModel result = MetricsHelper.CalculateResourceBillings(series);

            result.Direct.ShouldBe(0.0);
            result.Indirect.ShouldBe(5.0);
            result.Other.ShouldBe(4.0);
            result.Total.ShouldBe(9.0);
        }

        #endregion

        #region CalculateResourceEfforts

        [Fact]
        public void CalculateResourceEfforts_Given_DirectResource_Then_ReturnsEffortsWithActivityAndEfficiency()
        {
            // Inter-activity effort: effort && !activity = [T,F,F] => 1.0 => direct.
            // Activity effort: effort && activity = [F,T,T] => 2.0 => direct.
            // Total = 3.0. Activity = 2 + 0 (no effort) + 1 = 3. Efficiency = 3/3 = 1.
            ResourceSeriesModel series = MakeSeries(
                InterActivityAllocationType.Direct,
                ActivityAllocationType.Direct,
                effortAllocation: [true, true, true],
                activityAllocation: [false, true, true],
                scheduledActivities:
                [
                    new ScheduledActivityModel { Duration = 2 },
                    new ScheduledActivityModel { Duration = 5, HasNoEffort = true },
                    new ScheduledActivityModel { Duration = 1 },
                ]);

            ResourceEffortsModel result = MetricsHelper.CalculateResourceEfforts(series);

            result.Direct.ShouldBe(3.0);
            result.Indirect.ShouldBe(0.0);
            result.Other.ShouldBe(0.0);
            result.Total.ShouldBe(3.0);
            result.Activity.ShouldBe(3.0);
            result.Efficiency.ShouldBe(1.0);
        }

        #endregion

        #region CalculateResourceMargins

        [Fact]
        public void CalculateResourceMargins_Given_CostsAndBillings_Then_ReturnsRatiosAndAbsolutes()
        {
            var costs = new ResourceCostsModel
            {
                Direct = 80.0,
                Indirect = 50.0,
                Other = 0.0,
                Total = 130.0,
            };
            var billings = new ResourceBillingsModel
            {
                Direct = 100.0,
                Indirect = 100.0,
                Other = 0.0,
                Total = 200.0,
            };

            ResourceMarginsModel result = MetricsHelper.CalculateResourceMargins(costs, billings);

            result.Direct.ShouldBe(0.2);
            result.Indirect.ShouldBe(0.5);
            result.Other.ShouldBe(0.0);
            result.Total.ShouldNotBeNull();
            result.Total!.Value.ShouldBe(0.35, tolerance: 1e-9);
            result.DirectAbsolute.ShouldBe(20.0);
            result.IndirectAbsolute.ShouldBe(50.0);
            result.OtherAbsolute.ShouldBe(0.0);
            result.TotalAbsolute.ShouldBe(70.0);
        }

        #endregion

        #region Sum invariants - project metrics equal the sums of the per-resource metrics

        [Fact]
        public void SumResourceCosts_Given_MixedSeriesSet_Then_EqualsProjectCosts()
        {
            List<ResourceSeriesModel> seriesSet = MixedSeriesSet();

            CostsModel project = MetricsHelper.CalculateProjectCosts(seriesSet);
            CostsModel summed = MetricsHelper.SumResourceCosts(seriesSet.Select(MetricsHelper.CalculateResourceCosts));

            summed.ShouldBe(project);

            // Guard against the invariant becoming tautological: pin the
            // expected project buckets by hand.
            // Series 1 (direct/direct): inter [T,F,T,F]*10=20 + activity [F,T,F,F]*10=10 + fixed 5 => direct 35.
            // Series 2 (indirect/other): inter [F,T]*8=8 + fixed 2 => indirect 10; activity [T,F]*8=8 => other 8.
            // Series 3 (none/indirect): inter [T,F,F]*5=5 + fixed 1 => other 6; activity [F,F,T]*5=5 => indirect 5.
            project.Direct.ShouldBe(35.0);
            project.Indirect.ShouldBe(15.0);
            project.Other.ShouldBe(14.0);
            project.Total.ShouldBe(64.0);
        }

        [Fact]
        public void SumResourceBillings_Given_MixedSeriesSet_Then_EqualsProjectBillings()
        {
            List<ResourceSeriesModel> seriesSet = MixedSeriesSet();

            BillingsModel project = MetricsHelper.CalculateProjectBillings(seriesSet);
            BillingsModel summed = MetricsHelper.SumResourceBillings(seriesSet.Select(MetricsHelper.CalculateResourceBillings));

            summed.ShouldBe(project);

            // Series 1 (direct/direct): inter (billing && !activity) [T,F,F,F]*20=20 + activity [F,T,F,F]*20=20 + fixed 7 => direct 47.
            // Series 2 (indirect/other): inter [F,T]*4=4 + fixed 1 => indirect 5; activity [T,F]*4=4 => other 4.
            // Series 3 (none/indirect): inter [F,T,F]*6=6 + fixed 0 => other 6; activity [F,F,T]*6=6 => indirect 6.
            project.Direct.ShouldBe(47.0);
            project.Indirect.ShouldBe(11.0);
            project.Other.ShouldBe(10.0);
            project.Total.ShouldBe(68.0);
        }

        [Fact]
        public void SumResourceEfforts_Given_MixedSeriesSet_Then_EqualsProjectEfforts()
        {
            List<ResourceSeriesModel> seriesSet = MixedSeriesSet();

            EffortsModel project = MetricsHelper.CalculateProjectEfforts(seriesSet);
            EffortsModel summed = MetricsHelper.SumResourceEfforts(seriesSet.Select(MetricsHelper.CalculateResourceEfforts));

            summed.ShouldBe(project);

            // Series 1 (direct/direct): inter [T,F,T,F]=2 + activity [F,T,F,F]=1 => direct 3; activity durations 2+0 => 2.
            // Series 2 (indirect/other): inter [F,F]=0 => indirect 0; activity [T,F]=1 => other 1; durations 3.
            // Series 3 (none/indirect): inter [T,T,F]=2 => other 2; activity [F,F,T]=1 => indirect 1; durations 1+4 => 5.
            project.Direct.ShouldBe(3.0);
            project.Indirect.ShouldBe(1.0);
            project.Other.ShouldBe(3.0);
            project.Total.ShouldBe(7.0);
            project.Activity.ShouldBe(10.0);
        }

        [Fact]
        public void SumResourceEfforts_Given_ResourcesWithDifferentEfficiencies_Then_EfficiencyIsRatioOfSumsNotSumOfRatios()
        {
            // Resource A: total 3, activity 3 => efficiency 1.0.
            // Resource B: total 10, activity 2 => efficiency 0.2.
            var efforts = new List<ResourceEffortsModel>
            {
                new ResourceEffortsModel { Direct = 3.0, Indirect = 0.0, Other = 0.0, Total = 3.0, Activity = 3.0, Efficiency = 1.0 },
                new ResourceEffortsModel { Direct = 10.0, Indirect = 0.0, Other = 0.0, Total = 10.0, Activity = 2.0, Efficiency = 0.2 },
            };

            EffortsModel summed = MetricsHelper.SumResourceEfforts(efforts);

            summed.Total.ShouldBe(13.0);
            summed.Activity.ShouldBe(5.0);
            summed.Efficiency.ShouldNotBeNull();
            summed.Efficiency!.Value.ShouldBe(5.0 / 13.0, tolerance: 1e-9);
        }

        #endregion
    }
}
