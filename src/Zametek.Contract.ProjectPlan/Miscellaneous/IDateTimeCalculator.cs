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

        DateTimeOffset GetLocalNow();

        DateTimeOffset GetLocalNow(DateTime dateTime);

        (int?, DateTimeOffset?) CalculateTimeAndDateTime(DateTimeOffset projectStart, int? input);

        (int?, DateTimeOffset?) CalculateTimeAndDateTime(DateTimeOffset projectStart, DateTimeOffset? input);

        DateTimeOffset AddDays(DateTimeOffset startDateTime, int days);

        int CountDays(DateTimeOffset current, DateTimeOffset toCompareWith);

        DateTimeOffset DisplayEarliestStartDate(DateTimeOffset projectStart, DateTimeOffset earliestStart, int duration);

        DateTimeOffset DisplayLatestStartDate(DateTimeOffset earliestStart, DateTimeOffset latestStart, int duration);

        DateTimeOffset DisplayFinishDate(DateTimeOffset start, DateTimeOffset finish, int duration);

        DateTimeOffset MaximumLatestFinishDateIn(DateTimeOffset start, DateTimeOffset maxLatestFinish, int duration);

        DateTimeOffset MaximumLatestFinishDateOut(DateTimeOffset start, DateTimeOffset maxLatestFinish, int duration);
    }
}
