using Shouldly;
using System;
using System.Collections.Generic;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for DateTimeCalculator covering working-day arithmetic, non-working-day
    /// detection, and business-day counting under each NonWorkingDayMode.
    /// DateTimeCalculator accepts a TimeProvider via constructor so no mocking framework
    /// is needed - we use TimeProvider.System (or a fixed fake via FakeTimeProvider).
    /// </summary>
    public class DateTimeCalculatorTests
    {
        #region Helpers

        private static DateTimeCalculator CreateCalc() => new DateTimeCalculator(TimeProvider.System);

        private static readonly DateTimeOffset s_Monday = new(2025, 6, 9, 0, 0, 0, TimeSpan.Zero); // known Monday

        /// <summary>Returns a DateTimeOffset at midnight local time for the given date.</summary>
        private static DateTimeOffset Local(int year, int month, int day) =>
            new DateTimeOffset(new DateTime(year, month, day), TimeZoneInfo.Local.GetUtcOffset(new DateTime(year, month, day)));

        #endregion

        #region AddDays - None mode

        [Fact]
        public void AddDays_None_PositiveN_AddsCalendarDays()
        {
            var calc = CreateCalc();
            calc.NonWorkingDayMode = NonWorkingDayMode.None;
            calc.AddDays(s_Monday, 5).Date.ShouldBe(new DateTime(2025, 6, 14));
        }

        [Fact]
        public void AddDays_None_Zero_ReturnsSameDay()
        {
            var calc = CreateCalc();
            calc.NonWorkingDayMode = NonWorkingDayMode.None;
            calc.AddDays(s_Monday, 0).Date.ShouldBe(s_Monday.Date);
        }

        [Fact]
        public void AddDays_None_NegativeN_SubtractsCalendarDays()
        {
            var calc = CreateCalc();
            calc.NonWorkingDayMode = NonWorkingDayMode.None;
            calc.AddDays(s_Monday, -3).Date.ShouldBe(new DateTime(2025, 6, 6));
        }

        #endregion

        #region AddDays - Weekends mode

        [Fact]
        public void AddDays_Weekends_SkipsSaturdayAndSunday()
        {
            // Monday + 5 business days should skip the weekend, landing on next Monday
            var calc = CreateCalc();
            calc.NonWorkingDayMode = NonWorkingDayMode.Weekends;
            calc.AddDays(s_Monday, 5).Date.ShouldBe(new DateTime(2025, 6, 16)); // Monday + 5 weekdays
        }

        [Fact]
        public void AddDays_Weekends_SingleDay_MondayToTuesday()
        {
            var calc = CreateCalc();
            calc.NonWorkingDayMode = NonWorkingDayMode.Weekends;
            calc.AddDays(s_Monday, 1).Date.ShouldBe(new DateTime(2025, 6, 10)); // Tuesday
        }

        [Fact]
        public void AddDays_Weekends_FridayPlusOne_JumpsToMonday()
        {
            var friday = new DateTimeOffset(2025, 6, 13, 0, 0, 0, TimeSpan.Zero);
            var calc = CreateCalc();
            calc.NonWorkingDayMode = NonWorkingDayMode.Weekends;
            calc.AddDays(friday, 1).Date.ShouldBe(new DateTime(2025, 6, 16)); // next Monday
        }

        #endregion

        #region AddDays - CustomCalendar mode

        [Fact]
        public void AddDays_CustomCalendar_NoEvents_BehavesLikeNone()
        {
            var calc = CreateCalc();
            calc.SetNonWorkingDayCalendarEvents([]);
            calc.NonWorkingDayMode = NonWorkingDayMode.CustomCalendar;
            calc.AddDays(s_Monday, 5).Date.ShouldBe(new DateTime(2025, 6, 14));
        }

        [Fact]
        public void AddDays_CustomCalendar_WithWeekendRule_SkipsWeekends()
        {
            var weekendHoliday = new HolidayModel
        {
                Id = 1,
                RecurrencePattern = "FREQ=WEEKLY;BYDAY=SA,SU",
            };
            var calc = CreateCalc();
            calc.SetNonWorkingDayCalendarEvents([weekendHoliday]);
            calc.NonWorkingDayMode = NonWorkingDayMode.CustomCalendar;
            // Should behave like weekends mode
            calc.AddDays(s_Monday, 5).Date.ShouldBe(new DateTime(2025, 6, 16));
        }

        #endregion

        #region CountDays

        [Fact]
        public void CountDays_None_SameDay_ReturnsZero()
        {
            var calc = CreateCalc();
            calc.NonWorkingDayMode = NonWorkingDayMode.None;
            calc.CountDays(s_Monday, s_Monday).ShouldBe(0);
        }

        [Fact]
        public void CountDays_None_FiveDays_ReturnsFive()
        {
            var calc = CreateCalc();
            calc.NonWorkingDayMode = NonWorkingDayMode.None;
            var later = s_Monday.AddDays(5);
            calc.CountDays(s_Monday, later).ShouldBe(5);
        }

        [Fact]
        public void CountDays_None_Reversed_ReturnsNegative()
        {
            var calc = CreateCalc();
            calc.NonWorkingDayMode = NonWorkingDayMode.None;
            var later = s_Monday.AddDays(5);
            calc.CountDays(later, s_Monday).ShouldBe(-5);
        }

        [Fact]
        public void CountDays_Weekends_OverWeekend_CountsOnlyWeekdays()
        {
            var calc = CreateCalc();
            calc.NonWorkingDayMode = NonWorkingDayMode.Weekends;
            var friday = new DateTimeOffset(2025, 6, 13, 0, 0, 0, TimeSpan.Zero);
            var nextMonday = new DateTimeOffset(2025, 6, 16, 0, 0, 0, TimeSpan.Zero);
            calc.CountDays(friday, nextMonday).ShouldBe(1); // only Monday counts
        }

        #endregion

        #region AddDays / CountDays symmetry

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(10)]
        public void AddDays_CountDays_None_AreSymmetric(int n)
        {
            var calc = CreateCalc();
            calc.NonWorkingDayMode = NonWorkingDayMode.None;
            var result = calc.AddDays(s_Monday, n);
            calc.CountDays(s_Monday, result).ShouldBe(n);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(10)]
        public void AddDays_CountDays_Weekends_AreSymmetric(int n)
            {
            var calc = CreateCalc();
            calc.NonWorkingDayMode = NonWorkingDayMode.Weekends;
            var result = calc.AddDays(s_Monday, n);
            calc.CountDays(s_Monday, result).ShouldBe(n);
        }

        #endregion

        #region Mode switching

        [Fact]
        public void SwitchingMode_ClearsNonWorkingDaysCache()
        {
            var calc = CreateCalc();
            calc.NonWorkingDayMode = NonWorkingDayMode.Weekends;
            var after5Business = calc.AddDays(s_Monday, 5);

            calc.NonWorkingDayMode = NonWorkingDayMode.None;
            var after5Calendar = calc.AddDays(s_Monday, 5);

            // In None mode 5 days is always shorter than 5 business days that cross a weekend
            after5Calendar.ShouldBeLessThan(after5Business);
        }

        #endregion

        #region CalculateTimeAndDateTime

        [Fact]
        public void CalculateTimeAndDateTime_Int_NullInput_ReturnsNulls()
        {
            var calc = CreateCalc();
            var (time, dt) = calc.CalculateTimeAndDateTime(s_Monday, (int?)null);
            time.ShouldBeNull();
            dt.ShouldBeNull();
        }

        #endregion

        #region CalculateTimeAndDateTime

        [Fact]
        public void CalculateTimeAndDateTime_Int_Zero_ReturnsZeroAndProjectStart()
        {
            var calc = CreateCalc();
            var (time, dt) = calc.CalculateTimeAndDateTime(s_Monday, (int?)0);
            time.ShouldBe(0);
            dt.ShouldNotBeNull();
            dt!.Value.Date.ShouldBe(s_Monday.Date);
        }

        [Fact]
        public void CalculateTimeAndDateTime_Int_Negative_ClampsToZero()
        {
            var calc = CreateCalc();
            var (time, dt) = calc.CalculateTimeAndDateTime(s_Monday, (int?)-5);
            time.ShouldBe(0);
        }

        [Fact]
        public void CalculateTimeAndDateTime_DateTime_NullInput_ReturnsNulls()
        {
            var calc = CreateCalc();
            var (time, dt) = calc.CalculateTimeAndDateTime(s_Monday, (DateTimeOffset?)null);
            time.ShouldBeNull();
            dt.ShouldBeNull();
        }

        #endregion

        #region GetLocal

        [Fact]
        public void CalculateTimeAndDateTime_DateTime_BeforeProjectStart_ClampsToStart()
        {
            var calc = CreateCalc();
            var before = s_Monday.AddDays(-10);
            var (time, dt) = calc.CalculateTimeAndDateTime(s_Monday, (DateTimeOffset?)before);
            time.ShouldBe(0);
        }

        #endregion
    }
}
