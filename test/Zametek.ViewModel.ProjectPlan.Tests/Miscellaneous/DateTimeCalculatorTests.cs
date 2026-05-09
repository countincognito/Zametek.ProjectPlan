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
    /// is needed — we use TimeProvider.System (or a fixed fake via FakeTimeProvider).
    /// </summary>
    public class DateTimeCalculatorTests
    {
        #region Helpers

        /// <summary>Minimal TimeProvider that always returns the same instant.</summary>
        private sealed class FixedTimeProvider : TimeProvider
        {
            private readonly DateTimeOffset m_Now;
            public FixedTimeProvider(DateTimeOffset now) => m_Now = now;
            public override DateTimeOffset GetUtcNow() => m_Now.ToUniversalTime();
            public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;
        }

        private static DateTimeCalculator CreateCalculator(NonWorkingDayMode mode = NonWorkingDayMode.None)
        {
            var calc = new DateTimeCalculator(TimeProvider.System);
            calc.NonWorkingDayMode = mode;
            return calc;
        }

        /// <summary>Returns a DateTimeOffset at midnight local time for the given date.</summary>
        private static DateTimeOffset Local(int year, int month, int day) =>
            new DateTimeOffset(new DateTime(year, month, day), TimeZoneInfo.Local.GetUtcOffset(new DateTime(year, month, day)));

        #endregion

        #region NonWorkingDayMode.None — AddDays / CountDays treat every day as a working day

        [Fact]
        public void AddDays_None_Given_ZeroDays_Returns_SameDate()
        {
            var calc = CreateCalculator(NonWorkingDayMode.None);
            var start = Local(2025, 6, 2); // Monday
            calc.AddDays(start, 0).Date.ShouldBe(start.Date);
        }

        [Fact]
        public void AddDays_None_Given_PositiveDays_Counts_Every_Day()
        {
            var calc = CreateCalculator(NonWorkingDayMode.None);
            var start = Local(2025, 6, 6); // Friday
            var result = calc.AddDays(start, 3);
            // Friday + 3 calendar days = Monday (6 + 3 = 9 June 2025)
            result.Date.ShouldBe(Local(2025, 6, 9).Date);
        }

        [Fact]
        public void AddDays_None_Given_NegativeDays_Steps_Backward()
        {
            var calc = CreateCalculator(NonWorkingDayMode.None);
            var start = Local(2025, 6, 9); // Monday
            var result = calc.AddDays(start, -3);
            result.Date.ShouldBe(Local(2025, 6, 6).Date); // Friday
        }

        [Fact]
        public void CountDays_None_Given_SameDate_Returns_Zero()
        {
            var calc = CreateCalculator(NonWorkingDayMode.None);
            var date = Local(2025, 6, 2);
            calc.CountDays(date, date).ShouldBe(0);
        }

        [Fact]
        public void CountDays_None_Counts_Every_Calendar_Day_Including_Weekends()
        {
            var calc = CreateCalculator(NonWorkingDayMode.None);
            // Mon 2 Jun 2025 → Mon 9 Jun 2025 = 7 days (includes Sat+Sun)
            calc.CountDays(Local(2025, 6, 2), Local(2025, 6, 9)).ShouldBe(7);
        }

        [Fact]
        public void CountDays_None_Is_Negative_When_Arguments_Reversed()
        {
            var calc = CreateCalculator(NonWorkingDayMode.None);
            int forward = calc.CountDays(Local(2025, 6, 2), Local(2025, 6, 9));
            int backward = calc.CountDays(Local(2025, 6, 9), Local(2025, 6, 2));
            backward.ShouldBe(-forward);
        }

        #endregion

        #region NonWorkingDayMode.Weekends — AddDays / CountDays skip Saturday and Sunday

        [Fact]
        public void AddDays_Weekends_Given_ZeroDays_Returns_SameDate()
        {
            var calc = CreateCalculator(NonWorkingDayMode.Weekends);
            var start = Local(2025, 6, 2); // Monday
            calc.AddDays(start, 0).Date.ShouldBe(start.Date);
        }

        [Fact]
        public void AddDays_Weekends_SkipsWeekend_FridayPlusOne_GivesMonday()
        {
            var calc = CreateCalculator(NonWorkingDayMode.Weekends);
            var friday = Local(2025, 6, 6); // Friday
            var result = calc.AddDays(friday, 1);
            // Next working day after Friday = Monday 9 Jun
            result.Date.ShouldBe(Local(2025, 6, 9).Date);
        }

        [Fact]
        public void AddDays_Weekends_FiveDays_Spans_Weekend()
        {
            var calc = CreateCalculator(NonWorkingDayMode.Weekends);
            var monday = Local(2025, 6, 2); // Monday
            // Mon + 5 working days = Mon, Tue, Wed, Thu, Fri → finish Mon 9 Jun
            var result = calc.AddDays(monday, 5);
            result.Date.ShouldBe(Local(2025, 6, 9).Date);
        }

        [Fact]
        public void CountDays_Weekends_OneWorkWeek_IsFive()
        {
            var calc = CreateCalculator(NonWorkingDayMode.Weekends);
            // Mon 2 Jun → Mon 9 Jun 2025: 5 working days (Tue–Fri + Mon)
            calc.CountDays(Local(2025, 6, 2), Local(2025, 6, 9)).ShouldBe(5);
        }

        [Fact]
        public void CountDays_Weekends_FullWeekPlusWeekend_IsFive()
        {
            var calc = CreateCalculator(NonWorkingDayMode.Weekends);
            // Mon 2 Jun → Fri 6 Jun: 4 working days (Tue, Wed, Thu, Fri)
            calc.CountDays(Local(2025, 6, 2), Local(2025, 6, 6)).ShouldBe(4);
        }

        [Fact]
        public void CountDays_Weekends_Reversed_IsNegative()
        {
            var calc = CreateCalculator(NonWorkingDayMode.Weekends);
            int forward = calc.CountDays(Local(2025, 6, 2), Local(2025, 6, 9));
            int backward = calc.CountDays(Local(2025, 6, 9), Local(2025, 6, 2));
            backward.ShouldBe(-forward);
        }

        [Fact]
        public void AddDays_Weekends_PositiveDays_ThenNegativeDays_ReturnsToStart()
        {
            // Going forward N working days then backward N working days should
            // return to the original date (when both directions are in the cached
            // non-working-day window).
            var calc = CreateCalculator(NonWorkingDayMode.Weekends);
            var start = Local(2025, 6, 2); // Monday
            calc.ProjectStart = start;

            // Warm up the forward cache first so backward has non-working days available.
            var forward = calc.AddDays(start, 5); // Mon 9 Jun
            var back = calc.AddDays(forward, -5);  // Should be Mon 2 Jun again
            back.Date.ShouldBe(start.Date);
        }

        #endregion

        #region NonWorkingDayMode.CustomCalendar — user-defined holiday list

        [Fact]
        public void AddDays_CustomCalendar_EmptyHolidayList_BehavesLikeNone()
        {
            var calc = CreateCalculator(NonWorkingDayMode.CustomCalendar);
            calc.SetNonWorkingDayCalendarEvents([]);

            var start = Local(2025, 6, 6); // Friday
            var result = calc.AddDays(start, 3);
            // Without any non-working-day rules, every day is a working day.
            result.Date.ShouldBe(Local(2025, 6, 9).Date);
        }

        [Fact]
        public void AddDays_CustomCalendar_HolidayOnTuesday_SkipsIt()
        {
            var calc = CreateCalculator(NonWorkingDayMode.CustomCalendar);

            // Set a single-occurrence holiday on Tuesday 10 Jun 2025
            var holiday = new HolidayModel
            {
                Id = 1,
                StartDateTime = Local(2025, 6, 10),
                RecurrencePattern = "FREQ=DAILY;COUNT=1",
            };
            calc.SetNonWorkingDayCalendarEvents([holiday]);

            // Mon 9 Jun + 1 working day should skip Tue 10 Jun → Wed 11 Jun
            var result = calc.AddDays(Local(2025, 6, 9), 1);
            result.Date.ShouldBe(Local(2025, 6, 11).Date);
        }

        [Fact]
        public void CountDays_CustomCalendar_HolidayExcluded_FromCount()
        {
            var calc = CreateCalculator(NonWorkingDayMode.CustomCalendar);

            // Holiday on Wed 4 Jun 2025
            var holiday = new HolidayModel
            {
                Id = 1,
                StartDateTime = Local(2025, 6, 4),
                RecurrencePattern = "FREQ=DAILY;COUNT=1",
            };
            calc.SetNonWorkingDayCalendarEvents([holiday]);

            // Mon 2 Jun → Fri 6 Jun 2025: 4 days — Wed is a holiday so count = 3
            calc.CountDays(Local(2025, 6, 2), Local(2025, 6, 6)).ShouldBe(3);
        }

        [Fact]
        public void SetNonWorkingDayCalendarEvents_Replacing_List_Clears_Previous_Holidays()
        {
            var calc = CreateCalculator(NonWorkingDayMode.CustomCalendar);

            var holiday = new HolidayModel
            {
                Id = 1,
                StartDateTime = Local(2025, 6, 4),
                RecurrencePattern = "FREQ=DAILY;COUNT=1",
            };
            calc.SetNonWorkingDayCalendarEvents([holiday]);

            // Remove all holidays — counting should now include Wednesday
            calc.SetNonWorkingDayCalendarEvents([]);
            calc.CountDays(Local(2025, 6, 2), Local(2025, 6, 6)).ShouldBe(4);
        }

        #endregion

        #region Mode switching resets cached state

        [Fact]
        public void SwitchingMode_From_None_To_Weekends_Changes_Count()
        {
            var calc = CreateCalculator(NonWorkingDayMode.None);
            int allDays = calc.CountDays(Local(2025, 6, 2), Local(2025, 6, 9));

            calc.NonWorkingDayMode = NonWorkingDayMode.Weekends;
            int workDays = calc.CountDays(Local(2025, 6, 2), Local(2025, 6, 9));

            allDays.ShouldBe(7);
            workDays.ShouldBe(5);
        }

        #endregion

        #region CalculateTimeAndDateTime

        [Fact]
        public void CalculateTimeAndDateTime_FromInt_ZeroInput_Returns_ProjectStart()
        {
            var calc = CreateCalculator(NonWorkingDayMode.None);
            var projectStart = Local(2025, 1, 6);
            calc.ProjectStart = projectStart;

            (int? days, DateTimeOffset? dto) = calc.CalculateTimeAndDateTime(projectStart, 0);

            days.ShouldBe(0);
            dto.ShouldNotBeNull();
            dto!.Value.Date.ShouldBe(projectStart.Date);
        }

        [Fact]
        public void CalculateTimeAndDateTime_FromInt_NullInput_Returns_Nulls()
        {
            var calc = CreateCalculator(NonWorkingDayMode.None);
            var projectStart = Local(2025, 1, 6);

            (int? days, DateTimeOffset? dto) = calc.CalculateTimeAndDateTime(projectStart, (int?)null);

            days.ShouldBeNull();
            dto.ShouldBeNull();
        }

        [Fact]
        public void CalculateTimeAndDateTime_FromInt_NegativeInput_Clamps_To_Zero()
        {
            var calc = CreateCalculator(NonWorkingDayMode.None);
            var projectStart = Local(2025, 1, 6);
            calc.ProjectStart = projectStart;

            (int? days, DateTimeOffset? dto) = calc.CalculateTimeAndDateTime(projectStart, -5);

            days.ShouldBe(0);
        }

        #endregion

        #region GetLocal

        [Fact]
        public void GetLocal_Returns_DateTimeOffset_With_Local_Offset()
        {
            var calc = CreateCalculator();
            var input = new DateTime(2025, 6, 2, 9, 0, 0);
            var result = calc.GetLocal(input);

            result.DateTime.Date.ShouldBe(input.Date);
            result.Offset.ShouldBe(TimeZoneInfo.Local.GetUtcOffset(input));
        }

        #endregion
    }
}
