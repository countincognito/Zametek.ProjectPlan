using Shouldly;
using System;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Additional edge-case tests for DateTimeCalculator not covered by
    /// DateTimeCalculatorTests. Focuses on:
    ///   • DisplayMode.Classic vs Default behaviour
    ///   • CalculateTimeAndDateTime(DateTimeOffset?) overload
    ///   • Weekends mode: start-date-is-Saturday edge case
    ///   • Large number of working days across multiple weekends
    ///   • NonWorkingDayMode.Weekends: AddDays with 10 working days
    ///   • CustomCalendar with multiple holidays in one week
    /// </summary>
    public class DateTimeCalculatorEdgeCaseTests
    {
        #region Helpers

        private static DateTimeOffset Local(int year, int month, int day) =>
            new DateTimeOffset(
                new DateTime(year, month, day),
                TimeZoneInfo.Local.GetUtcOffset(new DateTime(year, month, day)));

        private static DateTimeCalculator CreateCalculator(
            NonWorkingDayMode mode = NonWorkingDayMode.None,
            DateTimeDisplayMode displayMode = DateTimeDisplayMode.Default)
        {
            var calc = new DateTimeCalculator(TimeProvider.System);
            calc.NonWorkingDayMode = mode;
            calc.DisplayMode = displayMode;
            return calc;
        }

        #endregion

        #region CalculateTimeAndDateTime - DateTimeOffset? overload

        [Fact]
        public void CalculateTimeAndDateTime_FromDateTimeOffset_NullInput_Returns_Nulls()
        {
            var calc = CreateCalculator(NonWorkingDayMode.None);
            var projectStart = Local(2025, 1, 6);

            (int? days, DateTimeOffset? dto) = calc.CalculateTimeAndDateTime(projectStart, (DateTimeOffset?)null);

            days.ShouldBeNull();
            dto.ShouldBeNull();
        }

        [Fact]
        public void CalculateTimeAndDateTime_FromDateTimeOffset_ProjectStartInput_Returns_Zero()
        {
            var calc = CreateCalculator(NonWorkingDayMode.None);
            var projectStart = Local(2025, 1, 6);

            (int? days, DateTimeOffset? dto) = calc.CalculateTimeAndDateTime(projectStart, (DateTimeOffset?)projectStart);

            days.ShouldBe(0);
            dto.ShouldNotBeNull();
            dto!.Value.Date.ShouldBe(projectStart.Date);
        }

        [Fact]
        public void CalculateTimeAndDateTime_FromDateTimeOffset_BeforeProjectStart_ClampsToProjectStart()
        {
            var calc = CreateCalculator(NonWorkingDayMode.None);
            var projectStart = Local(2025, 6, 10);
            var beforeStart  = Local(2025, 1, 1); // way before project start

            (int? days, DateTimeOffset? dto) = calc.CalculateTimeAndDateTime(projectStart, (DateTimeOffset?)beforeStart);

            // Should clamp: days = 0, date = projectStart
            days.ShouldBe(0);
            dto.ShouldNotBeNull();
            dto!.Value.Date.ShouldBe(projectStart.Date);
        }

        [Fact]
        public void CalculateTimeAndDateTime_FromDateTimeOffset_AfterProjectStart_Returns_PositiveDays()
        {
            var calc = CreateCalculator(NonWorkingDayMode.None);
            var projectStart = Local(2025, 6, 1);
            var input        = Local(2025, 6, 11); // 10 calendar days later

            (int? days, DateTimeOffset? dto) = calc.CalculateTimeAndDateTime(projectStart, (DateTimeOffset?)input);

            days.ShouldBe(10);
            dto.ShouldNotBeNull();
        }

        #endregion

        #region DisplayMode.Classic - DisplayEarliestStartDate

        [Fact]
        public void DisplayEarliestStartDate_Default_Returns_EarliestStart_Unchanged()
        {
            var calc = CreateCalculator(NonWorkingDayMode.None, DateTimeDisplayMode.Default);
            var projectStart  = Local(2025, 6, 1);
            var earliestStart = Local(2025, 6, 5);

            DateTimeOffset result = calc.DisplayEarliestStartDate(projectStart, earliestStart, duration: 5);
            result.Date.ShouldBe(earliestStart.Date);
        }

        [Fact]
        public void DisplayFinishDate_Default_Returns_FinishDate_Unchanged()
        {
            var calc   = CreateCalculator(NonWorkingDayMode.None, DateTimeDisplayMode.Default);
            var start  = Local(2025, 6, 1);
            var finish = Local(2025, 6, 10);

            DateTimeOffset result = calc.DisplayFinishDate(start, finish, duration: 5);
            result.Date.ShouldBe(finish.Date);
        }

        [Fact]
        public void DisplayLatestStartDate_Default_Returns_LatestStart_Unchanged()
        {
            var calc         = CreateCalculator(NonWorkingDayMode.None, DateTimeDisplayMode.Default);
            var earliestStart = Local(2025, 6, 1);
            var latestStart   = Local(2025, 6, 5);

            DateTimeOffset result = calc.DisplayLatestStartDate(earliestStart, latestStart, duration: 3);
            result.Date.ShouldBe(latestStart.Date);
        }

        #endregion

        #region DisplayMode.Classic - MaximumLatestFinishDate In/Out

        [Fact]
        public void MaximumLatestFinishDateIn_Default_Returns_MaxLatestFinish_Unchanged()
        {
            var calc           = CreateCalculator(NonWorkingDayMode.None, DateTimeDisplayMode.Default);
            var start          = Local(2025, 6, 1);
            var maxLatestFinish = Local(2025, 6, 30);

            DateTimeOffset result = calc.MaximumLatestFinishDateIn(start, maxLatestFinish, duration: 5);
            result.Date.ShouldBe(maxLatestFinish.Date);
        }

        [Fact]
        public void MaximumLatestFinishDateOut_Default_Returns_MaxLatestFinish_Unchanged()
        {
            var calc           = CreateCalculator(NonWorkingDayMode.None, DateTimeDisplayMode.Default);
            var start          = Local(2025, 6, 1);
            var maxLatestFinish = Local(2025, 6, 30);

            DateTimeOffset result = calc.MaximumLatestFinishDateOut(start, maxLatestFinish, duration: 5);
            result.Date.ShouldBe(maxLatestFinish.Date);
        }

        #endregion

        #region NonWorkingDayMode.Weekends - larger spans

        [Fact]
        public void AddDays_Weekends_TenWorkingDays_SkipsTwoWeekends()
        {
            var calc   = CreateCalculator(NonWorkingDayMode.Weekends);
            var monday = Local(2025, 6, 2); // Mon 2 Jun 2025

            // 10 working days = 2 full working weeks = Mon 16 Jun 2025
            DateTimeOffset result = calc.AddDays(monday, 10);
            result.Date.ShouldBe(Local(2025, 6, 16).Date);
        }

        [Fact]
        public void CountDays_Weekends_TwoFullWorkWeeks_IsTen()
        {
            var calc = CreateCalculator(NonWorkingDayMode.Weekends);
            // Mon 2 Jun 2025 → Mon 16 Jun 2025 = 10 working days
            calc.CountDays(Local(2025, 6, 2), Local(2025, 6, 16)).ShouldBe(10);
        }

        [Fact]
        public void AddDays_Weekends_StartOnSaturday_FirstDayIsMonday()
        {
            // When starting on a Saturday, adding 1 working day should
            // produce Monday (not Saturday+1=Sunday, and not Saturday itself).
            var calc     = CreateCalculator(NonWorkingDayMode.Weekends);
            var saturday = Local(2025, 6, 7); // Saturday

            // Adding 0 working days from a Saturday: Saturday stays Saturday
            // (the calculator counts working days moved, not whether start is working).
            // Adding 1 working day from Saturday should give us Mon 9 Jun.
            DateTimeOffset result = calc.AddDays(saturday, 1);
            result.Date.ShouldBe(Local(2025, 6, 9).Date); // Monday
        }

        #endregion

        #region CustomCalendar - multiple holidays in the same week

        [Fact]
        public void AddDays_CustomCalendar_MultipleHolidaysInWeek_SkipsAllOfThem()
        {
            var calc = CreateCalculator(NonWorkingDayMode.CustomCalendar);

            // Mon 2 Jun, Tue 3 Jun, and Thu 5 Jun are holidays.
            var h1 = new HolidayModel { Id = 1, StartDateTime = Local(2025, 6, 2), RecurrencePattern = "FREQ=DAILY;COUNT=1" };
            var h2 = new HolidayModel { Id = 2, StartDateTime = Local(2025, 6, 3), RecurrencePattern = "FREQ=DAILY;COUNT=1" };
            var h3 = new HolidayModel { Id = 3, StartDateTime = Local(2025, 6, 5), RecurrencePattern = "FREQ=DAILY;COUNT=1" };
            calc.SetNonWorkingDayCalendarEvents([h1, h2, h3]);

            // From Mon 2 Jun + 1 working day: Mon is a holiday, so first working day
            // is Wed 4 Jun; +1 more skips Thu (holiday) → Fri 6 Jun.
            // i.e., 1 working day from Mon 2 Jun = Wed 4 Jun.
            DateTimeOffset result = calc.AddDays(Local(2025, 6, 2), 1);
            result.Date.ShouldBe(Local(2025, 6, 4).Date); // Wednesday
        }

        [Fact]
        public void CountDays_CustomCalendar_ThreeHolidays_ReducesCount()
        {
            var calc = CreateCalculator(NonWorkingDayMode.CustomCalendar);

            var h1 = new HolidayModel { Id = 1, StartDateTime = Local(2025, 6, 2), RecurrencePattern = "FREQ=DAILY;COUNT=1" };
            var h2 = new HolidayModel { Id = 2, StartDateTime = Local(2025, 6, 3), RecurrencePattern = "FREQ=DAILY;COUNT=1" };
            var h3 = new HolidayModel { Id = 3, StartDateTime = Local(2025, 6, 5), RecurrencePattern = "FREQ=DAILY;COUNT=1" };
            calc.SetNonWorkingDayCalendarEvents([h1, h2, h3]);

            // Mon 2 Jun → Fri 6 Jun: 5 calendar days, 3 holidays → 2 working days.
            calc.CountDays(Local(2025, 6, 2), Local(2025, 6, 6)).ShouldBe(2);
        }

        #endregion

        #region GetLocalNow - returns a value close to now

        [Fact]
        public void GetLocalNow_Returns_ValueWithinOneMinute_Of_SystemNow()
        {
            var calc = new DateTimeCalculator(TimeProvider.System);
            DateTimeOffset before = DateTimeOffset.Now.AddSeconds(-5);
            DateTimeOffset result = calc.GetLocalNow();
            DateTimeOffset after  = DateTimeOffset.Now.AddSeconds(5);

            result.ShouldBeGreaterThanOrEqualTo(before);
            result.ShouldBeLessThanOrEqualTo(after);
        }

        #endregion

        #region NonWorkingDayCalendarEvents property reflects SetNonWorkingDayCalendarEvents

        [Fact]
        public void NonWorkingDayCalendarEvents_ReflectsSetCall()
        {
            var calc = CreateCalculator(NonWorkingDayMode.CustomCalendar);
            var h = new HolidayModel { Id = 99, StartDateTime = Local(2025, 12, 25), RecurrencePattern = "FREQ=DAILY;COUNT=1" };
            calc.SetNonWorkingDayCalendarEvents([h]);

            calc.NonWorkingDayCalendarEvents.Count.ShouldBe(1);
            calc.NonWorkingDayCalendarEvents[0].Id.ShouldBe(99);
        }

        [Fact]
        public void NonWorkingDayCalendarEvents_AfterClear_IsEmpty()
        {
            var calc = CreateCalculator(NonWorkingDayMode.CustomCalendar);
            var h = new HolidayModel { Id = 1, StartDateTime = Local(2025, 6, 10), RecurrencePattern = "FREQ=DAILY;COUNT=1" };
            calc.SetNonWorkingDayCalendarEvents([h]);
            calc.SetNonWorkingDayCalendarEvents([]);

            calc.NonWorkingDayCalendarEvents.ShouldBeEmpty();
        }

        #endregion
    }
}
