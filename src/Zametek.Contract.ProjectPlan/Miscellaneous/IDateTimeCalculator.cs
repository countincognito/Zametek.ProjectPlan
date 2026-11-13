using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IDateTimeCalculator
    {
        NonWorkingDayMode NonWorkingDayMode { get; set; }

        DateTimeDisplayMode DisplayMode { get; set; }

        DateTimeOffset ProjectStart { get; set; }

        DateTimeOffset NonWorkingDaysStart { get; }

        DateTimeOffset NonWorkingDaysFinish { get; }

        List<HolidayModel> NonWorkingDayCalendarEvents { get; }

        void SetNonWorkingDayCalendarEvents(List<HolidayModel> nonWorkingDayCalendarEvents);

        TimeZoneInfo LocalTimeZone { get; }

        DateTimeOffset GetLocalNow();

        DateTimeOffset GetLocalToday();

        DateTimeOffset GetLocal(DateTime dateTime);

        (int?, DateTimeOffset?) CalculateTimeAndDateTime(DateTimeOffset projectStart, int? input);

        (int?, DateTimeOffset?) CalculateTimeAndDateTime(DateTimeOffset projectStart, DateTimeOffset? input);

        // As the overload for a date, for the date of a constraint (a minimum earliest start or a maximum latest finish): a
        // date after the project start that is not a working day counts as the next working day.
        (int?, DateTimeOffset?) CalculateConstraintTimeAndDateTime(DateTimeOffset projectStart, DateTimeOffset? input);

        DateTimeOffset AddDays(DateTimeOffset startDateTime, int days);

        // As AddDays, but false when the count meets a run of more days off than DateTimeCalculator.MaximumNonWorkingRun.
        // The count then takes the next day as a working day, so that it ends, and the result is not a date of the calendar.
        bool TryAddDays(DateTimeOffset startDateTime, int days, out DateTimeOffset result);

        int CountDays(DateTimeOffset current, DateTimeOffset toCompareWith);

        bool IsNonWorkingDay(DateTimeOffset date);

        // The first working day on the date or after it. Null when the calendar has no working day in the
        // DateTimeCalculator.MaximumNonWorkingRun days from the date.
        DateTimeOffset? FirstWorkingDayOnOrAfter(DateTimeOffset date);

        DateTimeOffset DisplayEarliestStartDate(DateTimeOffset projectStart, DateTimeOffset earliestStart, int duration);

        DateTimeOffset DisplayLatestStartDate(DateTimeOffset earliestStart, DateTimeOffset latestStart, int duration);

        DateTimeOffset DisplayFinishDate(DateTimeOffset start, DateTimeOffset finish, int duration);

        DateTimeOffset MaximumLatestFinishDateIn(DateTimeOffset start, DateTimeOffset maxLatestFinish, int duration);

        DateTimeOffset MaximumLatestFinishDateOut(DateTimeOffset start, DateTimeOffset maxLatestFinish, int duration);
    }
}
