namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record MetricsModel
    {
        public double? Criticality { get; init; }

        public double? Fibonacci { get; init; }

        public double? Activity { get; init; }

        public double? ActivityStdDevCorrection { get; init; }

        public double? GeometricCriticality { get; init; }

        public double? GeometricFibonacci { get; init; }

        public double? GeometricActivity { get; init; }
    }
}
