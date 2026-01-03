namespace Zametek.Data.ProjectPlan.v0_5_0
{
    [Serializable]
    public record RisksModel
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
