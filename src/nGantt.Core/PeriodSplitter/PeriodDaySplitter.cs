using System;
using System.Collections.Generic;

namespace nGantt.PeriodSplitter
{
    public class PeriodDaySplitter : PeriodSplitter
    {
        public PeriodDaySplitter(DateTime min, DateTime max)
            : base(min, max)
        { }

        public override List<Period> Split()
        {
            var precedingBreak = min.Date;
            return base.Split(precedingBreak);
        }

        protected override DateTime Increase(DateTime date, int value)
        {
            return date.AddDays(value);
        }
    }
}
