using System;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class ActivitySeverityModel
    {
        public int SlackLimit { get; set; }

        public double CriticalityWeight { get; set; }

        public double FibonacciWeight { get; set; }

        public ColorFormatModel ColorFormat { get; set; }
    }
}
