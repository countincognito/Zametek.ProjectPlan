using System;
using System.Collections.Generic;

namespace nGantt.PeriodSplitter
{
    public abstract class PeriodSplitter
    {
        private readonly List<Period> result = new List<Period>();
        protected DateTime min;
        protected DateTime max;

        protected PeriodSplitter(DateTime min, DateTime max)
        {
            this.min = min;
            this.max = max;
        }

        public DateTime MinDate { get { return min; } }
        public DateTime MaxDate { get { return max; } }

        public abstract List<Period> Split();

        protected abstract DateTime Increase(DateTime date, int value);

        protected List<Period> Split(DateTime offsetDate)
        {
            var firstPeriod = new Period() { Start = min, End = Increase(offsetDate, 1) };
            result.Add(firstPeriod);

            if (firstPeriod.End >= max)
            {
                firstPeriod.End = max;
                return result;
            }

            int i = 1;
            while (Increase(offsetDate, i) < max)
            {
                var period = new Period() { Start = Increase(offsetDate, i), End = Increase(offsetDate, i + 1) };
                if (period.End >= max)
                    period.End = max;

                result.Add(period);
                i++;
            }

            return result;
        }
    }
}
