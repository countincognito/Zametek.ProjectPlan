using Shouldly;
using Xunit;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan.Tests
{
    /// <summary>
    /// A new activity is an edit of the scenario. With automatic compilation off, the add command did not mark the scenario as
    /// updated: the node of the scenario and the title of the window showed no asterisk, and a close of the window did not ask
    /// before it dropped the new activity. These tests work the real application on the plan of the fixture.
    /// </summary>
    [Collection(ApplicationCollection.Name)]
    public class AddActivityTests
    {
        private const int c_Activities = 46;

        private readonly ApplicationFixture m_Fixture;

        public AddActivityTests(ApplicationFixture fixture)
        {
            m_Fixture = fixture;
        }

        private Task RunAsync(bool autoCompile, Func<ApplicationDriver, Task> test) =>
            m_Fixture.RunAsync(async app =>
            {
                bool before = app.Main.AutoCompile;
                app.Main.AutoCompile = autoCompile;

                try
                {
                    await test(app);
                }
                finally
                {
                    // The application stays for the next test.
                    app.Main.AutoCompile = before;
                    await app.SettleAsync();
                }
            });

        private static async Task<int> AddAsync(ApplicationDriver app)
        {
            app.Activities.AddManagedActivityCommand.Execute(null);
            await app.UntilAsync(() => app.Core.RawActivities.Count == c_Activities + 1, @"the activity added");
            return app.Core.RawActivities.Max(x => x.Id);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public Task AddManagedActivityCommand_Then_TheScenarioAndTheWindowShowTheEdit(bool autoCompile) =>
            RunAsync(autoCompile, async app =>
            {
                IManagedNodeViewModel scenario = app.Scenarios[0];
                scenario.DisplayName.ShouldNotStartWith(@"*", customMessage: @"precondition: a load is not an edit");
                app.Main.ProjectHasChanges.ShouldBeFalse(@"precondition: nothing has changed since the open");

                await AddAsync(app);

                app.Core.IsProjectScenarioUpdated.ShouldBeTrue(@"a new activity is an edit");
                scenario.DisplayName.ShouldStartWith(@"*", customMessage: @"the node of the scenario shows the edit");
                app.Main.ProjectTitle.ShouldStartWith(@"*", customMessage: @"the title of the window shows the edit");
                app.Main.ProjectHasChanges.ShouldBeTrue(@"a close of the window asks before it drops the edit");
                app.Main.HasStaleOutputs.ShouldBe(!autoCompile, @"only a compilation puts the new activity in the outputs");
            });

        [Fact]
        public Task AddManagedActivityCommand_Given_NoCompilationAndThenALoad_Then_TheScenarioKeepsTheActivityAndItsMark() =>
            RunAsync(false, async app =>
            {
                IManagedNodeViewModel first = app.Scenarios[0];
                IManagedNodeViewModel second = await app.DuplicateAsync(first);
                int added = await AddAsync(app);

                await app.LoadAsync(second);

                app.SavedScenario(first).DependentActivities.ShouldContain(x => x.Activity.Id == added, @"the load kept the new activity");
                app.SavedScenario(second).DependentActivities.Count.ShouldBe(c_Activities);
                first.DisplayName.ShouldStartWith(@"*", customMessage: @"the mark stays until a save");
                second.DisplayName.ShouldNotStartWith(@"*", customMessage: @"a load is not an edit");
            });
    }
}
