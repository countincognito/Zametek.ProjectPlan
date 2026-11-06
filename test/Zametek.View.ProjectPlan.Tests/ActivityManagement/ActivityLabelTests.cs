using Shouldly;
using System.ComponentModel;
using Xunit;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan.Tests
{
    /// <summary>
    /// The Show Connections list of the Gantt chart and the Add Activity... list of the Effort Tracking tab show an activity with the
    /// same text: its number, a dash and its name. An activity with no name shows its number in the place of the name, and so its
    /// text has the number two times. These tests work the real application on the plan of the fixture. Resource 13 has a row for
    /// activity 2 only, and so its list offers each other activity.
    /// </summary>
    [Collection(ApplicationCollection.Name)]
    public class ActivityLabelTests
    {
        private const int c_ResourceWithARow = 13;
        private const int c_ActivityWithARow = 2;
        private const int c_ActivityToRename = 3;
        private const int c_ActivityWithAName = 4;

        private readonly ApplicationFixture m_Fixture;

        public ActivityLabelTests(ApplicationFixture fixture)
        {
            m_Fixture = fixture;
        }

        // Commits a new name as the grid does.
        private static void Rename(ApplicationDriver app, int activityId, string name)
        {
            IManagedActivityViewModel activity = app.Core.RawActivities.Single(x => x.Id == activityId);
            var editable = (IEditableObject)activity;
            editable.BeginEdit();
            activity.Name = name;
            editable.EndEdit();
        }

        private static string CandidateOf(ApplicationDriver app, int activityId) =>
            app.Effort.TimesheetSections.Single(x => x.ResourceId == c_ResourceWithARow).CandidateActivities.Single(x => x.Id == activityId).DisplayName;

        [Fact]
        public Task Lists_Given_AnActivityWithNoName_Then_TheGanttChartAndTheTimesheetShowItInTheSameWay() =>
            m_Fixture.RunAsync(async app =>
            {
                string? name = app.Core.RawActivities.Single(x => x.Id == c_ActivityWithAName).Name;
                name.ShouldNotBeNullOrWhiteSpace("precondition: the plan gives the activity a name");
                string before = CandidateOf(app, c_ActivityToRename);

                Rename(app, c_ActivityToRename, string.Empty);

                await app.UntilAsync(
                    () => app.Gantt.ActivitySelector.TargetActivities.Single(x => x.Id == c_ActivityToRename).Name.Length == 0,
                    @"the new name in the selector of the Gantt chart");
                await app.UntilAsync(
                    () => CandidateOf(app, c_ActivityToRename) != before,
                    @"the new text in the list of the timesheet");

                await app.ShowTabAsync(app.Gantt);
                Dictionary<int, string> items = await app.TextsOfTheItemsAsync(app.Gantt);

                items.ShouldContainKey(c_ActivityToRename);
                items.ShouldContainKey(c_ActivityWithAName);
                items[c_ActivityToRename].ShouldBe($@"{c_ActivityToRename} - {c_ActivityToRename}", @"the item of the Gantt chart for the activity with no name");
                items[c_ActivityWithAName].ShouldBe($@"{c_ActivityWithAName} - {name}", @"the item of the Gantt chart for the activity with a name");

                foreach ((int id, string text) in items.Where(x => x.Key != c_ActivityWithARow))
                {
                    CandidateOf(app, id).ShouldBe(text, $@"activity {id} in the list of the timesheet and in the list of the Gantt chart");
                }

                await app.ClickItemsAsync(app.Gantt, c_ActivityToRename);
                app.TextsOfTheBox(app.Gantt).ShouldContain($@"{c_ActivityToRename} - {c_ActivityToRename}", @"the box of the Gantt chart for the activity with no name");
            });
    }
}
