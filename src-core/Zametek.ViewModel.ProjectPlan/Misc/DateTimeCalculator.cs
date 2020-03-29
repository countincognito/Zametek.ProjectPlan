using FluentDateTime;
using System;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class DateTimeCalculator
        : IDateTimeCalculator
    {
        #region Ctors

        public DateTimeCalculator()
        {
            Mode = DateTimeCalculatorMode.AllDays;
        }

        #endregion

        #region Private Methods

        private static DateTime AddAllDays(
            DateTime current,
            int days)
        {
            return current.AddDays(days);
        }

        private static DateTime AddBusinessDays(
            DateTime current,
            int days)
        {
            return current.AddBusinessDays(days);
        }

        private static int CountAllDays(
            DateTime current,
            DateTime toCompareWith)
        {
            if (current.IsAfter(toCompareWith))
            {
                return -CountAllDays(toCompareWith, current);
            }
            return Convert.ToInt32((toCompareWith - current).TotalDays);
        }

        private static int CountBusinessDays(
            DateTime current,
            DateTime toCompareWith)
        {
            if (current.IsAfter(toCompareWith))
            {
                return -CountBusinessDays(toCompareWith, current);
            }
            int count = 0;
            while (current.IsBefore(toCompareWith))
            {
                current = current.AddDays(1);
                if (current.DayOfWeek != DayOfWeek.Saturday
                    && current.DayOfWeek != DayOfWeek.Sunday)
                {
                    count++;
                }
            }
            return count;
        }

        #endregion

        #region IDateTimeCalculator Members

        public DateTimeCalculatorMode Mode
        {
            get;
            private set;
        }

        public int DaysPerWeek
        {
            get
            {
                int daysPerWeek;
                switch (Mode)
                {
                    case DateTimeCalculatorMode.AllDays:
                        daysPerWeek = 7;
                        break;
                    case DateTimeCalculatorMode.BusinessDays:
                        daysPerWeek = 5;
                        break;
                    default:
                        throw new InvalidOperationException($@"Unknown DateTimeCalculatorMode value ""{Mode}""");
                }
                return daysPerWeek;
            }
        }

        public void UseBusinessDays(bool useBusinessDays)
        {
            Mode = useBusinessDays ? DateTimeCalculatorMode.BusinessDays : DateTimeCalculatorMode.AllDays;
        }

        public DateTime AddDays(
            DateTime startDateTime,
            int days)
        {
            DateTime finishDateTime;
            switch (Mode)
            {
                case DateTimeCalculatorMode.AllDays:
                    finishDateTime = AddAllDays(startDateTime, days);
                    break;
                case DateTimeCalculatorMode.BusinessDays:
                    finishDateTime = AddBusinessDays(startDateTime, days);
                    break;
                default:
                    throw new InvalidOperationException($@"Unknown DateTimeCalculatorMode value ""{Mode}""");
            }
            return finishDateTime;
        }

        public int CountDays(
            DateTime current,
            DateTime toCompareWith)
        {
            int count;
            switch (Mode)
            {
                case DateTimeCalculatorMode.AllDays:
                    count = CountAllDays(current, toCompareWith);
                    break;
                case DateTimeCalculatorMode.BusinessDays:
                    count = CountBusinessDays(current, toCompareWith);
                    break;
                default:
                    throw new InvalidOperationException($@"Unknown DateTimeCalculatorMode value ""{Mode}""");
            }
            return count;
        }

        #endregion
    }
}
