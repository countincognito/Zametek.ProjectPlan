
namespace Zametek.Client.ProjectPlan.Wpf
{
    public interface IMetricsManagerViewModel
    {
        double? CriticalityRisk
        {
            get;
        }

        double? FibonacciRisk
        {
            get;
        }

        double? ActivityRisk
        {
            get;
        }

        double? ActivityRiskWithStdDevCorrection
        {
            get;
        }

        double? GeometricCriticalityRisk
        {
            get;
        }

        double? GeometricFibonacciRisk
        {
            get;
        }

        double? GeometricActivityRisk
        {
            get;
        }

        int? CyclomaticComplexity
        {
            get;
        }

        double? DurationManMonths
        {
            get;
        }
    }
}
