namespace Zametek.Contract.ProjectPlan
{
    public interface IMetricsManagerViewModel
    {
        bool IsBusy { get; }

        bool HasCompilationErrors { get; }

        bool HasStaleOutputs { get; }

        double? CriticalityRisk { get; }

        double? FibonacciRisk { get; }

        double? ActivityRisk { get; }

        double? ActivityRiskWithStdDevCorrection { get; }

        double? GeometricCriticalityRisk { get; }

        double? GeometricFibonacciRisk { get; }

        double? GeometricActivityRisk { get; }

        int? CyclomaticComplexity { get; }

        double? DurationManMonths { get; }

        double? DirectCost { get; }

        double? IndirectCost { get; }

        double? OtherCost { get; }

        double? TotalCost { get; }
    }
}
