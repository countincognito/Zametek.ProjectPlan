using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IDateTimeCalculator
    {
        DateTimeCalculatorMode Mode { get; set; }

        int DaysPerWeek { get; }

        DateTimeOffset AddDays(DateTimeOffset startDateTime, int days);

        int CountDays(DateTimeOffset current, DateTimeOffset toCompareWith);
    }
}
