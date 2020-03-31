using System;
using System.Collections.Generic;

namespace nGantt.PeriodSplitter
{
    public abstract class PeriodSplitter
    {
        private readonly List<Period> result = new List<Period>();

        protected PeriodSplitter(DateTime min, DateTime max)
        {
            MinDate = min;
            MaxDate = max;
        }

        public DateTime MinDate { get; }
        public DateTime MaxDate { get; }

        public abstract List<Period> Split();

        protected abstract DateTime Increase(DateTime dateTime, int value);

        protected List<Period> Split(DateTime offsetDate)
        {
            var firstPeriod = new Period() { Start = MinDate, End = Increase(offsetDate, 1) };
            result.Add(firstPeriod);

            if (firstPeriod.End >= MaxDate)
            {
                firstPeriod.End = MaxDate;
                return result;
            }

            int i = 1;
            while (Increase(offsetDate, i) < MaxDate)
            {
                var period = new Period() { Start = Increase(offsetDate, i), End = Increase(offsetDate, i + 1) };
                if (period.End >= MaxDate)
                    period.End = MaxDate;

                result.Add(period);
                i++;
            }

            return result;
        }
    }
}
