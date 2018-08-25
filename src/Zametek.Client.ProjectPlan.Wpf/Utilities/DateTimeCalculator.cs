using System;
using System.ComponentModel;
using FluentDateTime;
using Zametek.Utility;

namespace Zametek.Client.ProjectPlan.Wpf
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

        private static DateTime AddAllDays(DateTime current, int days)
        {
            return current.AddDays(days);
        }

        private static DateTime AddBusinessDays(DateTime current, int days)
        {
            return current.AddBusinessDays(days);
        }

        private static int CountAllDays(DateTime current, DateTime toCompareWith)
        {
            if (current.IsAfter(toCompareWith))
            {
                return -CountAllDays(toCompareWith, current);
            }
            return Convert.ToInt32((toCompareWith - current).TotalDays);
        }

        private static int CountBusinessDays(DateTime current, DateTime toCompareWith)
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
                int daysPerWeek = 0;
                Mode.ValueSwitchOn()
                    .Case(DateTimeCalculatorMode.AllDays, x => daysPerWeek = 7)
                    .Case(DateTimeCalculatorMode.BusinessDays, x => daysPerWeek = 5)
                    .Default(x =>
                    {
                        throw new InvalidEnumArgumentException(@"Unknown DateTimeCalculatorMode value");
                    });
                return daysPerWeek;
            }
        }

        public void UseBusinessDays(bool useBusinessDays)
        {
            Mode = useBusinessDays ? DateTimeCalculatorMode.BusinessDays : DateTimeCalculatorMode.AllDays;
        }

        public DateTime AddDays(DateTime startDateTime, int days)
        {
            DateTime finishDateTime = startDateTime;
            Mode.ValueSwitchOn()
                .Case(DateTimeCalculatorMode.AllDays, x => finishDateTime = AddAllDays(startDateTime, days))
                .Case(DateTimeCalculatorMode.BusinessDays, x => finishDateTime = AddBusinessDays(startDateTime, days))
                .Default(x =>
                {
                    throw new InvalidEnumArgumentException(@"Unknown DateTimeCalculatorMode value");
                });
            return finishDateTime;
        }

        public int CountDays(DateTime current, DateTime toCompareWith)
        {
            int count = 0;
            Mode.ValueSwitchOn()
                .Case(DateTimeCalculatorMode.AllDays, x => count = CountAllDays(current, toCompareWith))
                .Case(DateTimeCalculatorMode.BusinessDays, x => count = CountBusinessDays(current, toCompareWith))
                .Default(x =>
                {
                    throw new InvalidEnumArgumentException(@"Unknown DateTimeCalculatorMode value");
                });
            return count;
        }

        #endregion
    }
}
