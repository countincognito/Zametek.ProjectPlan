namespace Zametek.Data.ProjectPlan.v0_3_0
{
    [Serializable]
    public record ActivitySeverityModel
    {
        public int SlackLimit { get; init; }

        public double CriticalityWeight { get; init; }

        public double FibonacciWeight { get; init; }

        public v0_1_0.ColorFormatModel ColorFormat { get; init; } = new v0_1_0.ColorFormatModel();
    }
}
