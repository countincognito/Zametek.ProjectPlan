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
    /// The metric builders end to end: a plan is loaded, compiled by the real
    /// compiler and scheduled by the real scheduler, and only then are the metrics
    /// built.
    ///
    /// This is the only place the four flags can be exercised as flags. HasNoRisk
    /// reaches the risk builder on the activity itself, but HasNoCost, HasNoBilling
    /// and HasNoEffort are resolved during scheduling: what the financial builder
    /// receives is a resource schedule whose cost, billing and effort allocations
    /// have already been switched off for the periods the flagged activities occupy.
    /// Asserting on hand-built allocations pins the convention; running a compile
    /// pins that the compiler and the metrics still agree on it.
    ///
    /// The assertions are differential wherever the exact figure depends on which
    /// resource the scheduler happened to pick: the same plan is built twice, with
    /// and without a flag, and the difference between the two is what is checked.
    /// </summary>
    public class MetricCalculationServiceCompilationTests
    {
        private const double c_Tolerance = 0.000000001;

        #region Helpers

        /// <summary>
        /// Two resources and three activities, arranged so that the plan has both a
        /// critical path and some slack on it. A1 (five days) and A2 (two days) start
        /// together on a resource each, A3 waits for both, so A2 has three days of
        /// slack while A1 and A3 have none. The flags are applied to A2, the activity
        /// with the slack, so that excluding it visibly moves the risk metrics.
        /// </summary>
        private static ProjectScenarioModel CreateScenario(
            bool hasNoCost = false,
            bool hasNoBilling = false,
            bool hasNoEffort = false,
            bool hasNoRisk = false)
        {
            List<ResourceModel> resources =
            [
                new ResourceModel
                {
                    Id = 1,
                    Name = @"Resource One",
                    DisplayOrder = 1,
                    IsExplicitTarget = false,
                    InterActivityAllocationType = InterActivityAllocationType.Direct,
                    ActivityAllocationType = ActivityAllocationType.Direct,
                    UnitCost = 10.0,
                    UnitBilling = 20.0,
                    ColorFormat = new ColorFormatModel { A = 255, R = 10, G = 20, B = 30 },
                },
                new ResourceModel
                {
                    Id = 2,
                    Name = @"Resource Two",
                    DisplayOrder = 2,
                    IsExplicitTarget = false,
                    InterActivityAllocationType = InterActivityAllocationType.Direct,
                    ActivityAllocationType = ActivityAllocationType.Direct,
                    UnitCost = 10.0,
                    UnitBilling = 20.0,
                    ColorFormat = new ColorFormatModel { A = 255, R = 40, G = 50, B = 60 },
                },
            ];

            List<DependentActivityModel> activities =
            [
                new DependentActivityModel
                {
                    Activity = new ActivityModel
                    {
                        Id = 1,
                        DisplayOrder = 1,
                        Name = @"Activity One",
                        Duration = 5,
                        Trackers =
                        [
                            new ActivityTrackerModel { Time = 0, ActivityId = 1, PercentageComplete = 25 },
                            new ActivityTrackerModel { Time = 1, ActivityId = 1, PercentageComplete = 60 },
                        ],
                    },
                },
                new DependentActivityModel
                {
                    Activity = new ActivityModel
                    {
                        Id = 2,
                        DisplayOrder = 2,
                        Name = @"Activity Two",
                        Duration = 2,
                        HasNoCost = hasNoCost,
                        HasNoBilling = hasNoBilling,
                        HasNoEffort = hasNoEffort,
                        HasNoRisk = hasNoRisk,
                    },
                },
                new DependentActivityModel
                {
                    Activity = new ActivityModel { Id = 3, DisplayOrder = 3, Name = @"Activity Three", Duration = 3 },
                    Dependencies = [1, 2],
                },
            ];

            return new ProjectScenarioModel
            {
                ProjectStart = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                Today = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                DependentActivities = activities,
                ResourceSettings = new ResourceSettingsModel
                {
                    Resources = resources,
                    DefaultUnitCost = 10.0,
                    DefaultUnitBilling = 20.0,
                    AreDisabled = false,
                },
                WorkStreamSettings = new WorkStreamSettingsModel(),
                HolidaySettings = new HolidaySettingsModel(),
                GraphSettings = CoreViewModelFixture.DefaultGraphSettings,
                DisplaySettings = new ProjectScenarioDisplaySettingsModel(),
            };
        }

        /// <summary>
        /// Loads the scenario, compiles it and rebuilds every metric, in the order the
        /// command line tool uses. Loading a scenario replaces the computed metrics
        /// with the ones stored in the file, and this scenario carries none, so the
        /// rebuild is what puts the numbers back.
        /// </summary>
        private static (MetricsModel metrics, List<ResourceMetricsModel> resourceMetrics, IReadOnlyList<IManagedActivityViewModel> activities)
            BuildMetrics(CoreViewModel core, ProjectScenarioModel scenario)
        {
            core.ProcessProjectScenario(scenario, Guid.NewGuid(), @"Test");
            core.HasCompilationErrors.ShouldBeFalse();

            core.BuildResourceSeriesSet();
            core.BuildNetworkMetrics();
            core.BuildRiskMetrics();
            core.BuildFinancialMetrics();

            return (core.Metrics, core.ResourceMetrics, core.RawActivities);
        }

        private static MetricsModel MetricsFor(
            bool hasNoCost = false,
            bool hasNoBilling = false,
            bool hasNoEffort = false,
            bool hasNoRisk = false)
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            return BuildMetrics(core, CreateScenario(hasNoCost, hasNoBilling, hasNoEffort, hasNoRisk)).metrics;
        }

        #endregion

        #region The plan the tests rest on

        [Fact]
        public void TheScenario_Compiles_IntoThePlanTheOtherTestsAssume()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            var (metrics, resourceMetrics, activities) = BuildMetrics(core, CreateScenario());

            activities.Count.ShouldBe(3);

            // A1 and A2 in parallel, A3 after both: eight days from end to end.
            metrics.Network.Duration.ShouldBe(8);

            IManagedActivityViewModel first = activities.Single(x => x.Id == 1);
            IManagedActivityViewModel second = activities.Single(x => x.Id == 2);
            IManagedActivityViewModel third = activities.Single(x => x.Id == 3);

            first.TotalSlack.ShouldBe(0);
            second.TotalSlack.ShouldBe(3);
            third.TotalSlack.ShouldBe(0);

            // Both resources are in play, so the two independent activities really did
            // run side by side rather than being serialised onto one resource.
            resourceMetrics.Count(x => x.ResourceId is not null).ShouldBe(2);
        }

        [Fact]
        public void TheFlags_SurviveTheCompile_OntoTheLiveActivities()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            var (_, _, activities) = BuildMetrics(
                core,
                CreateScenario(hasNoCost: true, hasNoBilling: true, hasNoEffort: true, hasNoRisk: true));

            IManagedActivityViewModel flagged = activities.Single(x => x.Id == 2);

            flagged.HasNoCost.ShouldBeTrue();
            flagged.HasNoBilling.ShouldBeTrue();
            flagged.HasNoEffort.ShouldBeTrue();
            flagged.HasNoRisk.ShouldBeTrue();

            IManagedActivityViewModel plain = activities.Single(x => x.Id == 1);

            plain.HasNoCost.ShouldBeFalse();
            plain.HasNoBilling.ShouldBeFalse();
            plain.HasNoEffort.ShouldBeFalse();
            plain.HasNoRisk.ShouldBeFalse();
        }

        /// <summary>
        /// Building a scenario maps every activity twice over - once from the live
        /// view models, which is what is written to the file, and once from the
        /// compiled copies. Neither may cost the plan its progress trackers, and
        /// neither may take them off the activities still in play.
        /// </summary>
        [Fact]
        public void BuildProjectScenario_Given_ACompiledPlan_Then_KeepsTheTrackersOnBothSides()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            var (_, _, activities) = BuildMetrics(core, CreateScenario());

            activities.Single(x => x.Id == 1).TrackerSet.Trackers.Count.ShouldBe(2);

            ProjectScenarioModel rebuilt = core.BuildProjectScenario();

            rebuilt.DependentActivities
                .Single(x => x.Activity.Id == 1).Activity.Trackers
                .Select(x => x.PercentageComplete)
                .ShouldBe([25, 60]);

            // Still there afterwards: building a scenario reads the plan, it does not
            // consume it. Doing it twice has to give the same answer.
            activities.Single(x => x.Id == 1).TrackerSet.Trackers.Count.ShouldBe(2);

            core.BuildProjectScenario()
                .DependentActivities.Single(x => x.Activity.Id == 1).Activity.Trackers
                .Select(x => x.PercentageComplete)
                .ShouldBe([25, 60]);
        }

        #endregion

        #region HasNoRisk, all the way through

        /// <summary>
        /// The end to end form of the regression: the flag is set on a compiled plan,
        /// and the risk metrics have to notice. While the flag was being dropped in
        /// mapping, these two were identical.
        /// </summary>
        [Fact]
        public void HasNoRisk_Given_ACompiledPlan_Then_TheRiskMetricsChange()
        {
            RisksModel counted = MetricsFor().Risks;
            RisksModel excluded = MetricsFor(hasNoRisk: true).Risks;

            excluded.ShouldNotBe(counted);
        }

        /// <summary>
        /// And it changes to exactly the right thing: the metrics for the two
        /// activities that are left, which both sit on the critical path.
        /// </summary>
        [Fact]
        public void HasNoRisk_Given_ACompiledPlan_Then_TheExcludedActivityIsGone()
        {
            RisksModel excluded = MetricsFor(hasNoRisk: true).Risks;

            // A1 and A3 have no slack, so what is left is a plan that is entirely
            // critical: every risk metric reads one.
            excluded.Criticality.ShouldBe(1.0);
            excluded.Fibonacci.ShouldBe(1.0);
            excluded.Activity.ShouldBe(1.0);
            excluded.GeometricCriticality.ShouldBe(1.0);
            excluded.GeometricFibonacci.ShouldBe(1.0);
            excluded.GeometricActivity.ShouldBe(1.0);

            // With the slack of A2 counted, the plan is not entirely critical.
            MetricsFor().Risks.Criticality!.Value.ShouldBeLessThan(1.0);
        }

        [Fact]
        public void HasNoRisk_Given_ACompiledPlan_Then_ItDoesNotTouchTheOtherMetrics()
        {
            MetricsModel plain = MetricsFor();
            MetricsModel excluded = MetricsFor(hasNoRisk: true);

            excluded.Network.ShouldBe(plain.Network);
            excluded.Costs.ShouldBe(plain.Costs);
            excluded.Billings.ShouldBe(plain.Billings);
            excluded.Margins.ShouldBe(plain.Margins);
            excluded.Efforts.ShouldBe(plain.Efforts);
        }

        #endregion

        #region HasNoCost, HasNoBilling and HasNoEffort, all the way through

        [Fact]
        public void HasNoCost_Given_ACompiledPlan_Then_TheActivitysDaysAreNotCharged()
        {
            MetricsModel plain = MetricsFor();
            MetricsModel exempt = MetricsFor(hasNoCost: true);

            // Two days of the activity, at ten a day.
            exempt.Costs.Total.GetValueOrDefault()
                .ShouldBe(plain.Costs.Total.GetValueOrDefault() - 20.0, c_Tolerance);

            exempt.Billings.ShouldBe(plain.Billings);
            exempt.Efforts.ShouldBe(plain.Efforts);
            exempt.Risks.ShouldBe(plain.Risks);
            exempt.Network.ShouldBe(plain.Network);
        }

        [Fact]
        public void HasNoBilling_Given_ACompiledPlan_Then_TheActivitysDaysAreNotBilled()
        {
            MetricsModel plain = MetricsFor();
            MetricsModel exempt = MetricsFor(hasNoBilling: true);

            // Two days of the activity, at twenty a day.
            exempt.Billings.Total.GetValueOrDefault()
                .ShouldBe(plain.Billings.Total.GetValueOrDefault() - 40.0, c_Tolerance);

            exempt.Costs.ShouldBe(plain.Costs);
            exempt.Efforts.ShouldBe(plain.Efforts);
            exempt.Risks.ShouldBe(plain.Risks);
            exempt.Network.ShouldBe(plain.Network);
        }

        [Fact]
        public void HasNoEffort_Given_ACompiledPlan_Then_TheActivitysDaysAreNotEffort()
        {
            MetricsModel plain = MetricsFor();
            MetricsModel exempt = MetricsFor(hasNoEffort: true);

            // The activity's own two days stop counting as activity effort.
            exempt.Efforts.Activity.GetValueOrDefault()
                .ShouldBe(plain.Efforts.Activity.GetValueOrDefault() - 2.0, c_Tolerance);

            exempt.Costs.ShouldBe(plain.Costs);
            exempt.Billings.ShouldBe(plain.Billings);
            exempt.Risks.ShouldBe(plain.Risks);
            exempt.Network.ShouldBe(plain.Network);
        }

        [Fact]
        public void EveryFlagAtOnce_Given_ACompiledPlan_Then_EachOneStillActsOnItsOwnMetric()
        {
            MetricsModel plain = MetricsFor();
            MetricsModel exempt = MetricsFor(hasNoCost: true, hasNoBilling: true, hasNoEffort: true, hasNoRisk: true);

            exempt.Costs.Total.GetValueOrDefault()
                .ShouldBe(MetricsFor(hasNoCost: true).Costs.Total.GetValueOrDefault(), c_Tolerance);
            exempt.Billings.Total.GetValueOrDefault()
                .ShouldBe(MetricsFor(hasNoBilling: true).Billings.Total.GetValueOrDefault(), c_Tolerance);
            exempt.Efforts.Activity.GetValueOrDefault()
                .ShouldBe(MetricsFor(hasNoEffort: true).Efforts.Activity.GetValueOrDefault(), c_Tolerance);
            exempt.Risks.ShouldBe(MetricsFor(hasNoRisk: true).Risks);

            // The plan itself is unchanged: the flags say what to charge for, not what
            // to schedule.
            exempt.Network.ShouldBe(plain.Network);
        }

        /// <summary>
        /// The per-resource breakdown has to move with the project totals, since the
        /// project figures are folded from it - an exemption that only showed up in
        /// one of the two would mean the panel and the summary disagreed.
        /// </summary>
        [Fact]
        public void TheFlags_Given_ACompiledPlan_Then_ThePerResourceBreakdownAgreesWithTheTotals()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            var (metrics, resourceMetrics, _) = BuildMetrics(
                core,
                CreateScenario(hasNoCost: true, hasNoBilling: true, hasNoEffort: true));

            resourceMetrics.ShouldNotBeEmpty();

            metrics.Costs.Total.GetValueOrDefault()
                .ShouldBe(resourceMetrics.Sum(x => x.Costs.Total.GetValueOrDefault()), c_Tolerance);
            metrics.Billings.Total.GetValueOrDefault()
                .ShouldBe(resourceMetrics.Sum(x => x.Billings.Total.GetValueOrDefault()), c_Tolerance);
            metrics.Efforts.Total.GetValueOrDefault()
                .ShouldBe(resourceMetrics.Sum(x => x.Efforts.Total.GetValueOrDefault()), c_Tolerance);
            metrics.Efforts.Activity.GetValueOrDefault()
                .ShouldBe(resourceMetrics.Sum(x => x.Efforts.Activity.GetValueOrDefault()), c_Tolerance);
        }

        #endregion
    }
}
