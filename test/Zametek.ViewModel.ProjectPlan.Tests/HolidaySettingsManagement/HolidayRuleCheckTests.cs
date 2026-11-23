using Shouldly;
using System;
using System.Globalization;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    // The calendar reads each occurrence of each holiday rule in its range of days. A rule that repeats more often than each
    // day held the load for minutes (from the review of src, test and pkg; measured on 2026-11-23). The check allows a rule
    // that repeats each day or less often, with no part for hours, minutes or seconds.
    public class HolidayRuleCheckTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData(@"")]
        [InlineData(@"FREQ=DAILY;COUNT=1")]
        [InlineData(@"FREQ=WEEKLY;BYDAY=SA,SU")]
        [InlineData(@"FREQ=MONTHLY;BYDAY=MO;BYSETPOS=1")]
        [InlineData(@"FREQ=YEARLY;BYMONTH=12;BYMONTHDAY=25")]
        [InlineData(@"freq=yearly;bymonth=1;bymonthday=1")]
        [InlineData(@"RRULE:FREQ=DAILY;UNTIL=20271231T000000Z")]
        [InlineData(@" FREQ = WEEKLY ; INTERVAL = 2 ")]
        public void IsAllowed_Given_ARuleOfDays_Then_True(string? rule)
        {
            HolidayRuleCheck.IsAllowed(rule).ShouldBeTrue();
        }

        [Theory]
        [InlineData(@"FREQ=SECONDLY")]
        [InlineData(@"FREQ=MINUTELY;COUNT=10")]
        [InlineData(@"FREQ=HOURLY")]
        [InlineData(@"freq=hourly")]
        [InlineData(@"RRULE:FREQ=SECONDLY")]
        [InlineData(@"FREQ=DAILY;BYHOUR=10")]
        [InlineData(@"FREQ=DAILY;BYMINUTE=0,30")]
        [InlineData(@"FREQ=WEEKLY;BYSECOND=1")]
        [InlineData(@"FREQ=FORTNIGHTLY")]
        public void IsAllowed_Given_ARuleThatRepeatsMoreOftenThanEachDay_Then_False(string rule)
        {
            HolidayRuleCheck.IsAllowed(rule).ShouldBeFalse();
        }

        // The desktop opens a project file with no check before the load. It refuses a file with such a rule, or with more
        // holidays than the calendar takes, with the message of FindProblem.
        [Fact]
        public void FindProblem_Given_ARuleThatIsNotAllowed_Then_TheHolidayTheScenarioAndTheRule()
        {
            ProjectModel project = Project(
                new HolidayModel { Id = 1, Name = @"Christmas Day", RecurrencePattern = @"FREQ=YEARLY;BYMONTH=12;BYMONTHDAY=25" },
                new HolidayModel { Id = 2, Name = @"Each minute", RecurrencePattern = @"FREQ=MINUTELY" });

            HolidayRuleCheck.FindProblem(project).ShouldBe(string.Format(
                CultureInfo.CurrentCulture,
                Resource.ProjectPlan.Messages.Message_ProjectHolidayRuleNotAllowed,
                @"Each minute",
                @"Base",
                @"FREQ=MINUTELY"));
        }

        // The dock of the holidays reads each rule with RecurrencePatternHelper, on the main thread with no catch. A rule that it
        // cannot read ended the application (from the review of src, test and pkg). Each rule here is allowed (IsAllowed).
        [Theory]
        [InlineData(@"FREQ=YEARLY;UNTIL=2027-12-31")]
        [InlineData(@"FREQ=YEARLY;COUNT=2;UNTIL=20271231")]
        [InlineData(@"FREQ=YEARLY;INTERVAL=0")]
        [InlineData(@"FREQ=YEARLY;BYMONTHDAY=last")]
        [InlineData(@"RRULE:FREQ=YEARLY;BYMONTH=12;BYMONTHDAY=25")]
        public void FindReadProblem_Given_ARuleThatTheDesktopCannotRead_Then_TheCauseFromTheReader(string rule)
        {
            HolidayRuleCheck.IsAllowed(rule).ShouldBeTrue();
            Exception? thrown = Record.Exception(() => RecurrencePatternHelper.ToRule(rule));

            thrown.ShouldNotBeNull();
            HolidayRuleCheck.FindReadProblem(rule).ShouldBe(thrown.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(@"")]
        [InlineData(@"FREQ=WEEKLY;BYDAY=SA,SU")]
        [InlineData(@"FREQ=YEARLY;BYMONTH=12;BYMONTHDAY=25")]
        [InlineData(@"FREQ=DAILY;UNTIL=20271231T000000Z")]
        public void FindReadProblem_Given_ARuleThatTheDesktopCanRead_Then_Null(string? rule)
        {
            HolidayRuleCheck.FindReadProblem(rule).ShouldBeNull();
        }

        [Fact]
        public void FindProblem_Given_ARuleThatTheDesktopCannotRead_Then_TheHolidayTheScenarioTheRuleAndTheCause()
        {
            ProjectModel project = Project(
                new HolidayModel { Id = 1, Name = @"Christmas Day", RecurrencePattern = @"FREQ=YEARLY;BYMONTH=12;BYMONTHDAY=25" },
                new HolidayModel { Id = 2, Name = @"Last year", RecurrencePattern = @"FREQ=YEARLY;UNTIL=2027-12-31" });

            HolidayRuleCheck.FindProblem(project).ShouldBe(string.Format(
                CultureInfo.CurrentCulture,
                Resource.ProjectPlan.Messages.Message_ProjectHolidayRuleNotReadable,
                @"Last year",
                @"Base",
                @"FREQ=YEARLY;UNTIL=2027-12-31",
                HolidayRuleCheck.FindReadProblem(@"FREQ=YEARLY;UNTIL=2027-12-31")));
        }

        [Fact]
        public void FindImportProblem_Given_ARuleThatTheDesktopCannotRead_Then_TheHolidayTheRuleAndTheCause()
        {
            HolidayModel[] holidays =
            [
                new HolidayModel { Id = 1, Name = @"Christmas Day", RecurrencePattern = @"FREQ=YEARLY;BYMONTH=12;BYMONTHDAY=25" },
                new HolidayModel { Id = 2, Name = @"Last year", RecurrencePattern = @"FREQ=YEARLY;UNTIL=2027-12-31" },
            ];

            HolidayRuleCheck.FindImportProblem(holidays).ShouldBe(string.Format(
                CultureInfo.CurrentCulture,
                Resource.ProjectPlan.Messages.Message_ImportHolidayRuleNotReadable,
                @"Last year",
                @"FREQ=YEARLY;UNTIL=2027-12-31",
                HolidayRuleCheck.FindReadProblem(@"FREQ=YEARLY;UNTIL=2027-12-31")));
            HolidayRuleCheck.FindImportProblem(holidays[..1]).ShouldBeNull();
            HolidayRuleCheck.FindImportProblem([]).ShouldBeNull();
        }

        [Fact]
        public void FindProblem_Given_MoreHolidaysThanTheCalendarTakes_Then_TheScenarioAndTheCount()
        {
            ProjectModel project = Project([.. Enumerable.Range(1, HolidayRuleCheck.MaximumHolidayCount + 1).Select(x => new HolidayModel { Id = x, RecurrencePattern = @"FREQ=YEARLY" })]);

            HolidayRuleCheck.FindProblem(project).ShouldBe(string.Format(
                CultureInfo.CurrentCulture,
                Resource.ProjectPlan.Messages.Message_ProjectTooManyHolidays,
                @"Base",
                HolidayRuleCheck.MaximumHolidayCount + 1,
                HolidayRuleCheck.MaximumHolidayCount));
        }

        [Fact]
        public void FindProblem_Given_HolidaysThatTheCalendarTakes_Then_Null()
        {
            HolidayRuleCheck.FindProblem(Project([.. Enumerable.Range(1, HolidayRuleCheck.MaximumHolidayCount).Select(x => new HolidayModel { Id = x, RecurrencePattern = @"FREQ=WEEKLY;BYDAY=SA,SU" })]))
                .ShouldBeNull();
            HolidayRuleCheck.FindProblem(Project()).ShouldBeNull();
            HolidayRuleCheck.FindProblem(new ProjectModel()).ShouldBeNull();
        }

        private static ProjectModel Project(params HolidayModel[] holidays)
        {
            Guid id = Guid.NewGuid();

            return new ProjectModel
            {
                Nodes = [new ProjectScenarioNodeModel { Id = id, Name = @"Base" }],
                Files = [new ProjectScenarioFileModel { NodeId = id, Scenario = new ProjectScenarioModel { HolidaySettings = new HolidaySettingsModel { Holidays = [.. holidays] } } }],
            };
        }
    }
}
