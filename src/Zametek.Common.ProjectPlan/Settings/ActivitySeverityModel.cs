namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ActivitySeverityModel
    {
        public int SlackLimit { get; init; }

        public double CriticalityWeight { get; init; }

        public double FibonacciWeight { get; init; }

        public ColorFormatModel ColorFormat { get; init; } = new ColorFormatModel();
    }
}
