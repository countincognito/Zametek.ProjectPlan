using Shouldly;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for ResourceSchedulingService.BuildTrackingSeriesSet, focusing on
    /// the per-resource breakdown (ByResource). The critical invariant is that
    /// the per-resource series always sum back to the aggregate series, since
    /// each resource allocated to an activity contributes exactly one unit of
    /// the AllocatedToResources.Count multiplier used in the aggregate.
    ///
    /// The hand-computed scenario:
    ///   Resources: R1 (display order 0), R2 (1), R3 (2, never allocated).
    ///   Activities:
    ///     A1: duration 4, window [0,4), allocated to [R1],
    ///         progress trackers (t0, 25%), (t2, 100%).
    ///     A2: duration 6, window [0,6), allocated to [R1, R2],
    ///         progress tracker (t2, 50%).
    ///     A3: duration 2, window [6,8), allocated to [R2], no trackers.
    ///   Resource trackers:
    ///     R1: t1 [(A1, 50%)], t3 [(A1, 100%), (A2, 30%)].
    ///     R2: t3 [(A2, 80%)].
    ///   Whole-project working time = 1x4 + 2x6 + 1x2 = 18 (R1 = 10, R2 = 8).
    ///   Final cumulative values: plan 18 (R1 10, R2 8),
    ///   progress 10 (R1 7, R2 3), effort 2.6 (R1 1.8, R2 0.8).
    /// </summary>
    public class ResourceSchedulingServiceTests
    {
        private const double c_Tolerance = 0.000000001;

        #region Helpers

        private static ResourceSchedulingService CreateService()
        {
            return new ResourceSchedulingService(new ProjectPlanMapper());
        }

        private static ResourceSettingsModel CreateResourceSettings()
        {
            return new ResourceSettingsModel
            {
                Resources =
                [
                    // Deliberately listed out of display order so the tests can
                    // prove that ByResource is sorted by DisplayOrder.
                    new ResourceModel
                    {
                        Id = 2,
                        Name = @"Res Two",
                        DisplayOrder = 1,
                        ColorFormat = new ColorFormatModel { A = 255, R = 40, G = 50, B = 60 },
                        Trackers =
                        [
                            new ResourceTrackerModel
                            {
                                Time = 3,
                                ResourceId = 2,
                                ActivityTrackers =
                                [
                                    new ResourceActivityTrackerModel { Time = 3, ResourceId = 2, ActivityId = 2, ActivityName = @"Act Two", PercentageWorked = 80 },
                                ],
                            },
                        ],
                    },
                    new ResourceModel
                    {
                        Id = 1,
                        Name = @"Res One",
                        DisplayOrder = 0,
                        ColorFormat = new ColorFormatModel { A = 255, R = 10, G = 20, B = 30 },
                        Trackers =
                        [
                            new ResourceTrackerModel
                            {
                                Time = 1,
                                ResourceId = 1,
                                ActivityTrackers =
                                [
                                    new ResourceActivityTrackerModel { Time = 1, ResourceId = 1, ActivityId = 1, ActivityName = @"Act One", PercentageWorked = 50 },
                                ],
                            },
                            new ResourceTrackerModel
                            {
                                Time = 3,
                                ResourceId = 1,
                                ActivityTrackers =
                                [
                                    new ResourceActivityTrackerModel { Time = 3, ResourceId = 1, ActivityId = 1, ActivityName = @"Act One", PercentageWorked = 100 },
                                    new ResourceActivityTrackerModel { Time = 3, ResourceId = 1, ActivityId = 2, ActivityName = @"Act Two", PercentageWorked = 30 },
                                ],
                            },
                        ],
                    },
                    new ResourceModel
                    {
                        Id = 3,
                        Name = @"Res Three",
                        DisplayOrder = 2,
                        ColorFormat = new ColorFormatModel { A = 255, R = 70, G = 80, B = 90 },
                    },
                ],
            };
        }

        private static List<ActivityModel> CreateActivities()
        {
            return
            [
                new ActivityModel
                {
                    Id = 1,
                    Name = @"Act One",
                    Duration = 4,
                    EarliestStartTime = 0,
                    EarliestFinishTime = 4,
                    AllocatedToResources = [1],
                    Trackers =
                    [
                        new ActivityTrackerModel { Time = 0, ActivityId = 1, PercentageComplete = 25 },
                        new ActivityTrackerModel { Time = 2, ActivityId = 1, PercentageComplete = 100 },
                    ],
                },
                new ActivityModel
                {
                    Id = 2,
                    Name = @"Act Two",
                    Duration = 6,
                    EarliestStartTime = 0,
                    EarliestFinishTime = 6,
                    AllocatedToResources = [1, 2],
                    Trackers =
                    [
                        new ActivityTrackerModel { Time = 2, ActivityId = 2, PercentageComplete = 50 },
                    ],
                },
                new ActivityModel
                {
                    Id = 3,
                    Name = @"Act Three",
                    Duration = 2,
                    EarliestStartTime = 6,
                    EarliestFinishTime = 8,
                    AllocatedToResources = [2],
                },
            ];
        }

        private static TrackingSeriesSetModel BuildTrackingSeriesSet(bool hasResources = true)
        {
            return CreateService().BuildTrackingSeriesSet(
                CreateActivities(),
                CreateResourceSettings(),
                hasResources);
        }

        private static double StepValueAt(
            IEnumerable<TrackingPointModel> series,
            int time)
        {
            // Points are appended in ascending time order, so the last matching
            // point carries the cumulative value at the given time.
            return series
                .Where(p => p.Time <= time)
                .Select(p => p.Value)
                .LastOrDefault();
        }

        private static void AssertPerResourceSumsMatchAggregate(
            List<TrackingPointModel> aggregate,
            IEnumerable<List<TrackingPointModel>> perResource)
        {
            List<List<TrackingPointModel>> series = [.. perResource];

            foreach (int time in aggregate.Select(p => p.Time).Distinct())
            {
                double aggregateValue = StepValueAt(aggregate, time);
                double summedValue = series.Sum(s => StepValueAt(s, time));

                summedValue.ShouldBe(aggregateValue, c_Tolerance, $@"Summed per-resource value should match the aggregate at time {time}");
            }
        }

        #endregion

        #region Sum-back-to-aggregate invariants

        [Fact]
        public void BuildTrackingSeriesSet_GivenAllocatedResources_ThenPerResourcePlanSumsBackToAggregate()
        {
            TrackingSeriesSetModel set = BuildTrackingSeriesSet();
            AssertPerResourceSumsMatchAggregate(set.Plan, set.ByResource.Select(x => x.Plan));
        }

        [Fact]
        public void BuildTrackingSeriesSet_GivenAllocatedResources_ThenPerResourceProgressSumsBackToAggregate()
        {
            TrackingSeriesSetModel set = BuildTrackingSeriesSet();
            AssertPerResourceSumsMatchAggregate(set.Progress, set.ByResource.Select(x => x.Progress));
        }

        [Fact]
        public void BuildTrackingSeriesSet_GivenAllocatedResources_ThenPerResourceEffortSumsBackToAggregate()
        {
            TrackingSeriesSetModel set = BuildTrackingSeriesSet();
            AssertPerResourceSumsMatchAggregate(set.Effort, set.ByResource.Select(x => x.Effort));
        }

        [Fact]
        public void BuildTrackingSeriesSet_GivenAllocatedResources_ThenPerResourceTotalWorkingTimesSumToWholeProject()
        {
            TrackingSeriesSetModel set = BuildTrackingSeriesSet();

            set.TotalWorkingTime.ShouldBe(18.0);
            set.ByResource.Sum(x => x.TotalWorkingTime).ShouldBe(set.TotalWorkingTime, c_Tolerance);
        }

        #endregion

        #region Hand-computed values

        [Fact]
        public void BuildTrackingSeriesSet_GivenAllocatedResources_ThenPerResourceTotalWorkingTimesMatchHandComputedValues()
        {
            TrackingSeriesSetModel set = BuildTrackingSeriesSet();

            set.ByResource[0].TotalWorkingTime.ShouldBe(10.0);
            set.ByResource[1].TotalWorkingTime.ShouldBe(8.0);
            set.ByResource[2].TotalWorkingTime.ShouldBe(0.0);
        }

        [Fact]
        public void BuildTrackingSeriesSet_GivenAllocatedResources_ThenFinalCumulativeValuesMatchHandComputedValues()
        {
            TrackingSeriesSetModel set = BuildTrackingSeriesSet();

            // Aggregate.
            set.Plan.Last().Value.ShouldBe(18.0, c_Tolerance);
            set.Progress.Last().Value.ShouldBe(10.0, c_Tolerance);
            set.Effort.Last().Value.ShouldBe(2.6, c_Tolerance);

            // R1.
            ResourceTrackingSeriesModel r1 = set.ByResource[0];
            r1.Plan.Last().Value.ShouldBe(10.0, c_Tolerance);
            r1.Progress.Last().Value.ShouldBe(7.0, c_Tolerance);
            r1.Effort.Last().Value.ShouldBe(1.8, c_Tolerance);

            // R2.
            ResourceTrackingSeriesModel r2 = set.ByResource[1];
            r2.Plan.Last().Value.ShouldBe(8.0, c_Tolerance);
            r2.Progress.Last().Value.ShouldBe(3.0, c_Tolerance);
            r2.Effort.Last().Value.ShouldBe(0.8, c_Tolerance);
        }

        [Fact]
        public void BuildTrackingSeriesSet_GivenAllocatedResources_ThenPerResourcePercentagesAreWholeProjectShares()
        {
            TrackingSeriesSetModel set = BuildTrackingSeriesSet();

            // The recorded percentages are shares of the whole project, so the
            // final plan percentages should be each resource's slice of 100%.
            ResourceTrackingSeriesModel r1 = set.ByResource[0];
            ResourceTrackingSeriesModel r2 = set.ByResource[1];

            r1.Plan.Last().ValuePercentage.ShouldBe(100.0 * 10.0 / 18.0, c_Tolerance);
            r2.Plan.Last().ValuePercentage.ShouldBe(100.0 * 8.0 / 18.0, c_Tolerance);
        }

        #endregion

        #region ByResource identity and ordering

        [Fact]
        public void BuildTrackingSeriesSet_GivenResourcesOutOfDisplayOrder_ThenByResourceIsSortedByDisplayOrder()
        {
            TrackingSeriesSetModel set = BuildTrackingSeriesSet();

            set.ByResource.Count.ShouldBe(3);
            set.ByResource.Select(x => x.ResourceId).ShouldBe([1, 2, 3]);
            set.ByResource.Select(x => x.DisplayOrder).ShouldBe([0, 1, 2]);
        }

        [Fact]
        public void BuildTrackingSeriesSet_GivenResources_ThenByResourceCopiesNameAndColor()
        {
            TrackingSeriesSetModel set = BuildTrackingSeriesSet();

            ResourceTrackingSeriesModel r1 = set.ByResource[0];
            r1.ResourceName.ShouldBe(@"Res One");
            r1.ColorFormat.ShouldBe(new ColorFormatModel { A = 255, R = 10, G = 20, B = 30 });

            ResourceTrackingSeriesModel r2 = set.ByResource[1];
            r2.ResourceName.ShouldBe(@"Res Two");
            r2.ColorFormat.ShouldBe(new ColorFormatModel { A = 255, R = 40, G = 50, B = 60 });
        }

        [Fact]
        public void BuildTrackingSeriesSet_GivenUnallocatedResource_ThenSeriesContainOnlyAnchorPoints()
        {
            TrackingSeriesSetModel set = BuildTrackingSeriesSet();

            ResourceTrackingSeriesModel r3 = set.ByResource[2];
            r3.Plan.ShouldBe([new TrackingPointModel()]);
            r3.Progress.ShouldBe([new TrackingPointModel()]);
            r3.Effort.ShouldBe([new TrackingPointModel()]);
            r3.EffortProjection.ShouldBeEmpty();
        }

        #endregion

        #region Projections

        [Fact]
        public void BuildTrackingSeriesSet_GivenAllocatedResources_ThenPerResourcePlanProjectionEndsAtLastPlanPoint()
        {
            TrackingSeriesSetModel set = BuildTrackingSeriesSet();

            foreach (ResourceTrackingSeriesModel resourceSeries in set.ByResource)
            {
                resourceSeries.PlanProjection.Count.ShouldBe(2);
                resourceSeries.PlanProjection.First().ShouldBe(new TrackingPointModel());
                resourceSeries.PlanProjection.Last().ShouldBe(resourceSeries.Plan.Last());
            }
        }

        [Fact]
        public void BuildTrackingSeriesSet_GivenAllocatedResources_ThenPerResourceProgressProjectionsMatchHandComputedValues()
        {
            TrackingSeriesSetModel set = BuildTrackingSeriesSet();

            // R1: linear fit through origin over [(0,0),(1,1),(3,7),(3,7)]
            // gives slope 43/19; projected completion = 10 / (43/19) = 4.42 -> 5.
            TrackingPointModel r1Projection = set.ByResource[0].ProgressProjection.Last();
            r1Projection.Time.ShouldBe(5);
            r1Projection.Value.ShouldBe(10.0, c_Tolerance);

            // R2: linear fit through origin over [(0,0),(3,3)] gives slope 1;
            // projected completion = 8 / 1 = 8.
            TrackingPointModel r2Projection = set.ByResource[1].ProgressProjection.Last();
            r2Projection.Time.ShouldBe(8);
            r2Projection.Value.ShouldBe(8.0, c_Tolerance);

            // R3 has no progress data, so only the anchor is present.
            set.ByResource[2].ProgressProjection.ShouldBe([new TrackingPointModel()]);
        }

        [Fact]
        public void BuildTrackingSeriesSet_GivenAllocatedResources_ThenPerResourceEffortProjectionsMatchHandComputedValues()
        {
            TrackingSeriesSetModel set = BuildTrackingSeriesSet();

            // R1: projected completion = max(progress projection 5, plan projection 6) = 6;
            // linear fit through origin over [(0,0),(2,0.5),(4,1.8),(4,1.8)]
            // gives slope 15.4/36; projected final effort = 6 x 15.4/36.
            TrackingPointModel r1Projection = set.ByResource[0].EffortProjection.Last();
            r1Projection.Time.ShouldBe(6);
            r1Projection.Value.ShouldBe(6.0 * 15.4 / 36.0, c_Tolerance);

            // R2: projected completion = max(progress projection 8, plan projection 8) = 8;
            // linear fit through origin over [(0,0),(4,0.8)] gives slope 0.2;
            // projected final effort = 8 x 0.2 = 1.6.
            TrackingPointModel r2Projection = set.ByResource[1].EffortProjection.Last();
            r2Projection.Time.ShouldBe(8);
            r2Projection.Value.ShouldBe(1.6, c_Tolerance);
        }

        [Fact]
        public void BuildTrackingSeriesSet_GivenAllocatedResources_ThenAggregateProjectionsAreStillProduced()
        {
            TrackingSeriesSetModel set = BuildTrackingSeriesSet();

            set.PlanProjection.Count.ShouldBe(2);
            set.PlanProjection.Last().ShouldBe(set.Plan.Last());
            set.ProgressProjection.Count.ShouldBe(2);
            set.EffortProjection.Count.ShouldBe(2);
        }

        #endregion

        #region CombineResourceTrackingSeries

        [Fact]
        public void CombineResourceTrackingSeries_GivenAllResources_ThenMatchesAggregateSeries()
        {
            TrackingSeriesSetModel set = BuildTrackingSeriesSet();

            TrackingSeriesSetModel combined = CreateService().CombineResourceTrackingSeries(set, [1, 2, 3]);

            combined.TotalWorkingTime.ShouldBe(set.TotalWorkingTime, c_Tolerance);

            foreach (int time in set.Plan.Select(p => p.Time).Distinct())
            {
                StepValueAt(combined.Plan, time).ShouldBe(StepValueAt(set.Plan, time), c_Tolerance, $@"Combined plan should match the aggregate at time {time}");
            }

            foreach (int time in set.Progress.Select(p => p.Time).Distinct())
            {
                StepValueAt(combined.Progress, time).ShouldBe(StepValueAt(set.Progress, time), c_Tolerance, $@"Combined progress should match the aggregate at time {time}");
            }

            foreach (int time in set.Effort.Select(p => p.Time).Distinct())
            {
                StepValueAt(combined.Effort, time).ShouldBe(StepValueAt(set.Effort, time), c_Tolerance, $@"Combined effort should match the aggregate at time {time}");
            }
        }

        [Fact]
        public void CombineResourceTrackingSeries_GivenSingleResource_ThenIsSelfContained()
        {
            TrackingSeriesSetModel set = BuildTrackingSeriesSet();

            TrackingSeriesSetModel combined = CreateService().CombineResourceTrackingSeries(set, [1]);

            combined.TotalWorkingTime.ShouldBe(10.0);
            combined.Plan.Last().Value.ShouldBe(10.0, c_Tolerance);

            // The combined percentages are relative to the combined total, so a
            // single fully-planned resource tops out at its own 100%.
            combined.Plan.Last().ValuePercentage.ShouldBe(100.0, c_Tolerance);

            combined.Progress.Last().Value.ShouldBe(7.0, c_Tolerance);
            combined.Effort.Last().Value.ShouldBe(1.8, c_Tolerance);

            combined.ByResource.Count.ShouldBe(1);
            combined.ByResource[0].ResourceId.ShouldBe(1);
        }

        [Fact]
        public void CombineResourceTrackingSeries_GivenTwoResources_ThenMatchesHandComputedValues()
        {
            TrackingSeriesSetModel set = BuildTrackingSeriesSet();

            TrackingSeriesSetModel combined = CreateService().CombineResourceTrackingSeries(set, [1, 2]);

            combined.TotalWorkingTime.ShouldBe(18.0);

            // Plan: R1 contributes 10 by time 6; R2 contributes 6 by time 6.
            StepValueAt(combined.Plan, 6).ShouldBe(16.0, c_Tolerance);
            combined.Plan.Last().Value.ShouldBe(18.0, c_Tolerance);

            // Progress projection: fit through origin over [(0,0),(1,1),(3,10)]
            // gives slope 3.1; projected completion = 18 / 3.1 = 5.8 -> 6.
            TrackingPointModel progressProjection = combined.ProgressProjection.Last();
            progressProjection.Time.ShouldBe(6);
            progressProjection.Value.ShouldBe(18.0, c_Tolerance);

            // Effort projection: projected completion = max(6, plan projection 8) = 8;
            // fit through origin over [(0,0),(2,0.5),(4,2.6)] gives slope 11.4/20;
            // projected final effort = 8 x 11.4/20 = 4.56.
            TrackingPointModel effortProjection = combined.EffortProjection.Last();
            effortProjection.Time.ShouldBe(8);
            effortProjection.Value.ShouldBe(4.56, c_Tolerance);
        }

        [Fact]
        public void CombineResourceTrackingSeries_GivenNoResourceIds_ThenReturnsEmptySet()
        {
            TrackingSeriesSetModel set = BuildTrackingSeriesSet();

            TrackingSeriesSetModel combined = CreateService().CombineResourceTrackingSeries(set, []);

            combined.TotalWorkingTime.ShouldBe(0.0);
            combined.Plan.ShouldBeEmpty();
            combined.PlanProjection.ShouldBeEmpty();
            combined.Progress.ShouldBeEmpty();
            combined.ProgressProjection.ShouldBeEmpty();
            combined.Effort.ShouldBeEmpty();
            combined.EffortProjection.ShouldBeEmpty();
            combined.ByResource.ShouldBeEmpty();
        }

        [Fact]
        public void CombineResourceTrackingSeries_GivenUnallocatedResource_ThenReturnsAnchorOnlySeries()
        {
            TrackingSeriesSetModel set = BuildTrackingSeriesSet();

            TrackingSeriesSetModel combined = CreateService().CombineResourceTrackingSeries(set, [3]);

            combined.TotalWorkingTime.ShouldBe(0.0);
            combined.Plan.Count.ShouldBe(1);
            combined.Plan[0].Value.ShouldBe(0.0);
            combined.PlanProjection.Count.ShouldBe(2);
            combined.EffortProjection.ShouldBeEmpty();
        }

        #endregion

        #region Edge cases

        [Fact]
        public void BuildTrackingSeriesSet_GivenNoActivities_ThenAllSeriesAreEmpty()
        {
            TrackingSeriesSetModel set = CreateService().BuildTrackingSeriesSet(
                [],
                CreateResourceSettings(),
                hasResources: true);

            set.Plan.ShouldBeEmpty();
            set.Progress.ShouldBeEmpty();
            set.Effort.ShouldBeEmpty();
            set.TotalWorkingTime.ShouldBe(0.0);

            set.ByResource.Count.ShouldBe(3);

            foreach (ResourceTrackingSeriesModel resourceSeries in set.ByResource)
            {
                resourceSeries.TotalWorkingTime.ShouldBe(0.0);
                resourceSeries.Plan.ShouldBeEmpty();
                resourceSeries.Progress.ShouldBeEmpty();
                resourceSeries.Effort.ShouldBeEmpty();
                resourceSeries.PlanProjection.ShouldBeEmpty();
                resourceSeries.ProgressProjection.ShouldBeEmpty();
                resourceSeries.EffortProjection.ShouldBeEmpty();
            }
        }

        [Fact]
        public void BuildTrackingSeriesSet_GivenHasResourcesFalse_ThenEffortSeriesAreEmptyButPlanAndProgressAreStillBrokenDown()
        {
            TrackingSeriesSetModel set = BuildTrackingSeriesSet(hasResources: false);

            set.Effort.ShouldBeEmpty();
            set.EffortProjection.ShouldBeEmpty();

            foreach (ResourceTrackingSeriesModel resourceSeries in set.ByResource)
            {
                resourceSeries.Effort.ShouldBeEmpty();
                resourceSeries.EffortProjection.ShouldBeEmpty();
            }

            set.ByResource[0].Plan.Last().Value.ShouldBe(10.0, c_Tolerance);
            set.ByResource[0].Progress.Last().Value.ShouldBe(7.0, c_Tolerance);
        }

        #endregion
    }
}
