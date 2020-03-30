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
            var precedingBreak = new DateTime(min.Year, min.Month, 1);
            return base.Split(precedingBreak);
        }

        protected override DateTime Increase(DateTime date, int value)
        {
            return date.AddMonths(value);
        }
    }
}
