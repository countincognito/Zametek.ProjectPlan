using Shouldly;
using Xunit;

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
    }
}
