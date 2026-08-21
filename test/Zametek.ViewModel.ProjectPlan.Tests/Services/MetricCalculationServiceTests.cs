using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for MetricCalculationService, driven from the activities a compilation
    /// actually holds rather than from hand-built models.
    ///
    /// That distinction is the point of this file. The service's job is to take the
    /// compiled activities, map them, filter them and hand them to MetricsHelper, and
    /// the mapping step is where HasNoRisk was being lost: the activity carried it,
    /// the model that reached the helper did not, and every no-risk activity was
    /// therefore counted in the risk metrics anyway. MetricsHelper's own tests could
    /// not see any of that, because they start with an ActivityModel that already has
    /// the flag set.
    ///
    /// So these tests start where the compiler leaves off - at
    /// <see cref="DependentActivity"/> - and assert on what comes out of the service.
    /// The flags are exercised individually and in combination, and each builder is
    /// checked for the flags it must ignore as well as the ones it must honour.
    /// </summary>
    public class MetricCalculationServiceTests
    {
        private const double c_Tolerance = 0.000000001;

        private static readonly DateTimeOffset c_ProjectStart =
            new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        #region Helpers

        private static MetricCalculationService CreateService() =>
            new MetricCalculationService(
                new ProjectPlanMapper(),
                new DateTimeCalculator(TimeProvider.System));

        /// <summary>
        /// An activity as the compiler leaves it: scheduled, with whichever of the
        /// four flags the caller is interested in. TotalSlack is not settable - the
        /// activity derives it - so it is steered through the schedule window, and
        /// the tests assert the slack they got rather than assuming it.
        /// </summary>
        private static DependentActivity MakeActivity(
            int id,
            int duration = 1,
            int? earliestStartTime = 0,
            int? latestFinishTime = null,
            IEnumerable<int>? dependencies = null,
            IEnumerable<int>? planningDependencies = null,
            IEnumerable<int>? resourceDependencies = null,
            bool hasNoCost = false,
            bool hasNoBilling = false,
            bool hasNoEffort = false,
            bool hasNoRisk = false,
            bool canBeRemoved = false) =>
            new DependentActivity(
                id: id,
                displayOrder: id,
                name: $@"Activity {id}",
                notes: string.Empty,
                targetWorkStreams: [],
                targetResources: [],
                dependencies: dependencies ?? [],
                planningDependencies: planningDependencies ?? [],
                resourceDependencies: resourceDependencies ?? [],
                successors: [],
                targetLogicalOperator: LogicalOperator.AND,
                allocatedToResources: [],
                canBeRemoved: canBeRemoved,
                hasNoCost: hasNoCost,
                hasNoBilling: hasNoBilling,
                hasNoEffort: hasNoEffort,
                hasNoRisk: hasNoRisk,
                duration: duration,
                freeSlack: null,
                earliestStartTime: earliestStartTime,
                latestFinishTime: latestFinishTime ?? earliestStartTime + duration,
                minimumFreeSlack: null,
                minimumEarliestStartTime: null,
                maximumLatestFinishTime: null,
                overrideColor: false,
                colorFormat: new ColorFormatModel(),
                trackers: []);

        /// <summary>
        /// An activity with the given total slack, expressed the way the compiler
        /// would leave one: started at zero, finishing later than it needs to.
        /// </summary>
        private static DependentActivity MakeActivityWithSlack(
            int id,
            int totalSlack,
            int duration = 1,
            bool hasNoCost = false,
            bool hasNoBilling = false,
            bool hasNoEffort = false,
            bool hasNoRisk = false)
        {
            DependentActivity activity = MakeActivity(
                id,
                duration: duration,
                earliestStartTime: 0,
                latestFinishTime: duration + totalSlack,
                hasNoCost: hasNoCost,
                hasNoBilling: hasNoBilling,
                hasNoEffort: hasNoEffort,
                hasNoRisk: hasNoRisk);

            // The slack the risk metrics read is derived, not stored, so this pins
            // the assumption rather than leaving the rest of the test resting on it.
            activity.TotalSlack.ShouldBe(totalSlack);
            return activity;
        }

        private static IGraphCompilation<int, int, int, IDependentActivity> MakeCompilation(
            params DependentActivity[] activities) =>
            new GraphCompilation<int, int, int, DependentActivity>(activities, [], []);

        private static List<ActivitySeverityModel> DefaultSeverities() =>
        [
            new ActivitySeverityModel { SlackLimit = 0, CriticalityWeight = 1.0, FibonacciWeight = 1.0 },
            new ActivitySeverityModel { SlackLimit = 5, CriticalityWeight = 0.8, FibonacciWeight = 0.5 },
            new ActivitySeverityModel { SlackLimit = 10, CriticalityWeight = 0.6, FibonacciWeight = 0.25 },
        ];

        /// <summary>
        /// The risk metrics MetricsHelper produces for the given slacks, built without
        /// going anywhere near the mapper, so that a mapping fault cannot cancel
        /// itself out on both sides of an assertion.
        /// </summary>
        private static RisksModel ExpectedRisksForSlacks(params int?[] totalSlacks) =>
            MetricsHelper.CalculateProjectRisks(
                [.. totalSlacks.Select((slack, index) => new ActivityModel { Id = index + 1, TotalSlack = slack })],
                DefaultSeverities());

        private static ResourceScheduleModel MakeSchedule(
            int resourceId,
            bool[] costAllocation,
            bool[] billingAllocation,
            bool[] effortAllocation,
            bool[] activityAllocation,
            params ScheduledActivityModel[] scheduledActivities) =>
            new ResourceScheduleModel
            {
                Resource = new ResourceModel { Id = resourceId },
                CostAllocation = [.. costAllocation],
                BillingAllocation = [.. billingAllocation],
                EffortAllocation = [.. effortAllocation],
                ActivityAllocation = [.. activityAllocation],
                ScheduledActivities = [.. scheduledActivities],
            };

        private static ResourceSeriesModel MakeSeries(
            ResourceScheduleModel schedule,
            string title = @"Resource",
            int displayOrder = 0,
            InterActivityAllocationType interActivityAllocationType = InterActivityAllocationType.Direct,
            ActivityAllocationType activityAllocationType = ActivityAllocationType.Direct,
            double unitCost = 10.0,
            double unitBilling = 20.0,
            double fixedCost = 0.0,
            double fixedBilling = 0.0) =>
            new ResourceSeriesModel
            {
                Title = title,
                DisplayOrder = displayOrder,
                InterActivityAllocationType = interActivityAllocationType,
                ActivityAllocationType = activityAllocationType,
                UnitCost = unitCost,
                UnitBilling = unitBilling,
                FixedCost = fixedCost,
                FixedBilling = fixedBilling,
                ResourceSchedule = schedule,
            };

        private static ResourceSeriesSetModel MakeSeriesSet(params ResourceSeriesModel[] combined) =>
            new ResourceSeriesSetModel { Combined = [.. combined] };

        #endregion

        #region BuildNetworkMetrics

        [Fact]
        public void BuildNetworkMetrics_Given_NullCompilation_Then_Throws()
        {
            MetricCalculationService service = CreateService();
            Should.Throw<ArgumentNullException>(
                () => service.BuildNetworkMetrics(null!, false, c_ProjectStart, 0, 10));
        }

        [Fact]
        public void BuildNetworkMetrics_Given_NoActivities_Then_EverythingIsNull()
        {
            MetricCalculationService service = CreateService();

            NetworkModel network = service.BuildNetworkMetrics(MakeCompilation(), false, c_ProjectStart, 0, 10);

            network.CyclomaticComplexity.ShouldBeNull();
            network.Duration.ShouldBeNull();
            network.DurationManMonths.ShouldBeNull();
        }

        [Fact]
        public void BuildNetworkMetrics_Given_CompilationErrors_Then_EverythingIsNull()
        {
            MetricCalculationService service = CreateService();
            IGraphCompilation<int, int, int, IDependentActivity> compilation =
                MakeCompilation(MakeActivity(1), MakeActivity(2, dependencies: [1]));

            NetworkModel network = service.BuildNetworkMetrics(compilation, true, c_ProjectStart, 0, 10);

            network.CyclomaticComplexity.ShouldBeNull();
            network.Duration.ShouldBeNull();
            network.DurationManMonths.ShouldBeNull();
        }

        [Fact]
        public void BuildNetworkMetrics_Given_AChain_Then_ReportsDurationAndManMonths()
        {
            MetricCalculationService service = CreateService();
            IGraphCompilation<int, int, int, IDependentActivity> compilation =
                MakeCompilation(
                    MakeActivity(1),
                    MakeActivity(2, dependencies: [1]),
                    MakeActivity(3, dependencies: [2]));

            NetworkModel network = service.BuildNetworkMetrics(compilation, false, c_ProjectStart, 0, 10);

            network.CyclomaticComplexity.ShouldNotBeNull();
            network.Duration.ShouldBe(10);

            // Default display mode counts every day, so ten units of duration is ten
            // days, and man months is 12 * (days / 365).
            network.DurationManMonths.ShouldNotBeNull();
            network.DurationManMonths!.Value.ShouldBe(12.0 * (10.0 / 365.0), c_Tolerance);
        }

        [Theory]
        [InlineData(null, 10)]
        [InlineData(0, null)]
        [InlineData(null, null)]
        public void BuildNetworkMetrics_Given_AnUnresolvedProjectWindow_Then_DurationIsNull(
            int? startTime,
            int? finishTime)
        {
            MetricCalculationService service = CreateService();
            IGraphCompilation<int, int, int, IDependentActivity> compilation = MakeCompilation(MakeActivity(1));

            NetworkModel network = service.BuildNetworkMetrics(compilation, false, c_ProjectStart, startTime, finishTime);

            network.Duration.ShouldBeNull();
            network.DurationManMonths.ShouldBeNull();

            // The complexity does not depend on the window, so it still arrives.
            network.CyclomaticComplexity.ShouldNotBeNull();
        }

        /// <summary>
        /// Current behaviour, pinned rather than endorsed: a finish before the start
        /// gives a negative duration, but the date calculator clamps a negative day
        /// count to zero, so the man months come back as zero rather than negative or
        /// null. The two outputs disagree about the same window.
        /// </summary>
        [Fact]
        public void BuildNetworkMetrics_Given_FinishBeforeStart_Then_DurationIsNegativeButManMonthsIsZero()
        {
            MetricCalculationService service = CreateService();
            IGraphCompilation<int, int, int, IDependentActivity> compilation = MakeCompilation(MakeActivity(1));

            NetworkModel network = service.BuildNetworkMetrics(compilation, false, c_ProjectStart, 10, 4);

            network.Duration.ShouldBe(-6);
            network.DurationManMonths.ShouldBe(0.0);
        }

        /// <summary>
        /// The complexity calculation folds planning and resource dependencies into
        /// the ordinary ones before it compiles, so the same graph drawn three ways
        /// has to come out the same.
        /// </summary>
        [Fact]
        public void BuildNetworkMetrics_Given_TheSameEdgesInDifferentDependencySets_Then_ComplexityIsTheSame()
        {
            MetricCalculationService service = CreateService();

            int? ordinary = service.BuildNetworkMetrics(
                MakeCompilation(
                    MakeActivity(1),
                    MakeActivity(2, dependencies: [1]),
                    MakeActivity(3, dependencies: [1, 2])),
                false, c_ProjectStart, 0, 10).CyclomaticComplexity;

            int? planning = service.BuildNetworkMetrics(
                MakeCompilation(
                    MakeActivity(1),
                    MakeActivity(2, planningDependencies: [1]),
                    MakeActivity(3, planningDependencies: [1, 2])),
                false, c_ProjectStart, 0, 10).CyclomaticComplexity;

            int? resource = service.BuildNetworkMetrics(
                MakeCompilation(
                    MakeActivity(1),
                    MakeActivity(2, resourceDependencies: [1]),
                    MakeActivity(3, resourceDependencies: [1, 2])),
                false, c_ProjectStart, 0, 10).CyclomaticComplexity;

            ordinary.ShouldNotBeNull();
            planning.ShouldBe(ordinary);
            resource.ShouldBe(ordinary);
        }

        [Fact]
        public void BuildNetworkMetrics_Given_MoreConnectedGraphs_Then_ComplexityIncreases()
        {
            MetricCalculationService service = CreateService();

            int? chain = service.BuildNetworkMetrics(
                MakeCompilation(
                    MakeActivity(1),
                    MakeActivity(2, dependencies: [1]),
                    MakeActivity(3, dependencies: [2]),
                    MakeActivity(4, dependencies: [3])),
                false, c_ProjectStart, 0, 10).CyclomaticComplexity;

            // A diamond: two independent paths between the same pair of activities.
            int? diamond = service.BuildNetworkMetrics(
                MakeCompilation(
                    MakeActivity(1),
                    MakeActivity(2, dependencies: [1]),
                    MakeActivity(3, dependencies: [1]),
                    MakeActivity(4, dependencies: [2, 3])),
                false, c_ProjectStart, 0, 10).CyclomaticComplexity;

            chain.ShouldNotBeNull();
            diamond.ShouldNotBeNull();
            diamond!.Value.ShouldBeGreaterThan(chain!.Value);
        }

        /// <summary>
        /// The complexity calculation moves the planning and resource dependencies
        /// into the ordinary set and then clears them, which is only safe because it
        /// works on clones. A regression here would quietly strip a live plan of its
        /// dependencies every time the metrics were rebuilt.
        /// </summary>
        [Fact]
        public void BuildNetworkMetrics_Given_ActivitiesWithEveryDependencyKind_Then_DoesNotMutateThem()
        {
            MetricCalculationService service = CreateService();
            DependentActivity first = MakeActivity(1);
            DependentActivity second = MakeActivity(2, dependencies: [1], planningDependencies: [1], resourceDependencies: [1]);

            service.BuildNetworkMetrics(MakeCompilation(first, second), false, c_ProjectStart, 0, 10);

            second.Dependencies.ShouldBe([1]);
            second.PlanningDependencies.ShouldBe([1]);
            second.ResourceDependencies.ShouldBe([1]);
        }

        [Fact]
        public void BuildNetworkMetrics_Given_ActivityFlags_Then_TheyMakeNoDifference()
        {
            MetricCalculationService service = CreateService();

            NetworkModel plain = service.BuildNetworkMetrics(
                MakeCompilation(MakeActivity(1), MakeActivity(2, dependencies: [1])),
                false, c_ProjectStart, 0, 10);

            NetworkModel flagged = service.BuildNetworkMetrics(
                MakeCompilation(
                    MakeActivity(1, hasNoCost: true, hasNoBilling: true, hasNoEffort: true, hasNoRisk: true),
                    MakeActivity(2, dependencies: [1], hasNoCost: true, hasNoBilling: true, hasNoEffort: true, hasNoRisk: true)),
                false, c_ProjectStart, 0, 10);

            flagged.ShouldBe(plain);
        }

        #endregion

        #region BuildRiskMetrics - the flag that reaches the metrics

        [Fact]
        public void BuildRiskMetrics_Given_NullArguments_Then_Throws()
        {
            MetricCalculationService service = CreateService();

            Should.Throw<ArgumentNullException>(
                () => service.BuildRiskMetrics(null!, false, DefaultSeverities()));
            Should.Throw<ArgumentNullException>(
                () => service.BuildRiskMetrics(MakeCompilation(), false, null!));
        }

        /// <summary>
        /// The regression test for the mapping fault, and the one that needs no
        /// arithmetic to be convincing: two compilations that differ in nothing but
        /// HasNoRisk have to produce different risk metrics. While the flag was being
        /// dropped in mapping these two were identical.
        /// </summary>
        [Fact]
        public void BuildRiskMetrics_Given_TwoPlansDifferingOnlyInHasNoRisk_Then_TheMetricsDiffer()
        {
            MetricCalculationService service = CreateService();

            RisksModel counted = service.BuildRiskMetrics(
                MakeCompilation(
                    MakeActivityWithSlack(1, totalSlack: 0),
                    MakeActivityWithSlack(2, totalSlack: 0),
                    MakeActivityWithSlack(3, totalSlack: 10)),
                false,
                DefaultSeverities());

            RisksModel excluded = service.BuildRiskMetrics(
                MakeCompilation(
                    MakeActivityWithSlack(1, totalSlack: 0),
                    MakeActivityWithSlack(2, totalSlack: 0),
                    MakeActivityWithSlack(3, totalSlack: 10, hasNoRisk: true)),
                false,
                DefaultSeverities());

            excluded.ShouldNotBe(counted);
        }

        [Fact]
        public void BuildRiskMetrics_Given_AnActivityWithNoRisk_Then_ItIsLeftOutOfEveryMetric()
        {
            MetricCalculationService service = CreateService();

            RisksModel risks = service.BuildRiskMetrics(
                MakeCompilation(
                    MakeActivityWithSlack(1, totalSlack: 0),
                    MakeActivityWithSlack(2, totalSlack: 3),
                    MakeActivityWithSlack(3, totalSlack: 10, hasNoRisk: true)),
                false,
                DefaultSeverities());

            // Exactly the metrics for the two activities that do carry risk.
            risks.ShouldBe(ExpectedRisksForSlacks(0, 3));
        }

        [Fact]
        public void BuildRiskMetrics_Given_SeveralActivitiesWithNoRisk_Then_OnlyTheRemainderCounts()
        {
            MetricCalculationService service = CreateService();

            RisksModel risks = service.BuildRiskMetrics(
                MakeCompilation(
                    MakeActivityWithSlack(1, totalSlack: 0),
                    MakeActivityWithSlack(2, totalSlack: 20, hasNoRisk: true),
                    MakeActivityWithSlack(3, totalSlack: 4),
                    MakeActivityWithSlack(4, totalSlack: 30, hasNoRisk: true),
                    MakeActivityWithSlack(5, totalSlack: 8)),
                false,
                DefaultSeverities());

            risks.ShouldBe(ExpectedRisksForSlacks(0, 4, 8));
        }

        /// <summary>
        /// Current behaviour, pinned rather than endorsed. A plan in which every
        /// activity is marked as carrying no risk reports a perfect score on all seven
        /// metrics, because the metrics fall through to their empty-set case, which
        /// returns one. A plan with no activities at all reports null on all seven.
        /// The two describe the same thing - nothing to assess - and answer
        /// differently.
        /// </summary>
        [Fact]
        public void BuildRiskMetrics_Given_EveryActivityHasNoRisk_Then_ReportsAPerfectScore()
        {
            MetricCalculationService service = CreateService();

            RisksModel allExcluded = service.BuildRiskMetrics(
                MakeCompilation(
                    MakeActivityWithSlack(1, totalSlack: 0, hasNoRisk: true),
                    MakeActivityWithSlack(2, totalSlack: 10, hasNoRisk: true)),
                false,
                DefaultSeverities());

            allExcluded.Criticality.ShouldBe(1.0);
            allExcluded.Fibonacci.ShouldBe(1.0);
            allExcluded.Activity.ShouldBe(1.0);
            allExcluded.ActivityStdDevCorrection.ShouldBe(1.0);
            allExcluded.GeometricCriticality.ShouldBe(1.0);
            allExcluded.GeometricFibonacci.ShouldBe(1.0);
            allExcluded.GeometricActivity.ShouldBe(1.0);

            RisksModel noActivities = service.BuildRiskMetrics(MakeCompilation(), false, DefaultSeverities());

            noActivities.ShouldBe(new RisksModel());
            noActivities.ShouldNotBe(allExcluded);
        }

        [Fact]
        public void BuildRiskMetrics_Given_NoActivities_Then_EverythingIsNull()
        {
            MetricCalculationService service = CreateService();

            RisksModel risks = service.BuildRiskMetrics(MakeCompilation(), false, DefaultSeverities());

            risks.ShouldBe(new RisksModel());
        }

        [Fact]
        public void BuildRiskMetrics_Given_CompilationErrors_Then_EverythingIsNull()
        {
            MetricCalculationService service = CreateService();

            RisksModel risks = service.BuildRiskMetrics(
                MakeCompilation(MakeActivityWithSlack(1, totalSlack: 0)),
                true,
                DefaultSeverities());

            risks.ShouldBe(new RisksModel());
        }

        #endregion

        #region BuildRiskMetrics - dummies

        [Fact]
        public void BuildRiskMetrics_Given_ADummyActivity_Then_ItIsLeftOut()
        {
            MetricCalculationService service = CreateService();

            var dummy = new DependentActivity(id: 99, duration: 0, canBeRemoved: true);
            dummy.IsDummy.ShouldBeTrue();

            RisksModel risks = service.BuildRiskMetrics(
                MakeCompilation(
                    MakeActivityWithSlack(1, totalSlack: 0),
                    MakeActivityWithSlack(2, totalSlack: 3),
                    dummy),
                false,
                DefaultSeverities());

            risks.ShouldBe(ExpectedRisksForSlacks(0, 3));
        }

        [Fact]
        public void BuildRiskMetrics_Given_ADummyMarkedWithNoRisk_Then_ItIsStillOnlyLeftOutOnce()
        {
            MetricCalculationService service = CreateService();

            var dummy = new DependentActivity(id: 99, duration: 0, canBeRemoved: true) { HasNoRisk = true };
            dummy.IsDummy.ShouldBeTrue();

            RisksModel risks = service.BuildRiskMetrics(
                MakeCompilation(MakeActivityWithSlack(1, totalSlack: 0), dummy),
                false,
                DefaultSeverities());

            risks.ShouldBe(ExpectedRisksForSlacks(0));
        }

        /// <summary>
        /// Dummies are dropped before the risk-bearing filter, so a plan of nothing
        /// but dummies takes the same fall-through as a plan of nothing but no-risk
        /// activities: a perfect score rather than a null one.
        /// </summary>
        [Fact]
        public void BuildRiskMetrics_Given_NothingButDummies_Then_ReportsAPerfectScore()
        {
            MetricCalculationService service = CreateService();

            RisksModel risks = service.BuildRiskMetrics(
                MakeCompilation(
                    new DependentActivity(id: 98, duration: 0, canBeRemoved: true),
                    new DependentActivity(id: 99, duration: 0, canBeRemoved: true)),
                false,
                DefaultSeverities());

            risks.Criticality.ShouldBe(1.0);
            risks.Activity.ShouldBe(1.0);
        }

        #endregion

        #region BuildRiskMetrics - the flags it must ignore

        [Fact]
        public void BuildRiskMetrics_Given_TheFinancialFlags_Then_TheyMakeNoDifference()
        {
            MetricCalculationService service = CreateService();

            RisksModel plain = service.BuildRiskMetrics(
                MakeCompilation(
                    MakeActivityWithSlack(1, totalSlack: 0),
                    MakeActivityWithSlack(2, totalSlack: 7)),
                false,
                DefaultSeverities());

            RisksModel flagged = service.BuildRiskMetrics(
                MakeCompilation(
                    MakeActivityWithSlack(1, totalSlack: 0, hasNoCost: true, hasNoBilling: true, hasNoEffort: true),
                    MakeActivityWithSlack(2, totalSlack: 7, hasNoCost: true, hasNoBilling: true, hasNoEffort: true)),
                false,
                DefaultSeverities());

            flagged.ShouldBe(plain);
        }

        /// <summary>
        /// Every combination of the four flags on a single activity: the risk metrics
        /// have to move when, and only when, HasNoRisk is the one that is set.
        /// </summary>
        [Theory]
        [InlineData(false, false, false, false)]
        [InlineData(true, false, false, false)]
        [InlineData(false, true, false, false)]
        [InlineData(false, false, true, false)]
        [InlineData(true, true, true, false)]
        [InlineData(false, false, false, true)]
        [InlineData(true, false, false, true)]
        [InlineData(false, true, false, true)]
        [InlineData(false, false, true, true)]
        [InlineData(true, true, true, true)]
        public void BuildRiskMetrics_Given_EveryFlagCombination_Then_OnlyHasNoRiskChangesTheOutcome(
            bool hasNoCost,
            bool hasNoBilling,
            bool hasNoEffort,
            bool hasNoRisk)
        {
            MetricCalculationService service = CreateService();

            RisksModel risks = service.BuildRiskMetrics(
                MakeCompilation(
                    MakeActivityWithSlack(1, totalSlack: 0),
                    MakeActivityWithSlack(
                        2,
                        totalSlack: 9,
                        hasNoCost: hasNoCost,
                        hasNoBilling: hasNoBilling,
                        hasNoEffort: hasNoEffort,
                        hasNoRisk: hasNoRisk)),
                false,
                DefaultSeverities());

            risks.ShouldBe(hasNoRisk ? ExpectedRisksForSlacks(0) : ExpectedRisksForSlacks(0, 9));
        }

        [Fact]
        public void BuildRiskMetrics_Given_ACompilation_Then_DoesNotMutateItsActivities()
        {
            MetricCalculationService service = CreateService();
            DependentActivity activity = MakeActivityWithSlack(1, totalSlack: 4, hasNoRisk: true);
            DependentActivity other = MakeActivityWithSlack(2, totalSlack: 0);

            service.BuildRiskMetrics(MakeCompilation(activity, other), false, DefaultSeverities());

            activity.HasNoRisk.ShouldBeTrue();
            activity.TotalSlack.ShouldBe(4);
            other.HasNoRisk.ShouldBeFalse();
            other.TotalSlack.ShouldBe(0);
        }

        #endregion

        #region BuildRiskMetrics - severities

        [Fact]
        public void BuildRiskMetrics_Given_ASingleSeverity_Then_UsesItAsTheCriticalWeight()
        {
            MetricCalculationService service = CreateService();
            List<ActivitySeverityModel> severities =
                [new ActivitySeverityModel { SlackLimit = 0, CriticalityWeight = 2.0, FibonacciWeight = 4.0 }];

            RisksModel risks = service.BuildRiskMetrics(
                MakeCompilation(MakeActivityWithSlack(1, totalSlack: 0)),
                false,
                severities);

            // One activity, at the only severity there is, so it scores full marks
            // against a critical weight that is itself that severity.
            risks.Criticality.ShouldBe(1.0);
            risks.Fibonacci.ShouldBe(1.0);
        }

        /// <summary>
        /// Current behaviour, pinned rather than endorsed: with no severities
        /// configured the critical weight cannot be found, and the risk build throws
        /// rather than returning an empty model. It throws even when every activity is
        /// excluded, because the weight is looked up before the excluded set is
        /// consulted.
        /// </summary>
        [Fact]
        public void BuildRiskMetrics_Given_NoSeverities_Then_Throws()
        {
            MetricCalculationService service = CreateService();

            Should.Throw<InvalidOperationException>(
                () => service.BuildRiskMetrics(
                    MakeCompilation(MakeActivityWithSlack(1, totalSlack: 0)),
                    false,
                    []));

            Should.Throw<InvalidOperationException>(
                () => service.BuildRiskMetrics(
                    MakeCompilation(MakeActivityWithSlack(1, totalSlack: 0, hasNoRisk: true)),
                    false,
                    []));
        }

        [Fact]
        public void BuildRiskMetrics_Given_NoSeveritiesAndNothingToAssess_Then_DoesNotThrow()
        {
            MetricCalculationService service = CreateService();

            RisksModel risks = service.BuildRiskMetrics(MakeCompilation(), false, []);

            risks.ShouldBe(new RisksModel());
        }

        #endregion

        #region BuildFinancialMetrics - shape

        [Fact]
        public void BuildFinancialMetrics_Given_NullSeriesSet_Then_Throws()
        {
            MetricCalculationService service = CreateService();
            Should.Throw<ArgumentNullException>(() => service.BuildFinancialMetrics(null!, false));
        }

        [Fact]
        public void BuildFinancialMetrics_Given_NoCombinedSeries_Then_EverythingIsDefault()
        {
            MetricCalculationService service = CreateService();

            var (costs, billings, margins, efforts, resourceMetrics) =
                service.BuildFinancialMetrics(new ResourceSeriesSetModel(), false);

            costs.ShouldBe(new CostsModel());
            billings.ShouldBe(new BillingsModel());
            margins.ShouldBe(new MarginsModel());
            efforts.ShouldBe(new EffortsModel());
            resourceMetrics.ShouldBeEmpty();
        }

        [Fact]
        public void BuildFinancialMetrics_Given_CompilationErrors_Then_EverythingIsDefault()
        {
            MetricCalculationService service = CreateService();
            ResourceSeriesSetModel seriesSet = MakeSeriesSet(
                MakeSeries(MakeSchedule(1, [true, true], [true, true], [true, true], [true, true])));

            var (costs, billings, margins, efforts, resourceMetrics) =
                service.BuildFinancialMetrics(seriesSet, true);

            costs.ShouldBe(new CostsModel());
            billings.ShouldBe(new BillingsModel());
            margins.ShouldBe(new MarginsModel());
            efforts.ShouldBe(new EffortsModel());
            resourceMetrics.ShouldBeEmpty();
        }

        /// <summary>
        /// Only the combined series are read. A series that appears in Scheduled or
        /// Unscheduled but not in Combined has already been folded into a combined
        /// entry, so counting it again would double the project's money.
        /// </summary>
        [Fact]
        public void BuildFinancialMetrics_Given_ScheduledAndUnscheduledSeries_Then_OnlyCombinedIsRead()
        {
            MetricCalculationService service = CreateService();
            ResourceSeriesModel series =
                MakeSeries(MakeSchedule(1, [true], [true], [true], [false]));

            var (costs, _, _, _, resourceMetrics) = service.BuildFinancialMetrics(
                new ResourceSeriesSetModel
                {
                    Combined = [series],
                    Scheduled = [series, series],
                    Unscheduled = [series],
                },
                false);

            resourceMetrics.Count.ShouldBe(1);
            costs.Total.ShouldBe(10.0);
        }

        [Fact]
        public void BuildFinancialMetrics_Given_SeveralSeries_Then_ListedInDisplayOrderThenIdDescending()
        {
            MetricCalculationService service = CreateService();

            ResourceSeriesSetModel seriesSet = MakeSeriesSet(
                MakeSeries(MakeSchedule(1, [true], [true], [true], [true]), title: @"One", displayOrder: 0),
                MakeSeries(MakeSchedule(3, [true], [true], [true], [true]), title: @"Three", displayOrder: 2),
                MakeSeries(MakeSchedule(2, [true], [true], [true], [true]), title: @"Two", displayOrder: 2),
                MakeSeries(MakeSchedule(0, [true], [true], [true], [true]), title: @"Spare", displayOrder: 0));

            var (_, _, _, _, resourceMetrics) = service.BuildFinancialMetrics(seriesSet, false);

            resourceMetrics.Select(x => x.ResourceName)
                .ShouldBe([@"Three", @"Two", @"One", @"Spare"]);

            // A series with no settings resource behind it reports no resource id.
            resourceMetrics.Single(x => x.ResourceName == @"Spare").ResourceId.ShouldBeNull();
            resourceMetrics.Single(x => x.ResourceName == @"One").ResourceId.ShouldBe(1);
        }

        [Fact]
        public void BuildFinancialMetrics_Given_SeveralSeries_Then_TheProjectTotalsAreTheSumOfTheParts()
        {
            MetricCalculationService service = CreateService();

            ResourceSeriesSetModel seriesSet = MakeSeriesSet(
                MakeSeries(
                    MakeSchedule(1, [true, true, false], [true, false, true], [true, true, true], [false, true, true]),
                    displayOrder: 1,
                    interActivityAllocationType: InterActivityAllocationType.Direct,
                    activityAllocationType: ActivityAllocationType.Direct,
                    unitCost: 10.0,
                    unitBilling: 20.0,
                    fixedCost: 5.0,
                    fixedBilling: 7.0),
                MakeSeries(
                    MakeSchedule(2, [true, true], [true, true], [true, false], [true, false]),
                    displayOrder: 2,
                    interActivityAllocationType: InterActivityAllocationType.Indirect,
                    activityAllocationType: ActivityAllocationType.Other,
                    unitCost: 8.0,
                    unitBilling: 4.0),
                MakeSeries(
                    MakeSchedule(3, [true, false, true], [false, true, true], [true, true, true], [false, false, true]),
                    displayOrder: 3,
                    interActivityAllocationType: InterActivityAllocationType.None,
                    activityAllocationType: ActivityAllocationType.Indirect,
                    unitCost: 5.0,
                    unitBilling: 6.0));

            var (costs, billings, margins, efforts, resourceMetrics) =
                service.BuildFinancialMetrics(seriesSet, false);

            costs.Direct.GetValueOrDefault().ShouldBe(resourceMetrics.Sum(x => x.Costs.Direct.GetValueOrDefault()), c_Tolerance);
            costs.Indirect.GetValueOrDefault().ShouldBe(resourceMetrics.Sum(x => x.Costs.Indirect.GetValueOrDefault()), c_Tolerance);
            costs.Other.GetValueOrDefault().ShouldBe(resourceMetrics.Sum(x => x.Costs.Other.GetValueOrDefault()), c_Tolerance);
            costs.Total.GetValueOrDefault().ShouldBe(
                costs.Direct.GetValueOrDefault() + costs.Indirect.GetValueOrDefault() + costs.Other.GetValueOrDefault(),
                c_Tolerance);

            billings.Direct.GetValueOrDefault().ShouldBe(resourceMetrics.Sum(x => x.Billings.Direct.GetValueOrDefault()), c_Tolerance);
            billings.Indirect.GetValueOrDefault().ShouldBe(resourceMetrics.Sum(x => x.Billings.Indirect.GetValueOrDefault()), c_Tolerance);
            billings.Other.GetValueOrDefault().ShouldBe(resourceMetrics.Sum(x => x.Billings.Other.GetValueOrDefault()), c_Tolerance);

            efforts.Total.GetValueOrDefault().ShouldBe(resourceMetrics.Sum(x => x.Efforts.Total.GetValueOrDefault()), c_Tolerance);
            efforts.Activity.GetValueOrDefault().ShouldBe(resourceMetrics.Sum(x => x.Efforts.Activity.GetValueOrDefault()), c_Tolerance);

            margins.ShouldBe(MetricsHelper.CalculateProjectMargins(costs, billings));
        }

        #endregion

        #region BuildFinancialMetrics - the activity flags, as the compiler leaves them

        // HasNoCost, HasNoBilling and HasNoEffort never reach this builder as flags.
        // The compiler resolves them while it schedules, and what arrives here is a
        // resource schedule whose cost, billing and effort allocations are already
        // false for the periods the flagged activities occupy - plus, for effort, the
        // flag itself carried through on the scheduled activity. These tests work in
        // those terms: an allocation that is on for a period the resource worked, and
        // off where the activity said not to charge for it.

        [Fact]
        public void BuildFinancialMetrics_Given_AnActivityWithNoCost_Then_ItsPeriodsAreNotCharged()
        {
            MetricCalculationService service = CreateService();

            // Two activity periods, the second of which is not to be charged for.
            ResourceSeriesModel charged = MakeSeries(
                MakeSchedule(1, [true, true], [true, true], [true, true], [true, true]));
            ResourceSeriesModel notCharged = MakeSeries(
                MakeSchedule(1, [true, false], [true, true], [true, true], [true, true]));

            var (chargedCosts, chargedBillings, _, chargedEfforts, _) =
                service.BuildFinancialMetrics(MakeSeriesSet(charged), false);
            var (costs, billings, _, efforts, _) =
                service.BuildFinancialMetrics(MakeSeriesSet(notCharged), false);

            chargedCosts.Total.ShouldBe(20.0);
            costs.Total.ShouldBe(10.0);

            // The billing and the effort are untouched by a cost exemption.
            billings.Total.ShouldBe(chargedBillings.Total);
            efforts.Total.ShouldBe(chargedEfforts.Total);
        }

        [Fact]
        public void BuildFinancialMetrics_Given_AnActivityWithNoBilling_Then_ItsPeriodsAreNotBilled()
        {
            MetricCalculationService service = CreateService();

            ResourceSeriesModel billed = MakeSeries(
                MakeSchedule(1, [true, true], [true, true], [true, true], [true, true]));
            ResourceSeriesModel notBilled = MakeSeries(
                MakeSchedule(1, [true, true], [true, false], [true, true], [true, true]));

            var (billedCosts, billedBillings, _, _, _) =
                service.BuildFinancialMetrics(MakeSeriesSet(billed), false);
            var (costs, billings, _, _, _) =
                service.BuildFinancialMetrics(MakeSeriesSet(notBilled), false);

            billedBillings.Total.ShouldBe(40.0);
            billings.Total.ShouldBe(20.0);
            costs.Total.ShouldBe(billedCosts.Total);
        }

        [Fact]
        public void BuildFinancialMetrics_Given_AnActivityWithNoEffort_Then_ItsDurationIsNotActivityEffort()
        {
            MetricCalculationService service = CreateService();

            // Both activities are worked; only one of them counts as activity effort.
            ResourceSeriesModel series = MakeSeries(
                MakeSchedule(
                    1,
                    [true, true, true, true],
                    [true, true, true, true],
                    [true, true, true, true],
                    [true, true, true, true],
                    new ScheduledActivityModel { Id = 1, Duration = 2 },
                    new ScheduledActivityModel { Id = 2, Duration = 2, HasNoEffort = true }));

            var (_, _, _, efforts, resourceMetrics) = service.BuildFinancialMetrics(MakeSeriesSet(series), false);

            // Four periods of effort in total, but only the two from the activity
            // that counts.
            efforts.Total.ShouldBe(4.0);
            efforts.Activity.ShouldBe(2.0);
            efforts.Efficiency.ShouldBe(0.5);

            resourceMetrics.ShouldHaveSingleItem().Efforts.Activity.ShouldBe(2.0);
        }

        [Fact]
        public void BuildFinancialMetrics_Given_EveryActivityHasNoEffort_Then_EfficiencyIsNull()
        {
            MetricCalculationService service = CreateService();

            ResourceSeriesModel series = MakeSeries(
                MakeSchedule(
                    1,
                    [true, true],
                    [true, true],
                    [true, true],
                    [true, true],
                    new ScheduledActivityModel { Id = 1, Duration = 2, HasNoEffort = true }));

            var (_, _, _, efforts, _) = service.BuildFinancialMetrics(MakeSeriesSet(series), false);

            efforts.Total.ShouldBe(2.0);
            efforts.Activity.ShouldBe(0.0);
            efforts.Efficiency.ShouldBeNull();
        }

        /// <summary>
        /// All three exemptions at once, which is what an activity that is present in
        /// the plan purely as a placeholder looks like by the time it reaches here:
        /// the resource is still allocated to it, but nothing is charged, billed or
        /// counted as effort for it.
        /// </summary>
        [Fact]
        public void BuildFinancialMetrics_Given_AnActivityExemptFromEverything_Then_OnlyTheAllocationRemains()
        {
            MetricCalculationService service = CreateService();

            ResourceSeriesModel series = MakeSeries(
                MakeSchedule(
                    1,
                    [false, false],
                    [false, false],
                    [false, false],
                    [true, true],
                    new ScheduledActivityModel { Id = 1, Duration = 2, HasNoCost = true, HasNoBilling = true, HasNoEffort = true }));

            var (costs, billings, margins, efforts, resourceMetrics) =
                service.BuildFinancialMetrics(MakeSeriesSet(series), false);

            costs.Total.ShouldBe(0.0);
            billings.Total.ShouldBe(0.0);
            efforts.Total.ShouldBe(0.0);
            efforts.Activity.ShouldBe(0.0);
            efforts.Efficiency.ShouldBeNull();

            // Nothing charged and nothing billed leaves no margin to report on.
            margins.TotalAbsolute.ShouldBe(0.0);
            margins.Total.ShouldBe(0.0);

            // The resource is still listed, so the plan still shows it was involved.
            resourceMetrics.ShouldHaveSingleItem().ResourceId.ShouldBe(1);
        }

        /// <summary>
        /// Current behaviour, pinned rather than endorsed: a resource that costs
        /// something but bills nothing - every activity it worked marked as not to be
        /// billed - reports a null margin rather than a loss, because the percentage
        /// has nothing to divide by. The absolute margin does show the loss.
        /// </summary>
        [Fact]
        public void BuildFinancialMetrics_Given_CostWithNoBillingAtAll_Then_TheMarginIsNull()
        {
            MetricCalculationService service = CreateService();

            ResourceSeriesModel series = MakeSeries(
                MakeSchedule(1, [true, true], [false, false], [true, true], [true, true]));

            var (costs, billings, margins, _, resourceMetrics) =
                service.BuildFinancialMetrics(MakeSeriesSet(series), false);

            costs.Total.ShouldBe(20.0);
            billings.Total.ShouldBe(0.0);

            margins.TotalAbsolute.ShouldBe(-20.0);
            margins.Total.ShouldBeNull();

            resourceMetrics.ShouldHaveSingleItem().Margins.Total.ShouldBeNull();
        }

        /// <summary>
        /// Fixed costs and billings are not allocated to any activity, so no activity
        /// flag can exempt them: a resource whose every period is exempt still carries
        /// its fixed charges.
        /// </summary>
        [Fact]
        public void BuildFinancialMetrics_Given_EveryPeriodExemptButFixedCharges_Then_TheFixedChargesRemain()
        {
            MetricCalculationService service = CreateService();

            ResourceSeriesModel series = MakeSeries(
                MakeSchedule(1, [false, false], [false, false], [false, false], [true, true]),
                interActivityAllocationType: InterActivityAllocationType.Indirect,
                fixedCost: 100.0,
                fixedBilling: 250.0);

            var (costs, billings, margins, _, _) = service.BuildFinancialMetrics(MakeSeriesSet(series), false);

            costs.Indirect.ShouldBe(100.0);
            costs.Total.ShouldBe(100.0);
            billings.Indirect.ShouldBe(250.0);
            billings.Total.ShouldBe(250.0);
            margins.TotalAbsolute.ShouldBe(150.0);
            margins.Total.GetValueOrDefault().ShouldBe(150.0 / 250.0, c_Tolerance);
        }

        /// <summary>
        /// The inter-activity portion and the activity portion of one resource can
        /// land in different buckets, so an exemption has to be read against the right
        /// one. Here the resource is indirect between activities and direct while
        /// working on them, and only the worked periods are exempt.
        /// </summary>
        [Fact]
        public void BuildFinancialMetrics_Given_ExemptWorkedPeriodsOnASplitResource_Then_OnlyTheActivityBucketDrops()
        {
            MetricCalculationService service = CreateService();

            ResourceSeriesModel worked = MakeSeries(
                MakeSchedule(1, [true, true], [true, true], [true, true], [false, true]),
                interActivityAllocationType: InterActivityAllocationType.Indirect,
                activityAllocationType: ActivityAllocationType.Direct);

            ResourceSeriesModel exempt = MakeSeries(
                MakeSchedule(1, [true, false], [true, false], [true, true], [false, true]),
                interActivityAllocationType: InterActivityAllocationType.Indirect,
                activityAllocationType: ActivityAllocationType.Direct);

            var (workedCosts, _, _, _, _) = service.BuildFinancialMetrics(MakeSeriesSet(worked), false);
            var (exemptCosts, _, _, _, _) = service.BuildFinancialMetrics(MakeSeriesSet(exempt), false);

            // Idle period indirect, worked period direct.
            workedCosts.Indirect.ShouldBe(10.0);
            workedCosts.Direct.ShouldBe(10.0);

            // The exemption takes the worked period out and leaves the idle one.
            exemptCosts.Indirect.ShouldBe(10.0);
            exemptCosts.Direct.ShouldBe(0.0);
        }

        #endregion
    }
}
