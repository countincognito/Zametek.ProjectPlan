using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for the outputs that the core builds from a compilation (the graphs, the series and the metrics): that each
    /// build reads what it should, and that a plan which does not compile has no graphs.
    ///
    /// CompilationOutputService builds them, and the core gives it the inputs. A cascade and the Build methods of the core
    /// reach the same builds by two routes, so a test goes through both where it can. A scenario load runs the cascade
    /// without the pipeline, and so a test can see its outputs without starting the core.
    /// </summary>
    public class CoreViewModelOutputTests
    {
        private static readonly JsonSerializerOptions s_DescribeOptions = new()
        {
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        };

        // The outputs as text, so that two sets of outputs can be compared by what is in them.
        private static string Describe(params object?[] outputs) =>
            JsonSerializer.Serialize(outputs, s_DescribeOptions);

        [Fact]
        public void ProcessProjectScenario_Given_APlanWithCompilationErrors_Then_ItsGraphsAreEmpty()
        {
            // Two activities that depend on each other.
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(2) with
            {
                DependentActivities =
                [
                    new DependentActivityModel { Activity = new ActivityModel { Id = 1, DisplayOrder = 1, Name = "A", Duration = 3 }, Dependencies = [2] },
                    new DependentActivityModel { Activity = new ActivityModel { Id = 2, DisplayOrder = 2, Name = "B", Duration = 4 }, Dependencies = [1] },
                ],
            };
            string empty = Describe(new ArrowGraphModel(), new VertexGraphModel());
            using CoreViewModel core = CoreViewModelFixture.Create();

            // A load builds the outputs with the cascade.
            core.ProcessProjectScenario(scenario, Guid.NewGuid(), @"Test");

            core.HasCompilationErrors.ShouldBeTrue();
            Describe(core.ArrowGraph, core.VertexGraph).ShouldBe(empty, "the cascade built graphs for a plan that does not compile");

            // The Build methods read the same flag.
            core.BuildArrowGraph();
            core.BuildVertexGraph();

            Describe(core.ArrowGraph, core.VertexGraph).ShouldBe(empty, "a Build method built a graph for a plan that does not compile");
        }

        #region The times of the vertex graph

        // The vertex graph shows the schedule that the compilation of the plan made. Its build does not schedule the
        // resources a second time: a second schedule can be a different one, and the graph then shows other times than
        // the list of the activities and the arrow graph. GraphCompilationServiceTests has the reason and more plans.

        // The times that the list of the activities shows for each activity, and that a node of the vertex graph shows.
        private static List<string> ActivityTimes(CoreViewModel core) =>
            [.. core.RawActivities
                .OrderBy(x => x.Id)
                .Select(x => $@"{x.Id}: {x.EarliestStartTime}-{x.EarliestFinishTime}, {x.LatestStartTime}-{x.LatestFinishTime}, total slack {x.TotalSlack}")];

        private static List<string> VertexGraphTimes(CoreViewModel core) =>
            [.. core.VertexGraph.Nodes
                .Select(x => x.Content)
                .OrderBy(x => x.Id)
                .Select(x => $@"{x.Id}: {x.EarliestStartTime}-{x.EarliestFinishTime}, {x.LatestStartTime}-{x.LatestFinishTime}, total slack {x.TotalSlack}")];

        // The finish of the arrow graph: the latest time of its events.
        private static int? ArrowGraphFinish(CoreViewModel core) =>
            core.ArrowGraph.Nodes.Max(x => x.Content.EarliestFinishTime);

        [Fact]
        public void ProcessProjectScenario_Given_APlanThatASecondScheduleMakesLonger_Then_TheVertexGraphHasTheTimesOfTheActivities()
        {
            // Two resources that an activity must name to get. The scheduler tries resource 1 first.
            static ResourceModel Resource(int id, int allocationOrder) => new()
            {
                Id = id,
                Name = $@"Resource {id}",
                DisplayOrder = id,
                IsExplicitTarget = true,
                AllocationOrder = allocationOrder,
                InterActivityAllocationType = InterActivityAllocationType.Direct,
                UnitCost = 1.0,
                UnitBilling = 1.0,
                ColorFormat = new ColorFormatModel { A = 255, R = 10, G = 20, B = 30 },
            };

            // An activity that one of its target resources does.
            static DependentActivityModel Activity(int id, int duration, List<int> targetResources, params int[] dependencies) => new()
            {
                Activity = new ActivityModel
                {
                    Id = id,
                    DisplayOrder = id,
                    Name = $@"Activity {id}",
                    Duration = duration,
                    TargetResources = targetResources,
                    TargetResourceOperator = LogicalOperator.OR,
                },
                Dependencies = [.. dependencies],
            };

            // The plan of GraphCompilationServiceTests.PlanThatASecondScheduleMakesLonger, which says why a second schedule
            // differs. Activity 1 is on resource 1, and the other three come after it. Either resource can do activity 2
            // (one day) and activity 3 (two days), and only resource 2 can do activity 4 (one day). The schedule of the plan
            // finishes at time 3, with activity 4 after activity 2 on resource 2. A second schedule that starts from this
            // one finishes at time 4.
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(4) with
            {
                DependentActivities =
                [
                    Activity(1, 1, [1]),
                    Activity(2, 1, [1, 2], 1),
                    Activity(3, 2, [1, 2], 1),
                    Activity(4, 1, [2], 1),
                ],
                ResourceSettings = new ResourceSettingsModel
                {
                    Resources = [Resource(1, allocationOrder: 0), Resource(2, allocationOrder: 1)],
                    DefaultUnitCost = 1.0,
                    DefaultUnitBilling = 1.0,
                    AreDisabled = false,
                },
            };
            using CoreViewModel core = CoreViewModelFixture.Create();

            // A load builds the outputs with the cascade.
            core.ProcessProjectScenario(scenario, Guid.NewGuid(), @"Test");

            core.HasCompilationErrors.ShouldBeFalse();
            core.RawActivities.Max(x => x.EarliestFinishTime).ShouldBe(3);
            core.RawActivities.Single(x => x.Id == 4).ResourceDependencies.ShouldBe(new HashSet<int> { 2 });

            VertexGraphTimes(core).ShouldBe(ActivityTimes(core), "the cascade built a vertex graph with another schedule than that of the activities");
            ArrowGraphFinish(core).ShouldBe(3, "the arrow graph does not finish when the activities do");

            // The Build method gives the same graph.
            core.BuildVertexGraph();

            VertexGraphTimes(core).ShouldBe(ActivityTimes(core), "the Build method built a vertex graph with another schedule than that of the activities");
        }

        #endregion

        [Fact]
        public void BuildNetworkMetrics_Given_ATodayThatIsNotTheProjectStart_Then_TheDurationInManMonthsIsThatOfTheStart()
        {
            // With weekends off, the days between the start and the finish of one plan depend on the day of the week that
            // it starts on. The plan lasts 47 days, which are nine weeks and two days of work. 2026-01-05 is a Monday and
            // 2026-01-08 is a Thursday.
            var monday = new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.Zero);
            var thursday = new DateTimeOffset(2026, 1, 8, 12, 0, 0, TimeSpan.Zero);
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(12, durationOffset: 1) with
            {
                ProjectStart = monday,
                Today = monday,
                DisplaySettings = new ProjectScenarioDisplaySettingsModel { NonWorkingDayMode = NonWorkingDayMode.Weekends },
            };
            using CoreViewModel core = CoreViewModelFixture.Create();
            core.ProcessProjectScenario(scenario, Guid.NewGuid(), @"Test");

            core.BuildNetworkMetrics();
            double? startedOnTheMonday = core.NetworkMetrics.DurationManMonths;
            startedOnTheMonday.ShouldNotBeNull();

            // Today moves and the start does not.
            core.Today = thursday;
            core.BuildNetworkMetrics();

            core.NetworkMetrics.DurationManMonths.ShouldBe(startedOnTheMonday, "the duration followed Today and not the start of the project");

            // The check above means something only if the start does move the duration.
            core.RequestedProjectStart = thursday;
            core.BuildNetworkMetrics();

            core.NetworkMetrics.DurationManMonths.ShouldNotBe(startedOnTheMonday, "the start of the project does not move the duration");
        }

        #region Colors of the resources that have none

        // A resource without a color of its own takes the next preset color. The series of the resources take the presets
        // from the first, and the series of the tracking carry on where those stopped. The plan has two resources, and each
        // has activities, so that the series of the resources draw two colors and the series of the tracking draw the next two.

        private static List<ColorFormatModel> Presets(int skip, int take) =>
            [.. ColorHelper.PresetColors.Skip(skip).Take(take).Select(ColorHelper.AvaloniaColorToColorFormatModel)];

        private static List<ColorFormatModel> ResourceSeriesColors(CoreViewModel core) =>
            [.. core.ResourceSeriesSet.Scheduled.Select(x => x.ColorFormat)];

        private static List<ColorFormatModel> TrackingSeriesColors(CoreViewModel core) =>
            [.. core.TrackingSeriesSet.ByResource.Select(x => x.ColorFormat)];

        [Fact]
        public void ProcessProjectScenario_Given_ResourcesWithoutColors_Then_TheTrackingSeriesCarryOnFromTheResourceSeries()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();

            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(6, coloredResources: false), Guid.NewGuid(), @"Test");

            ResourceSeriesColors(core).ShouldBe(Presets(0, 2));
            TrackingSeriesColors(core).ShouldBe(Presets(2, 2));
        }

        [Fact]
        public void BuildTrackingSeriesSet_Given_ResourcesWithoutColors_Then_ItCarriesOnFromTheBuildOfTheResourceSeries()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(6, coloredResources: false), Guid.NewGuid(), @"Test");

            // The pair twice. The series of the resources start the sequence again, and so the second pair is the first.
            for (int pair = 1; pair <= 2; pair++)
            {
                core.BuildResourceSeriesSet();
                core.BuildTrackingSeriesSet();

                ResourceSeriesColors(core).ShouldBe(Presets(0, 2), $@"the resource series of pair {pair}");
                TrackingSeriesColors(core).ShouldBe(Presets(2, 2), $@"the tracking series of pair {pair}");
            }
        }

        [Fact]
        public void BuildTrackingSeriesSet_Given_ACascadeBetweenItAndTheBuildOfTheResourceSeries_Then_ItIsAsIfThereWasNone()
        {
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(6, coloredResources: false);
            using CoreViewModel core = CoreViewModelFixture.Create();
            core.ProcessProjectScenario(scenario, Guid.NewGuid(), @"Test");
            core.BuildResourceSeriesSet();

            // A load runs a cascade, which draws colors for both of its series.
            core.ProcessProjectScenario(scenario, Guid.NewGuid(), @"Test");
            core.BuildTrackingSeriesSet();

            TrackingSeriesColors(core).ShouldBe(Presets(2, 2), "the cascade moved the colors of a build that was not its own");
        }

        #endregion
    }
}
