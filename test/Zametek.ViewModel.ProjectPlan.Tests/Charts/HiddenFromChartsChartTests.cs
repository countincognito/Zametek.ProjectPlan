using ScottPlot;
using ScottPlot.Plottables;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
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

        // The resources of IdlePlan, by their names.
        private const string c_Direct = @"Direct";
        private const string c_TypeNone = @"Type None";
        private const string c_Indirect = @"Indirect";
        private const string c_Long = @"Long";
        private const string c_Stream = @"Stream";

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

        [Fact]
        public void Charts_Given_TheSwitchOfAResourceAndAnotherFieldInTheEditDialog_Then_EachChartBuildsAgainWithNoCompile()
        {
            // The dialog sets the switch of resource 1 and its unit cost, an input of a compile. Auto compile is off, as in
            // each harness. Activities 1, 3 and 6 are on resource 1 and on no shown resource, and so no bar stays. Resources
            // 2 and 3 are hidden already, and so only resource 4 stays in the resource chart.
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = new();
            var dialogs = new EditDialogService(context =>
            {
                var edit = (ResourceEditViewModel)context;
                edit.IsHiddenFromCharts = true;
                edit.IsIsHiddenFromChartsActive = true;
                edit.UnitCost = 5.0;
                edit.IsUnitCostActive = true;
            });
            using ResourceSettingsManagerViewModel resources = harness.MakeResourceSettingsManager(dialogs);
            harness.Gantt.StartSubscriptions();
            harness.ResourceChart.StartSubscriptions();
            SequencerScopes.Drain(main, pool);
            BarIds(harness.Gantt.GanttChartPlotModel).ShouldBe(s_Shown, "precondition");
            Legend(harness.ResourceChart.ResourceChartPlotModel).ShouldBe([@"Resource 1", @"Resource 4"], "precondition");
            int compilations = harness.Core.CompilationOutputRevision;

            Select(resources.SetSelectedManagedResourcesCommand, resources.RawResources.Single(x => x.Id == 1));
            resources.EditManagedResourcesCommand.Execute(null);
            SequencerScopes.Drain(main, pool);

            dialogs.Errors.ShouldBeEmpty();
            harness.Core.ResourceSettings.Resources.Single(x => x.Id == 1).IsHiddenFromCharts.ShouldBeTrue();
            harness.Core.ResourceSettings.Resources.Single(x => x.Id == 1).UnitCost.ShouldBe(5.0);
            BarIds(harness.Gantt.GanttChartPlotModel).ShouldBeEmpty();
            Legend(harness.ResourceChart.ResourceChartPlotModel).ShouldBe([@"Resource 4"]);
            harness.Core.HasStaleOutputs.ShouldBeTrue("the unit cost is an input of a compile");
            harness.Core.CompilationOutputRevision.ShouldBe(compilations, "auto compile is off");
        }

        [Fact]
        public void Charts_Given_TheSwitchOfAWorkStreamAndAnotherFieldInTheEditDialog_Then_EachChartBuildsAgainWithNoCompile()
        {
            // The dialog sets the switch of work stream AA and makes it a phase, an input of a compile. Auto compile is off,
            // as in each harness. Activity 1 is in AA and BB, and BB is hidden already, and so activity 1 goes: its bar, and
            // its 2 days in the series of resource 1. In the style Block, a series has two points of 1 for each day.
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = new();
            var dialogs = new EditDialogService(context =>
            {
                var edit = (WorkStreamEditViewModel)context;
                edit.IsHiddenFromCharts = true;
                edit.IsIsHiddenFromChartsActive = true;
                edit.IsPhase = true;
                edit.IsIsPhaseActive = true;
            });
            using ResourceSettingsManagerViewModel resources = harness.MakeResourceSettingsManager(dialogs);
            using WorkStreamSettingsManagerViewModel workStreams = harness.MakeWorkStreamSettingsManager(resources, dialogs);
            harness.Core.DisplaySettingsViewModel.ResourceChartDisplayStyle = DisplayStyle.Block;
            harness.Core.DisplaySettingsViewModel.ResourceChartScheduleMode = ScheduleMode.Scheduled;
            harness.Gantt.StartSubscriptions();
            harness.ResourceChart.StartSubscriptions();
            SequencerScopes.Drain(main, pool);
            BarIds(harness.Gantt.GanttChartPlotModel).ShouldBe(s_Shown, "precondition");
            double before = DaysOf(harness.ResourceChart.ResourceChartPlotModel, @"Resource 1");
            int compilations = harness.Core.CompilationOutputRevision;

            Select(workStreams.SetSelectedManagedWorkStreamsCommand, workStreams.RawWorkStreams.Single(x => x.Id == c_AA));
            workStreams.EditManagedWorkStreamsCommand.Execute(null);
            SequencerScopes.Drain(main, pool);

            dialogs.Errors.ShouldBeEmpty();
            harness.Core.WorkStreamSettings.WorkStreams.Single(x => x.Id == c_AA).IsHiddenFromCharts.ShouldBeTrue();
            harness.Core.WorkStreamSettings.WorkStreams.Single(x => x.Id == c_AA).IsPhase.ShouldBeTrue();
            BarIds(harness.Gantt.GanttChartPlotModel).ShouldBe([3, 6]);
            (before - DaysOf(harness.ResourceChart.ResourceChartPlotModel, @"Resource 1")).ShouldBe(2.0 * 2);
            harness.Core.HasStaleOutputs.ShouldBeTrue("a phase is an input of a compile");
            harness.Core.CompilationOutputRevision.ShouldBe(compilations, "auto compile is off");
        }

        [Fact]
        public void ResourceChart_Given_ItsSubscriptionsStart_Then_ItBuildsOnce()
        {
            // The switches come in a second stream, merged with the first, and each stream gives a first value.
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = new();
            int builds = 0;
            harness.ResourceChart.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ResourceChartManagerViewModel.ResourceChartPlotModel))
                {
                    builds++;
                }
            };

            harness.ResourceChart.StartSubscriptions();
            SequencerScopes.Drain(main, pool);

            builds.ShouldBe(1);
        }

        [Theory]
        [InlineData(ScheduleMode.Combined, AllocationMode.Resource)]
        [InlineData(ScheduleMode.Combined, AllocationMode.Cost)]
        [InlineData(ScheduleMode.Combined, AllocationMode.Billing)]
        [InlineData(ScheduleMode.Combined, AllocationMode.Effort)]
        [InlineData(ScheduleMode.Combined, AllocationMode.Activity)]
        [InlineData(ScheduleMode.Scheduled, AllocationMode.Resource)]
        public void ResourceChart_Given_ADirectResourceWhoseActivitiesAreAllHidden_Then_ItHasNoSeriesAndAddsNothingToTheTotal(
            ScheduleMode scheduleMode,
            AllocationMode allocationMode)
        {
            // Activities 1 and 3 of the Direct resource are hidden. Its days 2 to 4 between them stay in its allocation, but
            // nobody can see what they are between, and the Gantt chart has no group for the resource.
            using Harness harness = new(IdlePlan(hiddenActivities: [1, 3]));
            harness.Core.DisplaySettingsViewModel.ResourceChartDisplayStyle = DisplayStyle.Block;
            harness.Core.DisplaySettingsViewModel.ResourceChartScheduleMode = scheduleMode;
            harness.Core.DisplaySettingsViewModel.ResourceChartAllocationMode = allocationMode;

            Dictionary<string, Scatter> series = harness.BuildResourceChart();

            series.Keys.ShouldNotContain(c_Direct);
            harness.BuildGantt(GroupByMode.Resource).Groups.Keys.ShouldNotContain(c_Direct);

            // The next series is the first of the stack now: its points are its own 5 days, and no day of the Direct resource.
            FirstSeriesDays(series[c_Long]).ShouldBe([1, 1, 1, 1, 1, 0, 0]);

            harness.Core.DisplaySettingsViewModel.ResourceChartShowHidden = true;
            harness.BuildResourceChart().Keys.ShouldContain(c_Direct);
        }

        [Fact]
        public void ResourceChart_Given_ADirectResourceWithOneShownActivity_Then_ItKeepsTheDaysBetweenItsActivities()
        {
            // Activity 1 (days 0 and 1) is shown, and activity 3 (days 5 and 6) is hidden. Days 2 to 4 stay, as for an
            // activity with no cost. The Direct resource is the first series of the stack.
            using Harness harness = new(IdlePlan(hiddenActivities: [3]));
            harness.Core.DisplaySettingsViewModel.ResourceChartDisplayStyle = DisplayStyle.Block;
            harness.Core.DisplaySettingsViewModel.ResourceChartScheduleMode = ScheduleMode.Scheduled;

            FirstSeriesDays(harness.BuildResourceChart()[c_Direct]).ShouldBe([1, 1, 1, 1, 1, 0, 0]);

            harness.Core.DisplaySettingsViewModel.ResourceChartShowHidden = true;
            FirstSeriesDays(harness.BuildResourceChart()[c_Direct]).ShouldBe([1, 1, 1, 1, 1, 1, 1]);
        }

        [Fact]
        public void ResourceChart_Given_AResourceOfTheTypeNoneWhoseActivitiesAreAllHidden_Then_ItHasNoSeries()
        {
            using Harness harness = new(IdlePlan(hiddenActivities: [4]));

            // Before, the series had no day, and so no height, but it kept its entry in the legend.
            harness.BuildResourceChart().Keys.ShouldNotContain(c_TypeNone);

            harness.Core.DisplaySettingsViewModel.ResourceChartShowHidden = true;
            harness.BuildResourceChart().Keys.ShouldContain(c_TypeNone);
        }

        [Theory]
        [InlineData(AllocationMode.Resource)]
        [InlineData(AllocationMode.Cost)]
        [InlineData(AllocationMode.Billing)]
        [InlineData(AllocationMode.Effort)]
        public void ResourceChart_Given_AnIndirectResourceWhoseActivitiesAreAllHidden_Then_OutsideTheModeActivityItsSeriesDoesNotChange(
            AllocationMode allocationMode)
        {
            // An Indirect resource works on each day of its range, with an activity or with none.
            using Harness harness = new(IdlePlan(hiddenActivities: [5]));
            harness.Core.DisplaySettingsViewModel.ResourceChartAllocationMode = allocationMode;

            (double X, double Y)[] hidden = [.. harness.BuildResourceChart()[c_Indirect].Data.GetScatterPoints().Select(p => (p.X, p.Y))];
            harness.Core.DisplaySettingsViewModel.ResourceChartShowHidden = true;
            (double X, double Y)[] shown = [.. harness.BuildResourceChart()[c_Indirect].Data.GetScatterPoints().Select(p => (p.X, p.Y))];

            hidden.ShouldNotBeEmpty();
            hidden.ShouldBe(shown);
        }

        [Theory]
        [InlineData(ScheduleMode.Combined)]
        [InlineData(ScheduleMode.Unscheduled)]
        public void ResourceChart_Given_AnIndirectResourceWhoseActivitiesAreAllHidden_Then_InTheModeActivityItHasNoSeries(ScheduleMode scheduleMode)
        {
            using Harness harness = new(IdlePlan(hiddenActivities: [5]));
            harness.Core.DisplaySettingsViewModel.ResourceChartScheduleMode = scheduleMode;
            harness.Core.DisplaySettingsViewModel.ResourceChartAllocationMode = AllocationMode.Activity;

            harness.BuildResourceChart().Keys.ShouldNotContain(c_Indirect);

            harness.Core.DisplaySettingsViewModel.ResourceChartShowHidden = true;
            harness.BuildResourceChart().Keys.ShouldContain(c_Indirect);
        }

        [Fact]
        public void ResourceChart_Given_AHiddenWorkStreamThatIsAllTheWorkOfAResource_Then_ItHasNoSeries()
        {
            // The Stream resource is Direct, and works only on activities 6 and 7 of the work stream, with days between them.
            using Harness harness = new(IdlePlan(hiddenActivities: [], hideWorkStream: true));

            harness.BuildResourceChart().Keys.ShouldNotContain(c_Stream);
            harness.BuildGantt(GroupByMode.Resource).Groups.Keys.ShouldNotContain(c_Stream);

            harness.Core.DisplaySettingsViewModel.ResourceChartShowHidden = true;
            harness.BuildResourceChart().Keys.ShouldContain(c_Stream);
        }

        // As the grid does, when the user selects the item.
        private static void Select(System.Windows.Input.ICommand setSelected, object item) =>
            setSelected.Execute(new Avalonia.Controls.SelectionChangedEventArgs(
                Avalonia.Controls.Primitives.SelectingItemsControl.SelectionChangedEvent,
                Array.Empty<object>(),
                new[] { item }));

        private static double DaysOf(Plot plot, string resourceName) =>
            plot.GetPlottables<Scatter>().Single(x => x.LegendText == resourceName).Data.GetScatterPoints().Sum(x => x.Y);

        // The allocation of the first series of the stack, in the style Block: two points for each day, of the same value.
        private static int[] FirstSeriesDays(Scatter series) =>
            [.. series.Data.GetScatterPoints().Where((_, i) => i % 2 == 0).Select(x => (int)x.Y)];

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

        // A plan for the series that show nothing. Each resource is a series, in this order of the stack:
        // - Direct (Direct): activity 1 (days 0 and 1) and activity 3 (days 5 and 6), and so days 2 to 4 between them.
        // - Long (None): activity 2 (days 0 to 4), which activity 3 and activity 7 follow. Never hidden.
        // - Type None (None): activity 4 (days 0 to 2).
        // - Indirect (Indirect): activity 5 (days 0 and 1).
        // - Stream (Direct): activity 6 (days 0 and 1) and activity 7 (days 5 and 6), each in the work stream WS only.
        // An activity in hiddenActivities has its own switch on.
        private static ProjectScenarioModel IdlePlan(int[] hiddenActivities, bool hideWorkStream = false)
        {
            const int workStream = 1;
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(0);

            DependentActivityModel Activity(int id, int duration, int resource, int[]? dependencies = null, bool inWorkStream = false) => new()
            {
                Dependencies = [.. dependencies ?? []],
                Activity = new ActivityModel
                {
                    Id = id,
                    DisplayOrder = id,
                    Name = $@"Activity {id}",
                    Duration = duration,
                    TargetResources = [resource],
                    TargetWorkStreams = inWorkStream ? [workStream] : [],
                    IsHiddenFromCharts = hiddenActivities.Contains(id),
                },
            };

            static ResourceModel Resource(int id, string name, InterActivityAllocationType type) => new()
            {
                Id = id,
                Name = name,
                DisplayOrder = id,
                ColorFormat = ColorHelper.Random(),
                InterActivityAllocationType = type,
            };

            return scenario with
            {
                DependentActivities =
                [
                    Activity(1, 2, 1),
                    Activity(2, 5, 2),
                    Activity(3, 2, 1, dependencies: [2]),
                    Activity(4, 3, 3),
                    Activity(5, 2, 4),
                    Activity(6, 2, 5, inWorkStream: true),
                    Activity(7, 2, 5, dependencies: [2], inWorkStream: true),
                ],
                ResourceSettings = scenario.ResourceSettings with
                {
                    Resources =
                    [
                        Resource(1, c_Direct, InterActivityAllocationType.Direct),
                        Resource(2, c_Long, InterActivityAllocationType.None),
                        Resource(3, c_TypeNone, InterActivityAllocationType.None),
                        Resource(4, c_Indirect, InterActivityAllocationType.Indirect),
                        Resource(5, c_Stream, InterActivityAllocationType.Direct),
                    ],
                },
                WorkStreamSettings = new WorkStreamSettingsModel
                {
                    WorkStreams = [new WorkStreamModel { Id = workStream, Name = @"WS", DisplayOrder = 1, IsHiddenFromCharts = hideWorkStream }],
                },
            };
        }

        // An edit dialog that the user fills and closes with OK. An error that a manager shows is kept for the test.
        private sealed class EditDialogService(Action<object> fill)
            : IDialogService
        {
            public List<string> Errors { get; } = [];

            public object Parent { set { } }

            public Task<bool> ShowContextAsync(string title, string header, string message, object context)
            {
                fill(context);
                return Task.FromResult(true);
            }

            public Task<bool> ShowContextAsync(string title, string header, string message, object context, double height, double width) =>
                ShowContextAsync(title, header, message, context);

            public Task ShowErrorAsync(string title, string header, string message)
            {
                Errors.Add(message);
                return Task.CompletedTask;
            }

            public Task ShowNotificationAsync(string title, string header, string message) =>
                throw new NotSupportedException(message);

            public Task ShowWarningAsync(string title, string header, string message) =>
                throw new NotSupportedException(message);

            public Task ShowInfoAsync(string title, string header, string message, bool showMainPageLink = false) =>
                throw new NotSupportedException(message);

            public Task ShowInfoAsync(string title, string header, string message, double height, double width, bool showMainPageLink = false) =>
                throw new NotSupportedException(message);

            public Task<bool> ShowConfirmationAsync(string title, string header, string message) =>
                throw new NotSupportedException(message);

            public Task<int> ShowChoiceAsync(string title, string header, string message, IReadOnlyList<string> choices) =>
                throw new NotSupportedException(message);

            public Task<string?> ShowOpenFileDialogAsync(string initialDirectory, IList<IFileFilter> fileFilters) =>
                throw new NotSupportedException();

            public Task<string?> ShowSaveFileDialogAsync(string initialFilename, string initialDirectory, IList<IFileFilter> fileFilters) =>
                throw new NotSupportedException();
        }

        private sealed class Harness
            : IDisposable
        {
            public Harness(bool withSwitches = true)
                : this(Plan(withSwitches))
            {
            }

            public Harness(ProjectScenarioModel plan)
            {
                Core = CoreViewModelFixture.Create();
                Core.ProcessProjectScenario(plan, Guid.NewGuid(), @"Plan");
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

            // The manager of the resources, started, as the application makes it.
            public ResourceSettingsManagerViewModel MakeResourceSettingsManager(IDialogService dialogService)
            {
                var manager = new ResourceSettingsManagerViewModel(Core, CoreViewModelFixture.CreateSettingService(), dialogService);
                manager.StartSubscriptions();
                return manager;
            }

            // The manager of the work streams, started, as the application makes it.
            public WorkStreamSettingsManagerViewModel MakeWorkStreamSettingsManager(
                ResourceSettingsManagerViewModel resourceSettingsManager,
                IDialogService dialogService)
            {
                var manager = new WorkStreamSettingsManagerViewModel(Core, resourceSettingsManager, CoreViewModelFixture.CreateSettingService(), dialogService);
                manager.StartSubscriptions();
                return manager;
            }

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
