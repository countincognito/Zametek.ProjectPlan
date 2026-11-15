using ScottPlot.Plottables;
using Shouldly;
using System;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The Gantt chart that groups by work stream puts an activity with no work stream under the default work stream. It
    /// must not write that work stream into the compiled activity: other threads read and clone the activities of the same
    /// compilation, and the plan does not have a work stream 0.
    /// </summary>
    public class GanttChartWorkStreamTests
    {
        [Fact]
        public void BuildGanttChartPlotModel_Given_AnActivityWithNoWorkStream_Then_TheCompiledActivityStaysAsItWas()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            core.RequestedProjectStart = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
            core.GraphSettings = CoreViewModelFixture.DefaultGraphSettings;
            core.DisplaySettingsViewModel.GanttChartGroupByMode = GroupByMode.WorkStream;
            core.AddManagedActivities(
            [
                new DependentActivityModel { Activity = new ActivityModel { Id = 1, DisplayOrder = 1, Duration = 10 } },
                new DependentActivityModel { Activity = new ActivityModel { Id = 2, DisplayOrder = 2, Duration = 6 } },
            ]);
            core.RunCompile();
            core.BuildResourceSeriesSet();
            using var gantt = new GanttChartManagerViewModel(
                core,
                CoreViewModelFixture.CreateSettingService(),
                CoreViewModelFixture.CreateDialogService(),
                new DateTimeCalculator(TimeProvider.System),
                new ScottPlotImageExporter(),
                CoreViewModelFixture.CreateUIDispatcher());

            gantt.BuildGanttChartPlotModel();

            // The chart has a bar for each activity, and so the build went through the grouping.
            AnnotatedBar[] bars = [.. gantt.GanttChartPlotModel.GetPlottables<BarPlot>().SelectMany(x => x.Bars).OfType<AnnotatedBar>()];
            bars.Select(x => x.ActivityId).Order().ShouldBe([1, 2]);

            foreach (IDependentActivity activity in core.GraphCompilation.DependentActivities)
            {
                activity.TargetWorkStreams.ShouldBeEmpty($@"the build of the chart changed the compiled activity {activity.Id}");
            }
        }
    }
}
