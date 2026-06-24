using Shouldly;
using System;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Edge-case tests for RecurrencePatternHelper that are not covered by the
    /// round-trip fixture (which exercises the happy path).
    /// Covers: day/frequency parsing utilities, error paths, and boundary values.
    /// </summary>
    public class RecurrencePatternHelperEdgeCaseTests
    {
        #region ParseFrequency - all tokens

        [Theory]
        [InlineData("SECONDLY", RecurrenceFrequency.Secondly)]
        [InlineData("MINUTELY", RecurrenceFrequency.Minutely)]
        [InlineData("HOURLY",   RecurrenceFrequency.Hourly)]
        [InlineData("DAILY",    RecurrenceFrequency.Daily)]
        [InlineData("WEEKLY",   RecurrenceFrequency.Weekly)]
        [InlineData("MONTHLY",  RecurrenceFrequency.Monthly)]
        [InlineData("YEARLY",   RecurrenceFrequency.Yearly)]
        public void ParseFrequency_KnownToken_ReturnsExpectedFrequency(string token, RecurrenceFrequency expected)
        {
            RecurrencePatternHelper.ParseFrequency(token).ShouldBe(expected);
        }

        [Theory]
        [InlineData("daily")]    // lowercase
        [InlineData("Daily")]    // mixed case
        public void ParseFrequency_CaseInsensitive_RecognisesLowerCase(string token)
        {
            RecurrencePatternHelper.ParseFrequency(token).ShouldBe(RecurrenceFrequency.Daily);
        }

        [Theory]
        [InlineData("")]
        [InlineData("UNKNOWN")]
        [InlineData("FORTNIGHTLY")]
        public void ParseFrequency_UnknownToken_ReturnsNone(string token)
        {
            RecurrencePatternHelper.ParseFrequency(token).ShouldBe(RecurrenceFrequency.None);
        }

        #endregion

        #region ParseDay - all tokens

        [Theory]
        [InlineData("MO", RecurrenceDay.MO)]
        [InlineData("TU", RecurrenceDay.TU)]
        [InlineData("WE", RecurrenceDay.WE)]
        [InlineData("TH", RecurrenceDay.TH)]
        [InlineData("FR", RecurrenceDay.FR)]
        [InlineData("SA", RecurrenceDay.SA)]
        [InlineData("SU", RecurrenceDay.SU)]
        public void ParseDay_KnownToken_ReturnsExpectedDay(string token, RecurrenceDay expected)
        {
            RecurrencePatternHelper.ParseDay(token).ShouldBe(expected);
        }

        [Theory]
        [InlineData("mo")]
        [InlineData("Mo")]
        public void ParseDay_LowercaseMO_ReturnsMonday(string token)
        {
            RecurrencePatternHelper.ParseDay(token).ShouldBe(RecurrenceDay.MO);
        }

        #endregion

        #region ToDayToken - all days

        [Theory]
        [InlineData(RecurrenceDay.MO, "MO")]
        [InlineData(RecurrenceDay.TU, "TU")]
        [InlineData(RecurrenceDay.WE, "WE")]
        [InlineData(RecurrenceDay.TH, "TH")]
        [InlineData(RecurrenceDay.FR, "FR")]
        [InlineData(RecurrenceDay.SA, "SA")]
        [InlineData(RecurrenceDay.SU, "SU")]
        public void ToDayToken_AllDays_RoundTripWithParseDay(RecurrenceDay day, string expectedToken)
        {
            string token = RecurrencePatternHelper.ToDayToken(day);
            token.ShouldBe(expectedToken);
            RecurrencePatternHelper.ParseDay(token).ShouldBe(day);
        }

        #endregion

        #region ToRecurrenceDay / ToDayOfWeek round-trips

        [Theory]
        [InlineData(DayOfWeek.Monday,    RecurrenceDay.MO)]
        [InlineData(DayOfWeek.Tuesday,   RecurrenceDay.TU)]
        [InlineData(DayOfWeek.Wednesday, RecurrenceDay.WE)]
        [InlineData(DayOfWeek.Thursday,  RecurrenceDay.TH)]
        [InlineData(DayOfWeek.Friday,    RecurrenceDay.FR)]
        [InlineData(DayOfWeek.Saturday,  RecurrenceDay.SA)]
        [InlineData(DayOfWeek.Sunday,    RecurrenceDay.SU)]
        public void ToRecurrenceDay_ToDayOfWeek_RoundTrips(DayOfWeek dotNetDay, RecurrenceDay expected)
        {
            RecurrenceDay recurrenceDay = RecurrencePatternHelper.ToRecurrenceDay(dotNetDay);
            recurrenceDay.ShouldBe(expected);
            RecurrencePatternHelper.ToDayOfWeek(recurrenceDay).ShouldBe(dotNetDay);
        }

        #endregion

        #region ToFrequencyToken - all frequencies

        [Theory]
        [InlineData(RecurrenceFrequency.Secondly, "SECONDLY")]
        [InlineData(RecurrenceFrequency.Minutely, "MINUTELY")]
        [InlineData(RecurrenceFrequency.Hourly,   "HOURLY")]
        [InlineData(RecurrenceFrequency.Daily,    "DAILY")]
        [InlineData(RecurrenceFrequency.Weekly,   "WEEKLY")]
        [InlineData(RecurrenceFrequency.Monthly,  "MONTHLY")]
        [InlineData(RecurrenceFrequency.Yearly,   "YEARLY")]
        public void ToFrequencyToken_AllFrequencies_ReturnExpectedTokens(RecurrenceFrequency freq, string expectedToken)
        {
            RecurrencePatternHelper.ToFrequencyToken(freq).ShouldBe(expectedToken);
        }

        #endregion

        #region ToRule - error paths

        [Fact]
        public void ToRule_NullInput_Returns_EmptyModel()
        {
            // null is treated the same as empty string.
            RecurrenceRuleModel model = RecurrencePatternHelper.ToRule(null);
            model.Frequency.ShouldBe(RecurrenceFrequency.None);
        }

        [Fact]
        public void ToRule_WhitespaceOnly_Returns_EmptyModel()
        {
            RecurrenceRuleModel model = RecurrencePatternHelper.ToRule("   ");
            model.Frequency.ShouldBe(RecurrenceFrequency.None);
        }

        [Fact]
        public void ToRule_MissingFrequency_Throws_FormatException()
        {
            Should.Throw<FormatException>(() => RecurrencePatternHelper.ToRule("COUNT=5"));
        }

        [Fact]
        public void ToRule_DuplicateKey_Throws_FormatException()
        {
            Should.Throw<FormatException>(() =>
                RecurrencePatternHelper.ToRule("FREQ=DAILY;FREQ=WEEKLY"));
        }

        [Fact]
        public void ToRule_BothCountAndUntil_Throws_InvalidOperationException()
        {
            Should.Throw<InvalidOperationException>(() =>
                RecurrencePatternHelper.ToRule("FREQ=DAILY;COUNT=5;UNTIL=20261231T000000"));
        }

        [Fact]
        public void ToRule_ZeroInterval_Throws_FormatException_Or_InvalidOperationException()
        {
            // INTERVAL=0 is not a positive integer so the parser should throw.
            Should.Throw<Exception>(() => RecurrencePatternHelper.ToRule("FREQ=DAILY;INTERVAL=0"));
        }

        #endregion

        #region ToPattern - FREQ=NONE produces empty string

        [Fact]
        public void ToPattern_FrequencyNone_Returns_EmptyString()
        {
            var model = new RecurrenceRuleModel { Frequency = RecurrenceFrequency.None };
            RecurrencePatternHelper.ToPattern(model).ShouldBe(string.Empty);
        }

        #endregion

        #region ToPattern - interval of 1 is omitted, other values are included

        [Fact]
        public void ToPattern_Interval_Of_One_Is_Omitted_From_Output()
        {
            var model = new RecurrenceRuleModel { Frequency = RecurrenceFrequency.Daily, Interval = 1 };
            string pattern = RecurrencePatternHelper.ToPattern(model);
            pattern.ShouldNotContain("INTERVAL");
        }

        [Fact]
        public void ToPattern_Interval_Greater_Than_One_Is_Included()
        {
            var model = new RecurrenceRuleModel { Frequency = RecurrenceFrequency.Daily, Interval = 3 };
            string pattern = RecurrencePatternHelper.ToPattern(model);
            pattern.ShouldContain("INTERVAL=3");
        }

        #endregion

        #region ToRule - UNTIL date-only format is accepted

        [Fact]
        public void ToRule_UntilDateOnly_Format_Is_Parsed()
        {
            // iCal allows UNTIL=YYYYMMDD (without the time component).
            RecurrenceRuleModel model = RecurrencePatternHelper.ToRule("FREQ=DAILY;UNTIL=20261231");
            model.Frequency.ShouldBe(RecurrenceFrequency.Daily);
            model.Until.ShouldNotBeNull();
            model.Until!.Value.Year.ShouldBe(2026);
            model.Until!.Value.Month.ShouldBe(12);
            model.Until!.Value.Day.ShouldBe(31);
        }

        #endregion

        #region Negative BYMONTHDAY is preserved

        [Fact]
        public void ToRule_NegativeByMonthDay_Preserved()
        {
            // "BYMONTHDAY=-1" means the last day of the month.
            RecurrenceRuleModel model = RecurrencePatternHelper.ToRule("FREQ=MONTHLY;BYMONTHDAY=-1");
            model.ByMonthDay.ShouldContain(-1);
        }

        [Fact]
        public void ToPattern_NegativeByMonthDay_RoundTrips()
        {
            var model = new RecurrenceRuleModel
            {
                Frequency  = RecurrenceFrequency.Monthly,
                ByMonthDay = [-1],
            };
            string pattern = RecurrencePatternHelper.ToPattern(model);
            RecurrenceRuleModel reparsed = RecurrencePatternHelper.ToRule(pattern);
            reparsed.ByMonthDay.ShouldContain(-1);
        }

        #endregion
    }
}
