using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IDateTimeCalculator
    {
        DateTimeCalculatorMode CalculatorMode { get; set; }

        DateTimeDisplayMode DisplayMode { get; set; }

        DateTimeOffset ProjectStart { get; set; }

        DateTimeOffset NonWorkingDaysFinish { get; }

        int DaysPerWeek { get; }

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
