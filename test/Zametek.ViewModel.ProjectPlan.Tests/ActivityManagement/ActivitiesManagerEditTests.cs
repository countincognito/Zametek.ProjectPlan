using ReactiveUI;
using Shouldly;
using System;
using System.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using System.Windows.Input;
using Xunit;
using Zametek.Contract.ProjectPlan;
using RxVoid = ReactiveUI.Primitives.RxVoid;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The commands of the activities grid that add an activity. Each of them is an edit of the scenario, and so each marks the
    /// scenario as updated. The mark puts an asterisk on the node of the scenario and on the title of the window, and a close
    /// asks before it drops the edit. With automatic compilation on, the compilation after the command also marks the scenario.
    /// With it off, the add command and the duplicate command did not mark it, and so a close lost the new activity.
    /// </summary>
    public class ActivitiesManagerEditTests
    {
        private const int c_ActivityCount = 3;

        // The insert, duplicate and milestone commands work on the selected activity.
        private const int c_SelectedId = 2;

        [Theory]
        [InlineData(nameof(IActivitiesManagerViewModel.AddManagedActivityCommand), false)]
        [InlineData(nameof(IActivitiesManagerViewModel.AddManagedActivityCommand), true)]
        [InlineData(nameof(IActivitiesManagerViewModel.InsertManagedActivityCommand), false)]
        [InlineData(nameof(IActivitiesManagerViewModel.InsertManagedActivityCommand), true)]
        [InlineData(nameof(IActivitiesManagerViewModel.DuplicateManagedActivityCommand), false)]
        [InlineData(nameof(IActivitiesManagerViewModel.DuplicateManagedActivityCommand), true)]
        [InlineData(nameof(IActivitiesManagerViewModel.AddMilestoneCommand), false)]
        [InlineData(nameof(IActivitiesManagerViewModel.AddMilestoneCommand), true)]
        public async Task Command_Given_ALoadedScenario_Then_TheNewActivityMarksTheScenarioAsUpdated(string commandName, bool autoCompile)
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(c_ActivityCount), Guid.NewGuid(), @"Test");
            core.AutoCompile = autoCompile;
            using var manager = new ActivitiesManagerViewModel(core, CoreViewModelFixture.CreateDialogService());
            Select(manager, core, c_SelectedId);
            core.IsProjectScenarioUpdated.ShouldBeFalse("precondition: a load is not an edit");

            await ExecuteAsync(CommandOf(manager, commandName));

            core.RawActivities.Count.ShouldBe(c_ActivityCount + 1);
            core.IsProjectScenarioUpdated.ShouldBeTrue();
        }

        private static ICommand CommandOf(ActivitiesManagerViewModel manager, string commandName) => commandName switch
        {
            nameof(IActivitiesManagerViewModel.AddManagedActivityCommand) => manager.AddManagedActivityCommand,
            nameof(IActivitiesManagerViewModel.InsertManagedActivityCommand) => manager.InsertManagedActivityCommand,
            nameof(IActivitiesManagerViewModel.DuplicateManagedActivityCommand) => manager.DuplicateManagedActivityCommand,
            nameof(IActivitiesManagerViewModel.AddMilestoneCommand) => manager.AddMilestoneCommand,
            _ => throw new ArgumentOutOfRangeException(nameof(commandName), commandName, null),
        };

        // As a click on the row of the activity in the grid does.
        private static void Select(ActivitiesManagerViewModel manager, CoreViewModel core, int activityId)
        {
            manager.SelectedActivities[activityId] = core.RawActivities.Single(x => x.Id == activityId);
            manager.HasSelectedActivities = true;
            manager.HasSelectedActivity = true;
        }

        // The command does its work on the task pool. This ends when the work has ended, and a command that does not end fails
        // the test.
        private static Task ExecuteAsync(ICommand command) =>
            ((ReactiveCommand<RxVoid, RxVoid>)command).Execute().ToTask().WaitAsync(TimeSpan.FromSeconds(30));
    }
}
