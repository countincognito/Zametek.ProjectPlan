using System;
using Xunit;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    public class RRuleLanguageParserTests
    {




        [Theory]
        [InlineData("FREQ=DAILY", "Every day")]
        [InlineData("FREQ=DAILY;INTERVAL=2;COUNT=10", "Every 2 days for 10 times")]
        [InlineData("FREQ=DAILY;UNTIL=20261231", "Every day until 31 Dec 2026")]
        [InlineData("FREQ=DAILY;UNTIL=20261231T235959Z", "Every day until 31 Dec 2026")]
        [InlineData("FREQ=WEEKLY", "Every week")]
        [InlineData("FREQ=WEEKLY;BYDAY=MO,WE,FR", "Every week on Monday, Wednesday and Friday")]
        [InlineData("FREQ=WEEKLY;INTERVAL=2;BYDAY=TU,TH", "Every 2 weeks on Tuesday and Thursday")]
        [InlineData("FREQ=MONTHLY;BYMONTHDAY=1,15", "Every month on the 1st and 15th")]
        [InlineData("FREQ=MONTHLY;BYMONTHDAY=-1", "Every month on the 1st from the end")]
        [InlineData("FREQ=MONTHLY;BYSETPOS=4;BYDAY=SU;INTERVAL=5", "Every 5 months on the 4th Sunday")]
        [InlineData("FREQ=MONTHLY;BYSETPOS=2,3;BYDAY=MO,FR", "Every month on the 2nd and 3rd Monday and Friday")]
        [InlineData("FREQ=MONTHLY;INTERVAL=3;BYMONTHDAY=10;UNTIL=20251231", "Every 3 months on the 10th until 31 Dec 2025")]
        [InlineData("FREQ=YEARLY;BYMONTH=4;BYMONTHDAY=10", "Every year on the 10th of April")]
        [InlineData("FREQ=YEARLY;BYMONTH=1,7;BYMONTHDAY=1,15", "Every year on the 1st and 15th of January and July")]
        [InlineData("FREQ=HOURLY;INTERVAL=3;COUNT=4", "Every 3 hours for 4 times")]
        [InlineData("FREQ=MINUTELY;INTERVAL=10", "Every 10 minutes")]
        [InlineData("FREQ=SECONDLY", "Every second")]
        [InlineData("FREQ=DAILY;BYHOUR=10;BYMINUTE=30", "Every day")] // unsupported parts ignored
        [InlineData("FREQ=MONTHLY;BYMONTHDAY=1,2,3,4,11,21,22,23", "Every month on the 1st, 2nd, 3rd, 4th, 11th, 21st, 22nd and 23rd")]
        [InlineData("FREQ=WEEKLY;BYDAY=1SU,2MO", "Every week on Sunday and Monday")]
        [InlineData("FREQ=MONTHLY;BYSETPOS=-1;BYDAY=FR", "Every month on the 1st from the end Friday")]
        public void ToText_ReturnsExpected_ForVariousRules(string rrule, string expected)
        {
            var actual = RRuleLanguageParser.ToText(rrule);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("FREQ=DAILY", 2026, 4, 13, "Every day starting 13 Apr 2026")]
        public void ToText_UsesDtStart_WhenOnlyFreq(
            string rrule, int year, int month, int day, string expected)
        {
            var dtStart = new DateTime(year, month, day);
            var actual = RRuleLanguageParser.ToText(rrule, dtStart);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void ToText_InvalidOrEmptyRRule_Throws(string rrule)
        {
            Assert.Throws<ArgumentException>(() => RRuleLanguageParser.ToText(rrule));
        }







        [Fact]
        public void Daily_DefaultInterval()
        {
            var text = RRuleLanguageParser.ToText("FREQ=DAILY");
            Assert.Equal("Every day", text);
        }

        [Fact]
        public void Daily_WithIntervalAndCount()
        {
            var text = RRuleLanguageParser.ToText("FREQ=DAILY;INTERVAL=2;COUNT=10");
            Assert.Equal("Every 2 days for 10 times", text);
        }

        [Fact]
        public void Daily_WithUntilDate()
        {
            var text = RRuleLanguageParser.ToText("FREQ=DAILY;UNTIL=20261231");
            Assert.Equal("Every day until 31 Dec 2026", text);
        }

        [Fact]
        public void Daily_WithUntilDateTimeUtc()
        {
            var text = RRuleLanguageParser.ToText("FREQ=DAILY;UNTIL=20261231T235959Z");
            // Date may render depending on culture, but we force invariant "d MMM yyyy"
            Assert.Equal("Every day until 31 Dec 2026", text);
        }

        [Fact]
        public void Weekly_Simple()
        {
            var text = RRuleLanguageParser.ToText("FREQ=WEEKLY");
            Assert.Equal("Every week", text);
        }

        [Fact]
        public void Weekly_WithDays()
        {
            var text = RRuleLanguageParser.ToText("FREQ=WEEKLY;BYDAY=MO,WE,FR");
            Assert.Equal("Every week on Monday, Wednesday and Friday", text);
        }

        [Fact]
        public void Weekly_WithIntervalAndDays()
        {
            var text = RRuleLanguageParser.ToText("FREQ=WEEKLY;INTERVAL=2;BYDAY=TU,TH");
            Assert.Equal("Every 2 weeks on Tuesday and Thursday", text);
        }

        [Fact]
        public void Monthly_OnSpecificDays()
        {
            var text = RRuleLanguageParser.ToText("FREQ=MONTHLY;BYMONTHDAY=1,15");
            Assert.Equal("Every month on the 1st and 15th", text);
        }

        [Fact]
        public void Monthly_NegativeMonthDay()
        {
            var text = RRuleLanguageParser.ToText("FREQ=MONTHLY;BYMONTHDAY=-1");
            Assert.Equal("Every month on the 1st from the end", text);
        }

        [Fact]
        public void Monthly_Every5Months_OnFourthSunday()
        {
            var text = RRuleLanguageParser.ToText("FREQ=MONTHLY;BYSETPOS=4;BYDAY=SU;INTERVAL=5");
            Assert.Equal("Every 5 months on the 4th Sunday", text);
        }

        [Fact]
        public void Monthly_EveryMonth_OnSecondMondayAndThirdFriday()
        {
            var text = RRuleLanguageParser.ToText("FREQ=MONTHLY;BYSETPOS=2,3;BYDAY=MO,FR");
            Assert.Equal("Every month on the 2nd and 3rd Monday and Friday", text);
        }

        [Fact]
        public void Monthly_IntervalAndUntil()
        {
            var text = RRuleLanguageParser.ToText("FREQ=MONTHLY;INTERVAL=3;BYMONTHDAY=10;UNTIL=20251231");
            Assert.Equal("Every 3 months on the 10th until 31 Dec 2025", text);
        }

        [Fact]
        public void Yearly_OnMonthAndDay()
        {
            var text = RRuleLanguageParser.ToText("FREQ=YEARLY;BYMONTH=4;BYMONTHDAY=10");
            Assert.Equal("Every year on the 10th of April", text);
        }

        [Fact]
        public void Yearly_MultipleMonthsAndDays()
        {
            var text = RRuleLanguageParser.ToText("FREQ=YEARLY;BYMONTH=1,7;BYMONTHDAY=1,15");
            Assert.Equal("Every year on the 1st and 15th of January and July", text);
        }

        [Fact]
        public void Hourly_WithCount()
        {
            var text = RRuleLanguageParser.ToText("FREQ=HOURLY;INTERVAL=3;COUNT=4");
            Assert.Equal("Every 3 hours for 4 times", text);
        }

        [Fact]
        public void Minutely_Simple()
        {
            var text = RRuleLanguageParser.ToText("FREQ=MINUTELY;INTERVAL=10");
            Assert.Equal("Every 10 minutes", text);
        }

        [Fact]
        public void Secondly_Simple()
        {
            var text = RRuleLanguageParser.ToText("FREQ=SECONDLY");
            Assert.Equal("Every second", text);
        }

        [Fact]
        public void UsesDtStartWhenOnlyFreq()
        {
            var dt = new DateTime(2026, 4, 13);
            var text = RRuleLanguageParser.ToText("FREQ=DAILY", dt);
            Assert.Equal("Every day starting 13 Apr 2026", text);
        }

        [Fact]
        public void IgnoresUnsupportedParts()
        {
            var text = RRuleLanguageParser.ToText("FREQ=DAILY;BYHOUR=10;BYMINUTE=30");
            // We ignore BYHOUR/BYMINUTE for now
            Assert.Equal("Every day", text);
        }

        [Fact]
        public void InvalidOrEmptyRRule_Throws()
        {
            Assert.Throws<ArgumentException>(() => RRuleLanguageParser.ToText(""));
            Assert.Throws<ArgumentException>(() => RRuleLanguageParser.ToText("   "));
        }

        [Fact]
        public void OrdinalFormatting_Positive()
        {
            // Use reflection to call private ToOrdinal for sanity check if needed,
            // or rely on observable behavior via BYMONTHDAY
            var text = RRuleLanguageParser.ToText("FREQ=MONTHLY;BYMONTHDAY=1,2,3,4,11,21,22,23");
            Assert.Equal("Every month on the 1st, 2nd, 3rd, 4th, 11th, 21st, 22nd and 23rd", text);
        }

        [Fact]
        public void ByDay_WithOrdinalsIgnoredInWeekly()
        {
            // For WEEKLY we ignore ordinals like 1SU
            var text = RRuleLanguageParser.ToText("FREQ=WEEKLY;BYDAY=1SU,2MO");
            Assert.Equal("Every week on Sunday and Monday", text);
        }

        [Fact]
        public void ByDay_WithNegativeOrdinalInMonthlySetpos()
        {
            var text = RRuleLanguageParser.ToText("FREQ=MONTHLY;BYSETPOS=-1;BYDAY=FR");
            Assert.Equal("Every month on the 1st from the end Friday", text);
        }
    }
}