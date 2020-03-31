using System;
using System.Collections.Generic;

namespace nGantt.PeriodSplitter
{
    public class PeriodHourSplitter : PeriodSplitter
    {
        public PeriodHourSplitter(DateTime min, DateTime max)
            : base(min, max)
        {

        }

        public override List<Period> Split()
        {
            var precedingBreak = new DateTime(MinDate.Year, MinDate.Month, MinDate.Day, MinDate.Hour, 0, 0);
            return base.Split(precedingBreak);
        }

        protected override DateTime Increase(DateTime dateTime, int value)
        {
            return dateTime.AddHours(value);
        }
    }
}
