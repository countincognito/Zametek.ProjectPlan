using Shouldly;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The two chart filters are replaced as a whole, and they never change in place. A handler reads a filter on the UI thread
    /// while a load replaces it on another thread (ARCHITECTURE section 7 rule 9).
    /// </summary>
    public class ProjectScenarioDisplaySettingsViewModelTests
    {
        [Fact]
        public void GanttChartShowConnections_Given_ANewFilterDuringARead_Then_TheReadKeepsWhatItHad()
        {
            // Here the new filter arrives on one thread, in the middle of the read, so that it lands in that window each time.
            using CoreViewModel core = CoreViewModelFixture.Create();
            IProjectScenarioDisplaySettingsViewModel display = core.DisplaySettingsViewModel;
            display.SetValues(new ProjectScenarioDisplaySettingsModel { GanttChartShowConnections = [1, 2, 3] });

            var read = new List<int>();

            foreach (int connection in display.GanttChartShowConnections)
            {
                read.Add(connection);
                display.SetValues(new ProjectScenarioDisplaySettingsModel { GanttChartShowConnections = [4, 5, 6] });
            }

            read.ToArray().ShouldBe(new[] { 1, 2, 3 });
            display.GanttChartShowConnections.ToArray().ShouldBe(new[] { 4, 5, 6 });
        }

        [Fact]
        public void EarnedValueShowResources_Given_ANewFilterDuringARead_Then_TheReadKeepsWhatItHad()
        {
            // Here the new filter arrives on one thread, in the middle of the read, so that it lands in that window each time.
            using CoreViewModel core = CoreViewModelFixture.Create();
            IProjectScenarioDisplaySettingsViewModel display = core.DisplaySettingsViewModel;
            display.SetValues(new ProjectScenarioDisplaySettingsModel { EarnedValueShowResources = [1, 2, 3] });

            var read = new List<int>();

            foreach (int resource in display.EarnedValueShowResources)
            {
                read.Add(resource);
                display.SetValues(new ProjectScenarioDisplaySettingsModel { EarnedValueShowResources = [4, 5, 6] });
            }

            read.ToArray().ShouldBe(new[] { 1, 2, 3 });
            display.EarnedValueShowResources.ToArray().ShouldBe(new[] { 4, 5, 6 });
        }

        [Fact]
        public void SetGanttChartShowConnections_Given_AChangeOfTheArrayAfterIt_Then_TheFilterStaysAsItWas()
        {
            // The selector passes its own snapshot of the ids.
            using CoreViewModel core = CoreViewModelFixture.Create();
            IProjectScenarioDisplaySettingsViewModel display = core.DisplaySettingsViewModel;
            int[] connections = [1, 2];

            display.SetGanttChartShowConnections(connections);
            connections[0] = 3;

            display.GanttChartShowConnections.ToArray().ShouldBe(new[] { 1, 2 });
        }

        [Fact]
        public void SetEarnedValueShowResources_Given_AChangeOfTheArrayAfterIt_Then_TheFilterStaysAsItWas()
        {
            // The selector passes its own snapshot of the ids.
            using CoreViewModel core = CoreViewModelFixture.Create();
            IProjectScenarioDisplaySettingsViewModel display = core.DisplaySettingsViewModel;
            int[] resources = [1, 2];

            display.SetEarnedValueShowResources(resources);
            resources[0] = 3;

            display.EarnedValueShowResources.ToArray().ShouldBe(new[] { 1, 2 });
        }

        [Fact]
        public void GetValues_Given_AChangeOfWhatItReturned_Then_TheFiltersStayAsTheyWere()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            IProjectScenarioDisplaySettingsViewModel display = core.DisplaySettingsViewModel;
            display.SetValues(new ProjectScenarioDisplaySettingsModel { GanttChartShowConnections = [1], EarnedValueShowResources = [2] });

            ProjectScenarioDisplaySettingsModel values = display.GetValues();
            values.GanttChartShowConnections.ToArray().ShouldBe(new[] { 1 });
            values.EarnedValueShowResources.ToArray().ShouldBe(new[] { 2 });

            values.GanttChartShowConnections.Add(3);
            values.EarnedValueShowResources.Add(4);

            display.GanttChartShowConnections.ToArray().ShouldBe(new[] { 1 });
            display.EarnedValueShowResources.ToArray().ShouldBe(new[] { 2 });
        }

        [Fact]
        public void SetValues_Given_AChangeOfTheModelAfterIt_Then_TheFiltersStayAsTheyWere()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            IProjectScenarioDisplaySettingsViewModel display = core.DisplaySettingsViewModel;
            var model = new ProjectScenarioDisplaySettingsModel { GanttChartShowConnections = [1], EarnedValueShowResources = [2] };

            display.SetValues(model);
            model.GanttChartShowConnections.Add(3);
            model.EarnedValueShowResources.Add(4);

            display.GanttChartShowConnections.ToArray().ShouldBe(new[] { 1 });
            display.EarnedValueShowResources.ToArray().ShouldBe(new[] { 2 });
        }
    }
}
