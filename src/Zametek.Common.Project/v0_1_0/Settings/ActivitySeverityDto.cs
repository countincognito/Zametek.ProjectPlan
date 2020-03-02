using System;

namespace Zametek.Common.Project.v0_1_0
{
    [Serializable]
    public class ActivitySeverityDto
    {
        public int SlackLimit { get; set; }
        public double CriticalityWeight { get; set; }
        public double FibonacciWeight { get; set; }
        public ColorFormatDto ColorFormat { get; set; }
    }
}
