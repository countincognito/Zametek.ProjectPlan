using ScottPlot.Plottables;
using Shouldly;
using System;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// What the bars of the Gantt chart tell the drag of the end of a bar: the shortest duration that a drag can give, and
    /// where the bar ends then. A drag moves the optimistic, the most likely and the pessimistic duration of the activity by
    /// the same number of days, and it stops where the optimistic duration reaches 0, or the most likely reaches 1.
    /// </summary>
    public class GanttChartDragTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void BuildGanttChartPlotModel_Given_AnActivityWithEstimates_Then_ItsBarSaysWhereTheShortestDragEnds(bool showDates)
        {
            // Activity 1 has O = 4, ML = 10 and P = 16, and so it takes 10 days. A drag can make it 6 days at the shortest,
            // where O is 0. Activity 2 takes 6 days from the same start, and so its bar ends where the shortest bar of activity
            // 1 ends, in days or in dates. Activity 3 has no estimates of its own, and a drag can make it 1 day, as before.
            using CoreViewModel core = CoreViewModelFixture.Create();
            core.ProjectStart = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
            core.GraphSettings = CoreViewModelFixture.DefaultGraphSettings;
            core.DisplaySettingsViewModel.ShowDates = showDates;
            core.AddManagedActivities(
            [
                new DependentActivityModel
                {
                    Activity = new ActivityModel { Id = 1, DisplayOrder = 1, Duration = 10, MostLikelyDuration = 10, OptimisticDuration = 4, PessimisticDuration = 16 },
                },
                new DependentActivityModel { Activity = new ActivityModel { Id = 2, DisplayOrder = 2, Duration = 6 } },
                new DependentActivityModel { Activity = new ActivityModel { Id = 3, DisplayOrder = 3, Duration = 5 } },
            ]);
            core.RunCompile();
            using var gantt = new GanttChartManagerViewModel(
                core,
                CoreViewModelFixture.CreateSettingService(),
                CoreViewModelFixture.CreateDialogService(),
                new DateTimeCalculator(TimeProvider.System),
                new ScottPlotImageExporter(),
                CoreViewModelFixture.CreateUIDispatcher());

            gantt.BuildGanttChartPlotModel();

            AnnotatedBar[] bars = [.. gantt.GanttChartPlotModel.GetPlottables<BarPlot>().First().Bars.OfType<AnnotatedBar>()];
            AnnotatedBar withEstimates = bars.Single(x => x.ActivityId == 1);
            AnnotatedBar sixDays = bars.Single(x => x.ActivityId == 2);
            AnnotatedBar withNone = bars.Single(x => x.ActivityId == 3);

            withEstimates.Duration.ShouldBe(10);
            withEstimates.MinimumDuration.ShouldBe(6);
            withEstimates.MinimumValue.ShouldBe(sixDays.Value);
            withNone.MinimumDuration.ShouldBe(1);
        }
    }
}
