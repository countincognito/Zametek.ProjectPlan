using System.Windows.Input;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IScenarioChartManagerViewModel
        : IKillSubscriptions
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        bool ShowNames { get; set; }

        TrackedMetrics TrackedMetricXAxis { get; set; }

        TrackedMetrics TrackedMetricYAxis { get; set; }

        CurveFittingType CurveFittingType { get; set; }

        string CurveFittingFormula { get; }

        ICommand SaveScenarioChartImageFileCommand { get; }

        ICommand ResetScenarioChartCommand { get; }

        ICommand ChangeTrackedMetricXAxisCommand { get; }

        ICommand ChangeTrackedMetricYAxisCommand { get; }

        ICommand ChangeCurveFittingTypeCommand { get; }

        Task SaveScenarioChartImageFileAsync(string? filename, int width, int height);

        void BuildScenarioChartPlotModel();
    }
}
