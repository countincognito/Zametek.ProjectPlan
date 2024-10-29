using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IDateTimeCalculator
    {
        DateTimeCalculatorMode CalculatorMode { get; set; }

        DateTimeDisplayMode DisplayMode { get; set; }

        int DaysPerWeek { get; }

        DateTimeOffset AddDays(DateTimeOffset startDateTime, int days);

        int CountDays(DateTimeOffset current, DateTimeOffset toCompareWith);

        DateTimeOffset DisplayFinishDate(DateTimeOffset start, DateTimeOffset finish, int duration);
    }
}
