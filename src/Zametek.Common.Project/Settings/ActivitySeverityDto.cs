using System;

namespace Zametek.Common.Project
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
