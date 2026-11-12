using System.Windows.Input;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IScenarioChartManagerViewModel
        : IStartSubscriptions, IKillSubscriptions, IDisposable
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        TrackedMetrics TrackedMetricXAxis { get; set; }

        TrackedMetrics TrackedMetricY1Axis { get; set; }

        TrackedMetrics TrackedMetricY2Axis { get; set; }

        CurveFittingType CurveFittingTypeY1 { get; set; }

        CurveFittingType CurveFittingTypeY2 { get; set; }

        bool ShowNamesY1 { get; set; }

        bool ShowNamesY2 { get; set; }

        bool ShowDerivativeY1 { get; set; }

        bool ShowDerivativeY2 { get; set; }

        bool AbsoluteCurveFittingY1 { get; set; }

        bool AbsoluteCurveFittingY2 { get; set; }

        bool HasCurveFittingY1 { get; }

        bool HasCurveFittingY2 { get; }

        string CurveFittingFormulaY1 { get; }

        string CurveFittingFormulaY2 { get; }

        ICommand SaveScenarioChartImageFileCommand { get; }

        // Saves the data of the chart as two CSV files: the points and the fits.
        ICommand ExportCsvFilesCommand { get; }

        ICommand ResetScenarioChartCommand { get; }

        ICommand ChangeTrackedMetricXAxisCommand { get; }

        ICommand ChangeTrackedMetricY1AxisCommand { get; }

        ICommand ChangeTrackedMetricY2AxisCommand { get; }

        ICommand ChangeCurveFittingTypeY1Command { get; }

        ICommand ChangeCurveFittingTypeY2Command { get; }

        // Writes the chart to the stream at the given size. The caller owns the stream, which is left open.
        Task WriteScenarioChartImageAsync(Stream stream, ChartImageFormat format, int width, int height);

        void BuildScenarioChartPlotModel();

        // The data of the chart from the settings of the chart that the plan keeps: the points of each axis of Y and its fit.
        // Null when the chart is empty: the scenario has compilation errors, or the chart has no metric for the axis of X, or for
        // both axes of Y. A job builds the tracked metrics first, as for the chart.
        ScenarioChartDataModel? BuildScenarioChartData();
    }
}
