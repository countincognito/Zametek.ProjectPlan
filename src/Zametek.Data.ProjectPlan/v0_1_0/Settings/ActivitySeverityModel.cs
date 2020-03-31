using System;

namespace Zametek.Data.ProjectPlan.v0_1_0
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
