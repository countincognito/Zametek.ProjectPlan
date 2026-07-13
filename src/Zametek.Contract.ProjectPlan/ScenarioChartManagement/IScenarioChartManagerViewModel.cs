using System.Windows.Input;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IScenarioChartManagerViewModel
        : IKillSubscriptions, IDisposable
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        bool ShowNames { get; set; }

        TrackedMetrics TrackedMetricXAxis { get; set; }

        TrackedMetrics TrackedMetricY1Axis { get; set; }

        TrackedMetrics TrackedMetricY2Axis { get; set; }

        CurveFittingType CurveFittingTypeY1 { get; set; }

        CurveFittingType CurveFittingTypeY2 { get; set; }

        string CurveFittingFormulaY1 { get; }

        string CurveFittingFormulaY2 { get; }

        ICommand SaveScenarioChartImageFileCommand { get; }

        ICommand ResetScenarioChartCommand { get; }

        ICommand ChangeTrackedMetricXAxisCommand { get; }

        ICommand ChangeTrackedMetricY1AxisCommand { get; }

        ICommand ChangeTrackedMetricY2AxisCommand { get; }

        ICommand ChangeCurveFittingTypeY1Command { get; }

        ICommand ChangeCurveFittingTypeY2Command { get; }

        Task SaveScenarioChartImageFileAsync(string? filename, int width, int height);

        void BuildScenarioChartPlotModel();
    }
}
