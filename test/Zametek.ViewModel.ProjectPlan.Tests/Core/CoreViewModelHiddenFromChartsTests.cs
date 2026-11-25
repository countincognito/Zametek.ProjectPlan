using Shouldly;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The switches that hide parts of the plan from the Gantt chart and the resource chart: IsHiddenFromCharts of an
    /// activity, a resource and a work stream, and the two switches that show the hidden parts again. A scenario keeps them
    /// through a load and a save.
    /// </summary>
    public class CoreViewModelHiddenFromChartsTests
    {
        [Fact]
        public void BuildProjectScenario_Given_AScenarioWithEachSwitch_Then_TheSavedScenarioHasEachSwitchAsItWasLoaded()
        {
            using CoreViewModel core = Load(PlanWithSwitches());

            ProjectScenarioModel saved = core.BuildProjectScenario();

            saved.DependentActivities.OrderBy(x => x.Activity.Id).Select(x => x.Activity.IsHiddenFromCharts)
                .ShouldBe([false, true, false]);
            saved.ResourceSettings.Resources.OrderBy(x => x.Id).Select(x => x.IsHiddenFromCharts)
                .ShouldBe([true, false]);
            saved.WorkStreamSettings.WorkStreams.OrderBy(x => x.Id).Select(x => x.IsHiddenFromCharts)
                .ShouldBe([false, true]);
            saved.DisplaySettings.GanttChartShowHidden.ShouldBeTrue();
            saved.DisplaySettings.ResourceChartShowHidden.ShouldBeFalse();
        }

        [Fact]
        public void BuildProjectScenario_Given_AScenarioWithNoSwitch_Then_NothingIsHidden()
        {
            using CoreViewModel core = Load(CoreViewModelFixture.CreateProjectScenario(3));

            ProjectScenarioModel saved = core.BuildProjectScenario();

            saved.DependentActivities.ShouldAllBe(x => !x.Activity.IsHiddenFromCharts);
            saved.ResourceSettings.Resources.ShouldAllBe(x => !x.IsHiddenFromCharts);
            saved.DisplaySettings.GanttChartShowHidden.ShouldBeFalse();
            saved.DisplaySettings.ResourceChartShowHidden.ShouldBeFalse();
        }

        [Fact]
        public async Task OpenProjectFile_Given_AFileOfVersion0_6_1_Then_NothingIsHiddenFromTheCharts()
        {
            ProjectScenarioModel scenario = await CoreViewModelFixture.LoadProjectScenarioAsync(@"sample_v0_6_1.zpp");

            scenario.DependentActivities.ShouldNotBeEmpty();
            scenario.DependentActivities.ShouldAllBe(x => !x.Activity.IsHiddenFromCharts);
            scenario.ResourceSettings.Resources.ShouldNotBeEmpty();
            scenario.ResourceSettings.Resources.ShouldAllBe(x => !x.IsHiddenFromCharts);
            scenario.WorkStreamSettings.WorkStreams.ShouldNotBeEmpty();
            scenario.WorkStreamSettings.WorkStreams.ShouldAllBe(x => !x.IsHiddenFromCharts);
            scenario.DisplaySettings.GanttChartShowHidden.ShouldBeFalse();
            scenario.DisplaySettings.ResourceChartShowHidden.ShouldBeFalse();
        }

        // Three activities with two resources and two work streams: activity 2, resource 1 and work stream 2 are hidden,
        // and the Gantt chart shows the hidden parts.
        private static ProjectScenarioModel PlanWithSwitches()
        {
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(3);
            return scenario with
            {
                DependentActivities =
                [
                    .. scenario.DependentActivities.Select(x => x with
                    {
                        Activity = x.Activity with { IsHiddenFromCharts = x.Activity.Id == 2, TargetWorkStreams = [1 + (x.Activity.Id % 2)] },
                    }),
                ],
                ResourceSettings = scenario.ResourceSettings with
                {
                    Resources = [.. scenario.ResourceSettings.Resources.Select(x => x with { IsHiddenFromCharts = x.Id == 1 })],
                },
                WorkStreamSettings = new WorkStreamSettingsModel
                {
                    WorkStreams =
                    [
                        new WorkStreamModel { Id = 1, Name = @"AA", DisplayOrder = 1 },
                        new WorkStreamModel { Id = 2, Name = @"BB", DisplayOrder = 2, IsHiddenFromCharts = true },
                    ],
                },
                DisplaySettings = scenario.DisplaySettings with { GanttChartShowHidden = true },
            };
        }

        private static CoreViewModel Load(ProjectScenarioModel plan)
        {
            CoreViewModel core = CoreViewModelFixture.Create();
            core.ProcessProjectScenario(plan, Guid.NewGuid(), @"Plan");
            core.RunCompile();
            return core;
        }
    }
}
