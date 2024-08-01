namespace Zametek.Contract.ProjectPlan
{
    public interface IMetricManagerViewModel
        : IKillSubscriptions
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        double? CriticalityRisk { get; }

        double? FibonacciRisk { get; }

        double? ActivityRisk { get; }

        double? ActivityRiskWithStdDevCorrection { get; }

        double? GeometricCriticalityRisk { get; }

        double? GeometricFibonacciRisk { get; }

        double? GeometricActivityRisk { get; }

        int? CyclomaticComplexity { get; }

        int? Duration { get; }

        double? DurationManMonths { get; }

        string ProjectFinish { get; }

        double? DirectCost { get; }

        double? IndirectCost { get; }

        double? OtherCost { get; }

        double? TotalCost { get; }

        double? Efficiency { get; }

        void BuildMetrics();

        void BuildCosts();
    }
}
