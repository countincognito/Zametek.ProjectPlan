using Shouldly;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Graphs.Avalonia;

namespace Zametek.View.ProjectPlan.Tests
{
    /// <summary>
    /// A load of a scenario that fails must change nothing. These tests work the real application: the failure comes from
    /// a change of the routing mode, where a load met the UI thread at the edges of a graph and threw. The application then
    /// showed the error, kept a part of a scenario, and a save wrote that part over a whole scenario.
    /// </summary>
    [Collection(ApplicationCollection.Name)]
    public class ScenarioLoadFailureTests
    {
        // The plan of the fixture.
        private const int c_Activities = 46;
        private const int c_Resources = 15;

        private readonly ApplicationFixture m_Fixture;

        public ScenarioLoadFailureTests(ApplicationFixture fixture)
        {
            m_Fixture = fixture;
        }

        private static void ShouldBeWhole(ProjectScenarioModel saved)
        {
            saved.DependentActivities.Count.ShouldBe(c_Activities);
            saved.ResourceSettings.Resources.Count.ShouldBe(c_Resources);
            saved.DisplaySettings.ArrowGraphEdgeRoutingMode.ShouldBe(EdgeRoutingMode.Spline);
        }

        // The first change fails in the reset of the scenario that is open, and the second fails after it.
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public Task Load_Given_AFailure_Then_TheApplicationGoesBackAndASaveWritesBothScenariosWhole(int failingChange) =>
            m_Fixture.RunAsync(async app =>
            {
                IManagedNodeViewModel first = app.Scenarios[0];
                IManagedNodeViewModel second = await app.DuplicateAsync(first);

                using (app.FailRoutingModeChange(failingChange))
                {
                    await app.FailToLoadAsync(second);
                }

                first.IsLoaded.ShouldBeTrue();
                second.IsLoaded.ShouldBeFalse();
                app.Core.Activities.Count.ShouldBe(c_Activities);
                app.Core.IsProjectScenarioUpdated.ShouldBeFalse(@"nothing has changed");
                await app.GraphsShouldShowAsync(GraphEdgeRoutingMode.Spline, GraphEdgeRoutingMode.SugiyamaSplines);

                ShouldBeWhole(app.SavedScenario(first));
                ShouldBeWhole(app.SavedScenario(second));

                await app.LoadAsync(second);

                app.Core.Activities.Count.ShouldBe(c_Activities);
                ShouldBeWhole(app.SavedScenario(first));
                ShouldBeWhole(app.SavedScenario(second));
            });
    }
}
