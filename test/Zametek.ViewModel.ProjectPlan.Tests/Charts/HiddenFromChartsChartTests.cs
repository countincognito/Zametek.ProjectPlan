using ScottPlot;
using ScottPlot.Plottables;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The Gantt chart and the resource chart leave out the parts of the plan that are hidden from the charts, by the rule
    /// of HiddenFromCharts, and show them with the switch "Show Hidden". The plan has each case:
    /// <list type="bullet">
    /// <item>1: on resource 1, in the work streams AA and BB, with BB hidden. It stays, in the group of AA.</item>
    /// <item>2: on resource 2 only, which is hidden. It is hidden.</item>
    /// <item>3: on resources 1 and 2 together. It stays, in the group of resource 1.</item>
    /// <item>4: on resource 1, in the work stream BB only. It is hidden.</item>
    /// <item>5: on resource 1, with its own switch on, and the longest. It is hidden.</item>
    /// <item>6: on resource 1, with no work stream. It stays, in the group of no work stream.</item>
    /// <item>7: a milestone with its own switch on, and 8 a milestone that is shown.</item>
    /// </list>
    /// Resource 3 is an Indirect resource that is hidden, and resource 4 an Indirect resource that is shown.
    /// </summary>
    public class HiddenFromChartsChartTests
    {
        private const int c_AA = 1;
        private const int c_BB = 2;

        private static readonly int[] s_Shown = [1, 3, 6];

        private static readonly int[] s_Hidden = [2, 4, 5, 7];

        [Fact]
        public void Gantt_Given_NoGroups_Then_EachHiddenActivityHasNoBar()
        {
            using Harness harness = new();

            harness.BuildGantt(GroupByMode.None).ActivityIds.ShouldBe(s_Shown);
        }

        [Fact]
        public void Gantt_Given_GroupsOfResources_Then_TheGroupOfTheHiddenResourceGoesAndItsMarkIsOfTheShownActivities()
        {
            using Harness harness = new();

            (int[] activityIds, Dictionary<string, AnnotatedRectangle> groups) = harness.BuildGantt(GroupByMode.Resource);

            activityIds.ShouldBe(s_Shown);
            groups.Keys.ShouldBe([@"Resource 1"]);

            // The mark of the group goes from the first to the last activity that the chart shows, and not to activity 5.
            List<ScheduledActivityModel> shownOnResource1 = [.. harness.Core.ResourceSeriesSet.ResourceSchedules
                .Single(x => x.Resource.Id == 1).ScheduledActivities.Where(x => !s_Hidden.Contains(x.Id))];
            groups[@"Resource 1"].X1.ShouldBe(shownOnResource1.Min(x => x.StartTime));
            groups[@"Resource 1"].X2.ShouldBe(shownOnResource1.Max(x => x.FinishTime));
            harness.FinishOf(5).ShouldBeGreaterThan(shownOnResource1.Max(x => x.FinishTime), "precondition: activity 5 finishes last");
        }

        [Fact]
        public void Gantt_Given_GroupsOfWorkStreams_Then_TheGroupOfTheHiddenWorkStreamGoesAndAnActivityAlsoInAShownOneStays()
        {
            using Harness harness = new();

            (int[] activityIds, Dictionary<string, AnnotatedRectangle> groups) = harness.BuildGantt(GroupByMode.WorkStream);

            activityIds.ShouldBe(s_Shown);
            groups.Keys.Order().ShouldBe(new[] { @"AA", Resource.ProjectPlan.Labels.Label_DefaultWorkStream }.Order());
        }

        [Theory]
        [InlineData(GroupByMode.None)]
        [InlineData(GroupByMode.Resource)]
        [InlineData(GroupByMode.WorkStream)]
        public void Gantt_Given_ShowHidden_Then_EachActivityHasABar(GroupByMode groupByMode)
        {
            using Harness harness = new();
            harness.Core.DisplaySettingsViewModel.GanttChartShowHidden = true;

            harness.BuildGantt(groupByMode).ActivityIds.ShouldBe([1, 2, 3, 4, 5, 6]);
        }

        [Fact]
        public void Gantt_Given_AHiddenMilestone_Then_ItHasNoArrow()
        {
            using Harness harness = new();
            harness.Core.DisplaySettingsViewModel.GanttChartShowMilestones = true;

            harness.Gantt.BuildGanttChartPlotModel();

            harness.Gantt.GanttChartPlotModel.GetPlottables<AnnotatedArrow>().Select(x => x.Annotation)
                .ShouldBe([@"Milestone 8 (8)"]);
        }

        [Fact]
        public void Gantt_Given_TheSelectionOfAHiddenActivity_Then_NoConnectionArrow()
        {
            using Harness harness = new();
            harness.Gantt.ActivitySelector.SetTargetActivities(
                [.. Enumerable.Range(1, 8).Select(x => new TargetActivityModel { Id = x, Name = $@"Activity {x}" })],
                []);
            harness.Gantt.ActivitySelector.SetSelectedTargetActivities([5]);
            harness.Gantt.ActivitySelector.SelectedActivityIds.ShouldBe([5], "precondition: activity 5 is selected");

            harness.Gantt.BuildGanttChartPlotModel();
            harness.Gantt.GanttChartPlotModel.GetPlottables<Arrow>().ShouldBeEmpty();

            // With the hidden activities shown, the selection marks its bar.
            harness.Core.DisplaySettingsViewModel.GanttChartShowHidden = true;
            harness.Gantt.BuildGanttChartPlotModel();
            harness.Gantt.GanttChartPlotModel.GetPlottables<Arrow>().ShouldNotBeEmpty();
        }

        [Theory]
        [InlineData(ScheduleMode.Combined, new[] { @"Resource 1", @"Resource 4" })]
        [InlineData(ScheduleMode.Scheduled, new[] { @"Resource 1" })]
        [InlineData(ScheduleMode.Unscheduled, new[] { @"Resource 4" })]
        public void ResourceChart_Given_EachScheduleMode_Then_AHiddenResourceHasNoSeries(ScheduleMode scheduleMode, string[] expected)
        {
            using Harness harness = new();
            harness.Core.DisplaySettingsViewModel.ResourceChartScheduleMode = scheduleMode;

            harness.BuildResourceChart().Keys.Order().ShouldBe(expected);

            harness.Core.DisplaySettingsViewModel.ResourceChartShowHidden = true;
            harness.BuildResourceChart().Keys.Count.ShouldBeGreaterThan(expected.Length);
        }

        [Fact]
        public void ResourceChart_Given_TheHiddenActivitiesOfResource1_Then_ItsSeriesHasNoneOfTheirDays()
        {
            // In the style Block, the first series has two points of 1 for each day of its allocation. Activities 4 (2 days)
            // and 5 (10 days) are hidden, and resource 1 has the type None: it has no other day between its activities.
            using Harness harness = new();
            harness.Core.DisplaySettingsViewModel.ResourceChartDisplayStyle = DisplayStyle.Block;
            harness.Core.DisplaySettingsViewModel.ResourceChartScheduleMode = ScheduleMode.Scheduled;

            double hidden = harness.BuildResourceChart()[@"Resource 1"].Data.GetScatterPoints().Sum(x => x.Y);
            harness.Core.DisplaySettingsViewModel.ResourceChartShowHidden = true;
            double shown = harness.BuildResourceChart()[@"Resource 1"].Data.GetScatterPoints().Sum(x => x.Y);

            (shown - hidden).ShouldBe(2.0 * (2 + 10));
        }

        [Fact]
        public void Charts_Given_HiddenParts_Then_TheScheduleTheMetricsAndTheSeriesDoNotChange()
        {
            using Harness hidden = new();
            using Harness plain = new(withSwitches: false);
            List<List<bool>> before = Allocations(hidden.Core.ResourceSeriesSet);

            hidden.BuildGantt(GroupByMode.Resource);
            hidden.BuildResourceChart();

            Allocations(hidden.Core.ResourceSeriesSet).ShouldBe(before, "the charts filter a copy");
            Allocations(hidden.Core.ResourceSeriesSet).ShouldBe(Allocations(plain.Core.ResourceSeriesSet));
            hidden.Core.GraphCompilation.DependentActivities.OrderBy(x => x.Id).Select(x => (x.Id, x.EarliestStartTime, x.EarliestFinishTime))
                .ShouldBe(plain.Core.GraphCompilation.DependentActivities.OrderBy(x => x.Id).Select(x => (x.Id, x.EarliestStartTime, x.EarliestFinishTime)));
            JsonSerializer.Serialize(hidden.Core.BuildProjectScenario().Metrics)
                .ShouldBe(JsonSerializer.Serialize(plain.Core.BuildProjectScenario().Metrics));
        }

        [Fact]
        public void Charts_Given_ASwitchThatChangesWithNoCompile_Then_EachChartBuildsAgain()
        {
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = new();
            harness.Gantt.StartSubscriptions();
            harness.ResourceChart.StartSubscriptions();
            SequencerScopes.Drain(main, pool);
            int compilations = harness.Core.CompilationOutputRevision;

            // An activity, in the grid.
            harness.Core.RawActivities.Single(x => x.Id == 1).IsHiddenFromCharts = true;
            SequencerScopes.Drain(main, pool);
            BarIds(harness.Gantt.GanttChartPlotModel).ShouldBe([3, 6]);

            // A resource, as its manager gives it to the core.
            harness.Core.SetResourceChartVisibility(new Dictionary<int, bool> { [1] = true });
            SequencerScopes.Drain(main, pool);
            Legend(harness.ResourceChart.ResourceChartPlotModel).ShouldBe([@"Resource 4"]);

            // The switch that shows the hidden parts.
            harness.Core.DisplaySettingsViewModel.ResourceChartShowHidden = true;
            SequencerScopes.Drain(main, pool);
            Legend(harness.ResourceChart.ResourceChartPlotModel).ShouldContain(@"Resource 1");

            harness.Core.CompilationOutputRevision.ShouldBe(compilations, "no switch compiles");
        }

        private static int[] BarIds(Plot plot) =>
            [.. plot.GetPlottables<BarPlot>().SelectMany(x => x.Bars).OfType<AnnotatedBar>().Select(x => x.ActivityId).Distinct().Order()];

        private static string[] Legend(Plot plot) =>
            [.. plot.GetPlottables<Scatter>().Select(x => x.LegendText).Order()];

        private static List<List<bool>> Allocations(ResourceSeriesSetModel set) =>
            [.. set.Combined.Concat(set.Scheduled).Concat(set.Unscheduled).SelectMany(x => new[]
            {
                x.ResourceSchedule.ResourceAllocation,
                x.ResourceSchedule.CostAllocation,
                x.ResourceSchedule.BillingAllocation,
                x.ResourceSchedule.EffortAllocation,
                x.ResourceSchedule.ActivityAllocation,
            }).Select(x => x.ToList())];

        private sealed class Harness
            : IDisposable
        {
            public Harness(bool withSwitches = true)
            {
                Core = CoreViewModelFixture.Create();
                Core.ProcessProjectScenario(Plan(withSwitches), Guid.NewGuid(), @"Plan");
                Core.BuildResourceSeriesSet();
                Core.DisplaySettingsViewModel.GanttChartAnnotationStyle = AnnotationStyle.Plain;

                var calculator = new DateTimeCalculator(TimeProvider.System);
                Gantt = new GanttChartManagerViewModel(
                    Core,
                    CoreViewModelFixture.CreateSettingService(),
                    CoreViewModelFixture.CreateDialogService(),
                    calculator,
                    new ScottPlotImageExporter(),
                    CoreViewModelFixture.CreateUIDispatcher());
                ResourceChart = new ResourceChartManagerViewModel(
                    Core,
                    CoreViewModelFixture.CreateSettingService(),
                    CoreViewModelFixture.CreateDialogService(),
                    calculator,
                    new ScottPlotImageExporter(),
                    CoreViewModelFixture.CreateUIDispatcher());
            }

            public CoreViewModel Core { get; }

            public GanttChartManagerViewModel Gantt { get; }

            public ResourceChartManagerViewModel ResourceChart { get; }

            public int FinishOf(int activityId) =>
                Core.GraphCompilation.DependentActivities.Single(x => x.Id == activityId).EarliestFinishTime.GetValueOrDefault();

            // The activities with a bar, and the mark of each group by its name.
            public (int[] ActivityIds, Dictionary<string, AnnotatedRectangle> Groups) BuildGantt(GroupByMode groupByMode)
            {
                Core.DisplaySettingsViewModel.GanttChartGroupByMode = groupByMode;
                Gantt.BuildGanttChartPlotModel();
                Plot plot = Gantt.GanttChartPlotModel;
                return (
                    [.. plot.GetPlottables<BarPlot>().SelectMany(x => x.Bars).OfType<AnnotatedBar>().Select(x => x.ActivityId).Distinct().Order()],
                    plot.GetPlottables<AnnotatedRectangle>().ToDictionary(x => x.Annotation));
            }

            // Each series by its name.
            public Dictionary<string, Scatter> BuildResourceChart()
            {
                ResourceChart.BuildResourceChartPlotModel();
                return ResourceChart.ResourceChartPlotModel.GetPlottables<Scatter>().ToDictionary(x => x.LegendText);
            }

            public void Dispose()
            {
                ResourceChart.Dispose();
                Gantt.Dispose();
                Core.Dispose();
            }

            private static ProjectScenarioModel Plan(bool withSwitches)
            {
                ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(0);

                static DependentActivityModel Activity(int id, int duration, int[] targetResources, int[] targetWorkStreams, bool isHidden = false, bool both = false, int[]? dependencies = null) => new()
                {
                    Dependencies = [.. dependencies ?? []],
                    Activity = new ActivityModel
                    {
                        Id = id,
                        DisplayOrder = id,
                        Name = duration == 0 ? $@"Milestone {id}" : $@"Activity {id}",
                        Duration = duration,
                        TargetResources = [.. targetResources],
                        TargetResourceOperator = both ? LogicalOperator.AND : LogicalOperator.OR,
                        TargetWorkStreams = [.. targetWorkStreams],
                        IsHiddenFromCharts = isHidden,
                    },
                };

                return scenario with
                {
                    DependentActivities =
                    [
                        Activity(1, 2, [1], [c_AA, c_BB]),
                        Activity(2, 3, [2], []),
                        Activity(3, 2, [1, 2], [], both: true),
                        Activity(4, 2, [1], [c_BB]),
                        Activity(5, 10, [1], [], isHidden: withSwitches, dependencies: [1, 3, 4, 6]),
                        Activity(6, 1, [1], []),
                        Activity(7, 0, [], [], isHidden: withSwitches),
                        Activity(8, 0, [], []),
                    ],
                    ResourceSettings = scenario.ResourceSettings with
                    {
                        Resources =
                        [
                            new ResourceModel { Id = 1, Name = @"Resource 1", DisplayOrder = 1, ColorFormat = ColorHelper.Random() },
                            new ResourceModel { Id = 2, Name = @"Resource 2", DisplayOrder = 2, ColorFormat = ColorHelper.Random(), IsHiddenFromCharts = withSwitches },
                            new ResourceModel { Id = 3, Name = @"Resource 3", DisplayOrder = 3, ColorFormat = ColorHelper.Random(), InterActivityAllocationType = InterActivityAllocationType.Indirect, IsHiddenFromCharts = withSwitches },
                            new ResourceModel { Id = 4, Name = @"Resource 4", DisplayOrder = 4, ColorFormat = ColorHelper.Random(), InterActivityAllocationType = InterActivityAllocationType.Indirect },
                        ],
                    },
                    WorkStreamSettings = new WorkStreamSettingsModel
                    {
                        WorkStreams =
                        [
                            new WorkStreamModel { Id = c_AA, Name = @"AA", DisplayOrder = 1 },
                            new WorkStreamModel { Id = c_BB, Name = @"BB", DisplayOrder = 2, IsHiddenFromCharts = withSwitches },
                        ],
                    },
                };
            }
        }
    }
}
