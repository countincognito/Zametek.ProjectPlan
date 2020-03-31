using System;
using System.Collections.Generic;

namespace nGantt.PeriodSplitter
{
    public class PeriodMonthSplitter : PeriodSplitter
    {
        public PeriodMonthSplitter(DateTime min, DateTime max)
            : base(min, max)
        { }

        public override List<Period> Split()
        {
            var precedingBreak = new DateTime(MinDate.Year, MinDate.Month, 1);
            return base.Split(precedingBreak);
        }

        protected override DateTime Increase(DateTime dateTime, int value)
        {
            return dateTime.AddMonths(value);
        }
    }
}
