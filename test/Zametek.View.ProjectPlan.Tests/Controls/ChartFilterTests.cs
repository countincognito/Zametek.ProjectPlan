using Shouldly;
using Xunit;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan.Tests
{
    /// <summary>
    /// Each scenario has its own filters on the Gantt and earned-value charts. These tests work the real application: the main
    /// view, Dock, the views of the charts and the control of the filter. A dropdown must show what the scenario has, and opening
    /// it must change nothing. An item of the dropdown must not bring back a filter from the scenario before.
    /// </summary>
    [Collection(ApplicationCollection.Name)]
    public class ChartFilterTests
    {
        private static readonly int[] s_Connections = [1, 2, 3];
        private static readonly int[] s_Resources = [1, 2];

        private readonly ApplicationFixture m_Fixture;

        public ChartFilterTests(ApplicationFixture fixture)
        {
            m_Fixture = fixture;
        }

        private static async Task SetFiltersAsync(ApplicationDriver app)
        {
            await app.ShowTabAsync(app.Gantt);
            await app.ClickItemsAsync(app.Gantt, s_Connections);
            await app.ShowTabAsync(app.EarnedValue);
            await app.ClickItemsAsync(app.EarnedValue, s_Resources);
        }

        [Fact]
        public Task Load_Given_FiltersOnTheOtherScenario_Then_TheDropdownsDoNotBringThemBack() =>
            m_Fixture.RunAsync(async app =>
            {
                IManagedNodeViewModel original = app.Scenarios[0];
                IManagedNodeViewModel duplicate = await app.DuplicateAsync(original);
                await app.LoadAsync(duplicate);
                await SetFiltersAsync(app);

                await app.LoadAsync(original);

                // The tab of this chart stayed on show for the load, and the other tab was hidden.
                await app.FilterShouldBeAsync(app.EarnedValue, isUpdated: false);
                await app.ShowTabAsync(app.Gantt);
                await app.FilterShouldBeAsync(app.Gantt, isUpdated: false);

                app.SavedDisplaySettings(original).GanttChartShowConnections.ShouldBeEmpty();
                app.SavedDisplaySettings(original).EarnedValueShowResources.ShouldBeEmpty();
                app.SavedDisplaySettings(duplicate).GanttChartShowConnections.ShouldBe(s_Connections);
                app.SavedDisplaySettings(duplicate).EarnedValueShowResources.ShouldBe(s_Resources);
            });

        [Fact]
        public Task Load_Given_FiltersOnTheScenarioThatOpened_Then_TheDropdownsDoNotBringThemBack() =>
            m_Fixture.RunAsync(async app =>
            {
                // No load between the duplicate and the filters.
                IManagedNodeViewModel original = app.Scenarios[0];
                IManagedNodeViewModel duplicate = await app.DuplicateAsync(original);
                await SetFiltersAsync(app);

                await app.LoadAsync(duplicate);

                await app.FilterShouldBeAsync(app.EarnedValue, isUpdated: false);
                await app.ShowTabAsync(app.Gantt);
                await app.FilterShouldBeAsync(app.Gantt, isUpdated: false);
            });

        [Fact]
        public Task Load_Given_TheFiltersSetInTheOtherOrder_Then_TheDropdownsDoNotBringThemBack() =>
            m_Fixture.RunAsync(async app =>
            {
                IManagedNodeViewModel original = app.Scenarios[0];
                IManagedNodeViewModel duplicate = await app.DuplicateAsync(original);
                await app.ShowTabAsync(app.EarnedValue);
                await app.ClickItemsAsync(app.EarnedValue, s_Resources);
                await app.ShowTabAsync(app.Gantt);
                await app.ClickItemsAsync(app.Gantt, s_Connections);

                await app.LoadAsync(duplicate);

                await app.FilterShouldBeAsync(app.Gantt, isUpdated: false);
                await app.ShowTabAsync(app.EarnedValue);
                await app.FilterShouldBeAsync(app.EarnedValue, isUpdated: false);
            });

        [Fact]
        public Task Load_Given_AnEmptyScenarioBetween_Then_TheDropdownsDoNotBringTheFiltersBack() =>
            m_Fixture.RunAsync(async app =>
            {
                IManagedNodeViewModel original = app.Scenarios[0];
                IManagedNodeViewModel duplicate = await app.DuplicateAsync(original);
                await app.LoadAsync(duplicate);
                IManagedNodeViewModel empty = await app.CreateEmptyAsync();
                await SetFiltersAsync(app);

                await app.LoadAsync(empty);
                await app.LoadAsync(original);

                await app.FilterShouldBeAsync(app.EarnedValue, isUpdated: false);
                await app.ShowTabAsync(app.Gantt);
                await app.FilterShouldBeAsync(app.Gantt, isUpdated: false);
            });

        [Fact]
        public Task Load_Given_NoChartOnShow_Then_TheDropdownsDoNotBringTheFiltersBack() =>
            m_Fixture.RunAsync(async app =>
            {
                IManagedNodeViewModel original = app.Scenarios[0];
                IManagedNodeViewModel duplicate = await app.DuplicateAsync(original);
                await app.LoadAsync(duplicate);
                await SetFiltersAsync(app);
                await app.ShowTabAsync(app.Activities);

                await app.LoadAsync(original);

                await app.ShowTabAsync(app.EarnedValue);
                await app.FilterShouldBeAsync(app.EarnedValue, isUpdated: false);
                await app.ShowTabAsync(app.Gantt);
                await app.FilterShouldBeAsync(app.Gantt, isUpdated: false);
            });

        [Fact]
        public Task Load_Given_ADifferentFilterOnEachScenario_Then_EachScenarioShowsItsOwn() =>
            m_Fixture.RunAsync(async app =>
            {
                IManagedNodeViewModel original = app.Scenarios[0];
                IManagedNodeViewModel duplicate = await app.DuplicateAsync(original);
                await app.LoadAsync(duplicate);
                await SetFiltersAsync(app);
                await app.LoadAsync(original);
                await app.ClickItemsAsync(app.EarnedValue, 3);
                await app.ShowTabAsync(app.Gantt);
                await app.ClickItemsAsync(app.Gantt, 4, 5);

                for (int i = 0; i < 2; i++)
                {
                    await app.LoadAsync(duplicate);
                    await app.FilterShouldBeAsync(app.Gantt, isUpdated: false, s_Connections);
                    await app.ShowTabAsync(app.EarnedValue);
                    await app.FilterShouldBeAsync(app.EarnedValue, isUpdated: false, s_Resources);

                    await app.LoadAsync(original);
                    await app.FilterShouldBeAsync(app.EarnedValue, isUpdated: false, 3);
                    await app.ShowTabAsync(app.Gantt);
                    await app.FilterShouldBeAsync(app.Gantt, isUpdated: false, 4, 5);
                }

                app.SavedDisplaySettings(duplicate).GanttChartShowConnections.ShouldBe(s_Connections);
                app.SavedDisplaySettings(duplicate).EarnedValueShowResources.ShouldBe(s_Resources);
                app.SavedDisplaySettings(original).GanttChartShowConnections.ShouldBe(new[] { 4, 5 });
                app.SavedDisplaySettings(original).EarnedValueShowResources.ShouldBe(new[] { 3 });
            });

        [Fact]
        public Task Load_Given_ThereAndBackWithNoDropdownBetween_Then_TheFiltersAreStillThere() =>
            m_Fixture.RunAsync(async app =>
            {
                // An item of the dropdown keeps its tick through both loads, and it agrees with the filter at the end.
                IManagedNodeViewModel original = app.Scenarios[0];
                IManagedNodeViewModel duplicate = await app.DuplicateAsync(original);
                await app.LoadAsync(duplicate);
                await SetFiltersAsync(app);

                await app.LoadAsync(original);
                await app.LoadAsync(duplicate);

                await app.FilterShouldBeAsync(app.EarnedValue, isUpdated: false, s_Resources);
                await app.ShowTabAsync(app.Gantt);
                await app.FilterShouldBeAsync(app.Gantt, isUpdated: false, s_Connections);
            });

        [Fact]
        public Task Dropdown_Given_TheFilterOfAChart_Then_ItsBoxAndItsItemsShowWithTheirTemplates() =>
            m_Fixture.RunAsync(async app =>
            {
                await app.ShowTabAsync(app.Gantt);
                await app.ItemsShouldShowTheirTextsAndTicksAsync(app.Gantt, s_Connections);
                await app.ShowTabAsync(app.EarnedValue);
                await app.ItemsShouldShowTheirTextsAndTicksAsync(app.EarnedValue, s_Resources);
            });

        [Fact]
        public Task Dropdown_Given_ClicksAfterALoad_Then_TheyCountAsEditsOfThatScenario() =>
            m_Fixture.RunAsync(async app =>
            {
                IManagedNodeViewModel original = app.Scenarios[0];
                IManagedNodeViewModel duplicate = await app.DuplicateAsync(original);
                await app.LoadAsync(duplicate);
                await SetFiltersAsync(app);
                await app.LoadAsync(original);

                // An item that the other scenario has selected, on, and then off again.
                await app.ClickItemsAsync(app.EarnedValue, 1);
                await app.FilterShouldBeAsync(app.EarnedValue, isUpdated: true, 1);
                await app.ClickItemsAsync(app.EarnedValue, 1);
                await app.FilterShouldBeAsync(app.EarnedValue, isUpdated: true);

                await app.ClickItemsAsync(app.EarnedValue, 2);
                await app.FilterShouldBeAsync(app.EarnedValue, isUpdated: true, 2);
                app.SavedDisplaySettings(duplicate).EarnedValueShowResources.ShouldBe(s_Resources);
            });
    }
}
