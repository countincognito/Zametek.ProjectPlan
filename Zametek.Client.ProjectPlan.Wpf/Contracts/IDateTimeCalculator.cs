using System;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public interface IDateTimeCalculator
    {
        DateTimeCalculatorMode Mode
        {
            get;
        }

        int DaysPerWeek
        {
            get;
        }

        void UseBusinessDays(bool useBusinessDays);
        DateTime AddDays(DateTime startDateTime, int days);
        int CountDays(DateTime current, DateTime toCompareWith);
    }
}
