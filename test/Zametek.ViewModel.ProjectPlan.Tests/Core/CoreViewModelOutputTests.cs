using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using Zametek.Common.ProjectPlan;

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
            core.ProjectStart = thursday;
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
