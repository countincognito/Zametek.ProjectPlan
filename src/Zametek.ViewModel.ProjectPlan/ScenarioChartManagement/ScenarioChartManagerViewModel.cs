using Avalonia;
using Avalonia.Threading;
using ReactiveUI;
using ScottPlot;
using ScottPlot.Avalonia;
using ScottPlot.Plottables;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ScenarioChartManagerViewModel
        : ToolViewModelBase, IScenarioChartManagerViewModel, IScottPlotViewModel
    {
        #region Fields

        private readonly Lock m_Lock;

        private static readonly IList<IFileFilter> s_ExportFileFilters =
            [
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_ImageJpegFileType,
                    Patterns =
                    [
                        Resource.ProjectPlan.Filters.Filter_ImageJpegFilePattern
                    ]
                },
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_ImagePngFileType,
                    Patterns =
                    [
                        Resource.ProjectPlan.Filters.Filter_ImagePngFilePattern
                    ]
                },
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_ImageBmpFileType,
                    Patterns =
                    [
                        Resource.ProjectPlan.Filters.Filter_ImageBmpFilePattern
                    ]
                },
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_ImageWebpFileType,
                    Patterns =
                    [
                        Resource.ProjectPlan.Filters.Filter_ImageWebpFilePattern
                    ]
                },
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_ImageSvgFileType,
                    Patterns =
                    [
                        Resource.ProjectPlan.Filters.Filter_ImageSvgFilePattern
                    ]
                },
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_PdfFileType,
                    Patterns =
                    [
                        Resource.ProjectPlan.Filters.Filter_PdfFilePattern
                    ]
                }
            ];

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IProjectScenarioManagerViewModel m_ProjectScenarioManagerViewModel;
        private readonly ISettingService m_SettingService;
        private readonly IDialogService m_DialogService;
        private readonly IDateTimeCalculator m_DateTimeCalculator;
        private readonly IScottPlotImageExporter m_ScottPlotImageExporter;

        // Reclaims the unmanaged Skia memory of each plot this view model replaces.
        private readonly AvaPlotRetirer m_PlotRetirer;

        private readonly IDisposable? m_BuildScenarioChartPlotModelSub;

        private const double c_AnnotatedEllipseRadius = 5.0;

        #endregion

        #region Ctors

        public ScenarioChartManagerViewModel(
            ICoreViewModel coreViewModel,
            IProjectScenarioManagerViewModel projectScenarioManagerViewModel,
            ISettingService settingService,
            IDialogService dialogService,
            IDateTimeCalculator dateTimeCalculator,
            IScottPlotImageExporter scottPlotImageExporter)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(projectScenarioManagerViewModel);
            ArgumentNullException.ThrowIfNull(settingService);
            ArgumentNullException.ThrowIfNull(dialogService);
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            ArgumentNullException.ThrowIfNull(scottPlotImageExporter);
            m_Lock = new();
            m_CoreViewModel = coreViewModel;
            m_ProjectScenarioManagerViewModel = projectScenarioManagerViewModel;
            m_SettingService = settingService;
            m_DialogService = dialogService;
            m_DateTimeCalculator = dateTimeCalculator;
            m_ScottPlotImageExporter = scottPlotImageExporter;
            m_ScenarioChartPlotModel = new AvaPlot();
            m_PlotRetirer = new AvaPlotRetirer();
            m_CurveFittingFormulaY1 = string.Empty;
            m_CurveFittingFormulaY2 = string.Empty;

            {
                ReactiveCommand<Unit, Unit> saveScenarioChartImageFileCommand = ReactiveCommand.CreateFromTask(SaveScenarioChartImageFileAsync);
                SaveScenarioChartImageFileCommand = saveScenarioChartImageFileCommand;
            }

            ResetScenarioChartCommand = ReactiveCommand.Create(ResetScenarioChart);

            ChangeTrackedMetricXAxisCommand = ReactiveCommand.CreateFromTask<TrackedMetrics>(ChangeTrackedMetricXAxisAsync);
            ChangeTrackedMetricY1AxisCommand = ReactiveCommand.CreateFromTask<TrackedMetrics>(ChangeTrackedMetricY1AxisAsync);
            ChangeTrackedMetricY2AxisCommand = ReactiveCommand.CreateFromTask<TrackedMetrics>(ChangeTrackedMetricY2AxisAsync);
            ChangeCurveFittingTypeY1Command = ReactiveCommand.CreateFromTask<CurveFittingType>(ChangeCurveFittingTypeY1Async);
            ChangeCurveFittingTypeY2Command = ReactiveCommand.CreateFromTask<CurveFittingType>(ChangeCurveFittingTypeY2Async);

            m_IsBusy = this
                .WhenAnyValue(
                    rcm => rcm.m_CoreViewModel.IsBusy,
                    rcm => rcm.m_ProjectScenarioManagerViewModel.IsBusy,
                    (a, b) => a || b)
                .ToProperty(this, rcm => rcm.IsBusy);

            m_HasStaleOutputs = this
                .WhenAnyValue(rcm => rcm.m_CoreViewModel.HasStaleOutputs)
                .ToProperty(this, rcm => rcm.HasStaleOutputs);

            m_HasCompilationErrors = this
                .WhenAnyValue(rcm => rcm.m_CoreViewModel.HasCompilationErrors)
                .ToProperty(this, rcm => rcm.HasCompilationErrors);

            m_ShowNames = this
                .WhenAnyValue(rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartShowNames)
                .ToProperty(this, agm => agm.ShowNames);

            m_TrackedMetricXAxis = this
                .WhenAnyValue(rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartTrackedMetricXAxis)
                .ToProperty(this, rcm => rcm.TrackedMetricXAxis);

            m_TrackedMetricY1Axis = this
                .WhenAnyValue(rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartTrackedMetricY1Axis)
                .ToProperty(this, rcm => rcm.TrackedMetricY1Axis);

            m_TrackedMetricY2Axis = this
                .WhenAnyValue(rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartTrackedMetricY2Axis)
                .ToProperty(this, rcm => rcm.TrackedMetricY2Axis);

            m_CurveFittingTypeY1 = this
                .WhenAnyValue(rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartCurveFittingTypeY1)
                .ToProperty(this, rcm => rcm.CurveFittingTypeY1);

            m_CurveFittingTypeY2 = this
                .WhenAnyValue(rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartCurveFittingTypeY2)
                .ToProperty(this, rcm => rcm.CurveFittingTypeY2);

            m_BuildScenarioChartPlotModelSub = this
                .WhenAnyValue(
                    rcm => rcm.m_ProjectScenarioManagerViewModel.TrackedMetricsSet,
                    rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartShowNames,
                    rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartTrackedMetricXAxis,
                    rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartTrackedMetricY1Axis,
                    rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartTrackedMetricY2Axis,
                    rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartCurveFittingTypeY1,
                    rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartCurveFittingTypeY2,
                    rcm => rcm.m_CoreViewModel.ProjectStart,
                    rcm => rcm.m_CoreViewModel.BaseTheme,
                    (_, _, _, _, _, _, _, _, _) => Unit.Default)
                .MuteWhile(this.WhenAnyValue(rcm => rcm.m_CoreViewModel.IsBulkUpdating)) // Conflate redundant notifications while a project scenario is loaded/reset.
                .ObserveOn(RxSchedulers.TaskpoolScheduler)
                .Subscribe(async _ => await BuildScenarioChartPlotModelAsync());

            Id = Resource.ProjectPlan.Titles.Title_ScenarioChartView;
            Title = Resource.ProjectPlan.Titles.Title_ScenarioChartView;
        }

        #endregion

        #region Properties

        private AvaPlot m_ScenarioChartPlotModel;
        public AvaPlot ScenarioChartPlotModel
        {
            get
            {
                return m_ScenarioChartPlotModel;
            }
            private set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_ScenarioChartPlotModel, value);
                }
            }
        }

        public object? ImageBounds { get; set; }

        #endregion

        #region Private Methods

        private async Task BuildScenarioChartPlotModelAsync()
        {
            try
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    lock (m_Lock)
                    {
                        BuildScenarioChartPlotModel();
                    }
                });
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private static (AvaPlot, string, string) BuildScenarioChartPlotModelInternal(
            TrackedMetricsSetModel trackedMetricsSet,
            bool showNames,
            TrackedMetrics xMetric,
            TrackedMetrics y1Metric,
            TrackedMetrics y2Metric,
            CurveFittingType curveFittingTypeY1,
            CurveFittingType curveFittingTypeY2,
            BaseTheme baseTheme)
        {
            ArgumentNullException.ThrowIfNull(trackedMetricsSet);
            var plotModel = new AvaPlot();
            plotModel.Plot.HideGrid();

            // Now build the plot.

            if (trackedMetricsSet.TrackedMetrics.Count == 0
                || xMetric == TrackedMetrics.None
                || (y1Metric == TrackedMetrics.None && y2Metric == TrackedMetrics.None))
            {
                return (plotModel.SetBaseTheme(baseTheme), string.Empty, string.Empty);
            }

            // Select the metric for the X axis.
            Func<MetricsModel, double> xMetricFunction = GetMetricFunction(xMetric);

            // X Axis title.
            IXAxis xAxis = plotModel.Plot.Axes.Bottom;
            xAxis.Label.Text = StringConverters.TrackedMetricsValue(xMetric);
            xAxis.Label.FontSize = PlotHelper.FontSize;
            xAxis.Label.Bold = false;

            // Each Y metric keeps a fixed side (Y1 left, Y2 right, each with
            // its own scale) so the chart stays predictable when only one of
            // them is active.

            string curveFittingFormulaY1 = string.Empty;
            string curveFittingFormulaY2 = string.Empty;

            if (y1Metric != TrackedMetrics.None)
            {
                curveFittingFormulaY1 = BuildTrackedMetricSeries(
                    plotModel,
                    trackedMetricsSet,
                    showNames,
                    xMetricFunction,
                    y1Metric,
                    curveFittingTypeY1,
                    plotModel.Plot.Axes.Left,
                    Colors.Blue,
                    MarkerShape.FilledCircle);
            }

            if (y2Metric != TrackedMetrics.None)
            {
                curveFittingFormulaY2 = BuildTrackedMetricSeries(
                    plotModel,
                    trackedMetricsSet,
                    showNames,
                    xMetricFunction,
                    y2Metric,
                    curveFittingTypeY2,
                    plotModel.Plot.Axes.Right,
                    Colors.Red,
                    MarkerShape.FilledSquare);
            }

            plotModel.Plot.Axes.AutoScale();

            plotModel.Plot.Axes.AutoScaleExpand();

            return (plotModel.SetBaseTheme(baseTheme), curveFittingFormulaY1, curveFittingFormulaY2);
        }

        private static string BuildTrackedMetricSeries(
            AvaPlot plotModel,
            TrackedMetricsSetModel trackedMetricsSet,
            bool showNames,
            Func<MetricsModel, double> xMetricFunction,
            TrackedMetrics yMetric,
            CurveFittingType curveFittingType,
            IYAxis yAxis,
            Color markerFillColor,
            MarkerShape markerShape)
        {
            // Select the metric for the Y axis.
            Func<MetricsModel, double> yMetricFunction = GetMetricFunction(yMetric);

            // Gather the data points for the selected metrics.

            List<AnnotatedMarker> markers = [];
            List<Text> annotations = [];

            foreach (TrackedMetricsModel trackedMetrics in trackedMetricsSet.TrackedMetrics)
            {
                var marker = new AnnotatedMarker
                {
                    X = xMetricFunction(trackedMetrics.Metrics),
                    Y = yMetricFunction(trackedMetrics.Metrics),
                    Size = 14.0f,
                    LineWidth = 1.5f,
                    MarkerFillColor = markerFillColor,
                    MarkerLineColor = Colors.WhiteSmoke,
                    Shape = markerShape,
                    Annotation = trackedMetrics.Path,
                };

                marker.Axes.YAxis = yAxis;

                var annotation = new Text
                {
                    LabelText = marker.Annotation,
                    Location = new Coordinates(marker.X, marker.Y),
                    OffsetX = 10,
                    OffsetY = 10,
                    LabelPadding = 5,
                    //FontSize = 12,
                    //Color = Colors.Black,
                    //BackgroundColor = Colors.White.WithAlpha(200),
                    //BorderColor = Colors.Black,
                    //BorderWidth = 1,
                };

                annotation.Axes.YAxis = yAxis;

                markers.Add(marker);
                annotations.Add(annotation);
            }

            markers = [.. markers.OrderBy(m => m.X).ThenBy(m => m.Y)];
            plotModel.Plot.PlottableList.AddRange(markers);

            if (showNames)
            {
                annotations = [.. annotations.OrderBy(m => m.Location.X).ThenBy(m => m.Location.Y)];
                plotModel.Plot.PlottableList.AddRange(annotations);
            }

            // Y Axis title.
            yAxis.Label.Text = StringConverters.TrackedMetricsValue(yMetric);
            yAxis.Label.FontSize = PlotHelper.FontSize;
            yAxis.Label.Bold = false;

            // Build the curve fitting if requested.
            return BuildCurveFit(plotModel, markers, curveFittingType, yAxis, markerFillColor);
        }

        private static string BuildCurveFit(
            AvaPlot plotModel,
            List<AnnotatedMarker> markers,
            CurveFittingType curveFittingType,
            IYAxis yAxis,
            Color color)
        {
            string formula = string.Empty;
            double[] xs = [.. markers.Select(x => x.X)];
            double[] ys = [.. markers.Select(x => x.Y)];

            Debug.Assert(xs.Length == ys.Length);

            switch (curveFittingType)
            {
                case CurveFittingType.None:
                    break;
                case CurveFittingType.Linear:
                    {
                        if (xs.Length >= 2)
                        {
                            (double a, double b) = MathNet.Numerics.Fit.Line(xs, ys);
                            double[] fx = [.. xs.Select(x => a + b * x)];
                            double r2 = MathNet.Numerics.GoodnessOfFit.RSquared(ys, fx);
                            formula = $"y = {b:F3}x + {a:F3} (r²={r2:F3})";
                            Scatter line = plotModel.Plot.Add.ScatterLine(xs, fx);
                            line.MarkerSize = 0;
                            line.LineWidth = 2;
                            line.LinePattern = LinePattern.Dashed;
                            line.Color = color;
                            line.Axes.YAxis = yAxis;
                        }
                    }
                    break;
                case CurveFittingType.Exponential:
                    {
                        if (xs.Length >= 2)
                        {
                            (double a, double r) = MathNet.Numerics.Fit.Exponential(xs, ys);
                            double[] fx = [.. xs.Select(x => a * Math.Exp(r * x))];
                            double r2 = MathNet.Numerics.GoodnessOfFit.RSquared(ys, fx);
                            formula = $"y = {a:F3}e^{r:F3}x (r²={r2:F3})";
                            Scatter line = plotModel.Plot.Add.ScatterLine(xs, fx);
                            line.MarkerSize = 0;
                            line.LineWidth = 2;
                            line.LinePattern = LinePattern.Dashed;
                            line.Color = color;
                            line.Axes.YAxis = yAxis;
                        }
                    }
                    break;
                case CurveFittingType.Logarithmic:
                    {
                        if (xs.Length >= 2)
                        {
                            (double a, double b) = MathNet.Numerics.Fit.Logarithm(xs, ys);
                            double[] fx = [.. xs.Select(x => a + b * Math.Log(x))];
                            double r2 = MathNet.Numerics.GoodnessOfFit.RSquared(ys, fx);
                            formula = $"y = {b:F3}ln(x) + {a:F3} (r²={r2:F3})";
                            Scatter line = plotModel.Plot.Add.ScatterLine(xs, fx);
                            line.MarkerSize = 0;
                            line.LineWidth = 2;
                            line.LinePattern = LinePattern.Dashed;
                            line.Color = color;
                            line.Axes.YAxis = yAxis;
                        }
                    }
                    break;
                case CurveFittingType.Power:
                    {
                        if (xs.Length >= 2)
                        {
                            (double a, double b) = MathNet.Numerics.Fit.Power(xs, ys);
                            double f(double x) => a * Math.Pow(x, b);
                            Coordinates pt1 = new(xs.First(), f(xs.First()));
                            Coordinates pt2 = new(xs.Last(), f(xs.Last()));
                            double r2 = MathNet.Numerics.GoodnessOfFit.RSquared(ys, xs.Select(x => f(x)));
                            formula = $"y = {a:F3}x^{b:F3} (r²={r2:F3})";
                            LinePlot line = plotModel.Plot.Add.Line(pt1, pt2);
                            line.MarkerSize = 0;
                            line.LineWidth = 2;
                            line.LinePattern = LinePattern.Dashed;
                            line.Color = color;
                            line.Axes.YAxis = yAxis;
                        }
                    }
                    break;
                case CurveFittingType.PolynomialOrder2:
                    {
                        formula = BuildPolynomialCurveFit(plotModel, xs, ys, 2, yAxis, color);
                    }
                    break;
                case CurveFittingType.PolynomialOrder3:
                    {
                        formula = BuildPolynomialCurveFit(plotModel, xs, ys, 3, yAxis, color);
                    }
                    break;
                case CurveFittingType.PolynomialOrder4:
                    {
                        formula = BuildPolynomialCurveFit(plotModel, xs, ys, 4, yAxis, color);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(curveFittingType), @$"{Resource.ProjectPlan.Messages.Message_UnknownCurveFittingType} {curveFittingType}");
            }

            return formula;
        }

        private static string BuildPolynomialCurveFit(
            AvaPlot plotModel,
            double[] xs,
            double[] ys,
            int order,
            IYAxis yAxis,
            Color color)
        {
            char[] superscript = { '⁰', '¹', '²', '³', '⁴' };

            int minimumOrder = 0;
            int maximumOrder = superscript.Length - 1;

            if (xs.Length != ys.Length
                || order < minimumOrder
                || order > maximumOrder
                || xs.Length <= order)
            {
                return string.Empty;
            }

            double[] coefficients = MathNet.Numerics.Fit.Polynomial(xs, ys, order);
            double[] fx = [.. xs.Select(x => MathNet.Numerics.Polynomial.Evaluate(x, coefficients))];

            // Plot the regression line.
            Scatter line = plotModel.Plot.Add.ScatterLine(xs, fx);
            line.MarkerSize = 0;
            line.LineWidth = 2;
            line.LinePattern = LinePattern.Dashed;
            line.Color = color;
            line.Axes.YAxis = yAxis;

            // Build the formula.
            StringBuilder formula = new(@"y = ");

            for (int i = coefficients.Length - 1; i >= 0; i--)
            {
                if (i < coefficients.Length - 1)
                {
                    if (coefficients[i] < 0)
                    {
                        formula.Append(@" - ");
                    }
                    else
                    {
                        formula.Append(@" + ");
                    }
                }
                else if (coefficients[i] < 0)
                {
                    formula.Append('-');
                }

                if (i > 0)
                {
                    formula.Append($"{Math.Abs(coefficients[i]):F3}x{superscript[i]}");
                }
                else
                {
                    formula.Append($"{Math.Abs(coefficients[i]):F3}");
                }
            }

            double r2 = MathNet.Numerics.GoodnessOfFit.RSquared(ys, fx);
            formula.Append($@" (r²={r2:F3})");
            return formula.ToString();
        }

        private static Func<MetricsModel, double> GetMetricFunction(TrackedMetrics metric)
        {
            return metric switch
            {
                TrackedMetrics.None => model => 0,
                TrackedMetrics.RisksCriticality => model => model.Risks.Criticality.GetValueOrDefault(),
                TrackedMetrics.RisksFibonacci => model => model.Risks.Fibonacci.GetValueOrDefault(),
                TrackedMetrics.RisksActivity => model => model.Risks.Activity.GetValueOrDefault(),
                TrackedMetrics.RisksActivityStdDevCorrection => model => model.Risks.ActivityStdDevCorrection.GetValueOrDefault(),
                TrackedMetrics.RisksGeometricCriticality => model => model.Risks.GeometricCriticality.GetValueOrDefault(),
                TrackedMetrics.RisksGeometricFibonacci => model => model.Risks.GeometricFibonacci.GetValueOrDefault(),
                TrackedMetrics.RisksGeometricActivity => model => model.Risks.GeometricActivity.GetValueOrDefault(),
                TrackedMetrics.CostsDirect => model => model.Costs.Direct.GetValueOrDefault(),
                TrackedMetrics.CostsIndirect => model => model.Costs.Indirect.GetValueOrDefault(),
                TrackedMetrics.CostsOther => model => model.Costs.Other.GetValueOrDefault(),
                TrackedMetrics.CostsTotal => model => model.Costs.Total.GetValueOrDefault(),
                TrackedMetrics.BillingsDirect => model => model.Billings.Direct.GetValueOrDefault(),
                TrackedMetrics.BillingsIndirect => model => model.Billings.Indirect.GetValueOrDefault(),
                TrackedMetrics.BillingsOther => model => model.Billings.Other.GetValueOrDefault(),
                TrackedMetrics.BillingsTotal => model => model.Billings.Total.GetValueOrDefault(),
                TrackedMetrics.MarginsDirect => model => model.Margins.Direct.GetValueOrDefault(),
                TrackedMetrics.MarginsIndirect => model => model.Margins.Indirect.GetValueOrDefault(),
                TrackedMetrics.MarginsOther => model => model.Margins.Other.GetValueOrDefault(),
                TrackedMetrics.MarginsTotal => model => model.Margins.Total.GetValueOrDefault(),
                TrackedMetrics.MarginsDirectAbsolute => model => model.Margins.DirectAbsolute.GetValueOrDefault(),
                TrackedMetrics.MarginsIndirectAbsolute => model => model.Margins.IndirectAbsolute.GetValueOrDefault(),
                TrackedMetrics.MarginsOtherAbsolute => model => model.Margins.OtherAbsolute.GetValueOrDefault(),
                TrackedMetrics.MarginsTotalAbsolute => model => model.Margins.TotalAbsolute.GetValueOrDefault(),
                TrackedMetrics.EffortsDirect => model => model.Efforts.Direct.GetValueOrDefault(),
                TrackedMetrics.EffortsIndirect => model => model.Efforts.Indirect.GetValueOrDefault(),
                TrackedMetrics.EffortsOther => model => model.Efforts.Other.GetValueOrDefault(),
                TrackedMetrics.EffortsTotal => model => model.Efforts.Total.GetValueOrDefault(),
                TrackedMetrics.EffortsActivity => model => model.Efforts.Activity.GetValueOrDefault(),
                TrackedMetrics.EffortsEfficiency => model => model.Efforts.Efficiency.GetValueOrDefault(),
                TrackedMetrics.NetworkCyclomaticComplexity => model => model.Network.CyclomaticComplexity.GetValueOrDefault(),
                TrackedMetrics.NetworkDuration => model => model.Network.Duration.GetValueOrDefault(),
                TrackedMetrics.NetworkDurationManMonths => model => model.Network.DurationManMonths.GetValueOrDefault(),
                _ => throw new ArgumentOutOfRangeException(nameof(metric), @$"{Resource.ProjectPlan.Messages.Message_UnknownTrackedMetric} {metric}"),
            };
        }

        private void ResetScenarioChart()
        {
            ScenarioChartPlotModel.Plot.Axes.AutoScale();
        }

        private async Task ChangeTrackedMetricXAxisAsync(TrackedMetrics trackedMetric)
        {
            try
            {
                TrackedMetricXAxis = trackedMetric;
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task ChangeTrackedMetricY1AxisAsync(TrackedMetrics trackedMetric)
        {
            try
            {
                TrackedMetricY1Axis = trackedMetric;
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task ChangeTrackedMetricY2AxisAsync(TrackedMetrics trackedMetric)
        {
            try
            {
                TrackedMetricY2Axis = trackedMetric;
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task ChangeCurveFittingTypeY1Async(CurveFittingType curveFittingType)
        {
            try
            {
                CurveFittingTypeY1 = curveFittingType;
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task ChangeCurveFittingTypeY2Async(CurveFittingType curveFittingType)
        {
            try
            {
                CurveFittingTypeY2 = curveFittingType;
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task SaveScenarioChartImageFileAsync()
        {
            try
            {
                string title = m_SettingService.ProjectTitle;
                title = string.IsNullOrWhiteSpace(title) ? Resource.ProjectPlan.Titles.Title_UntitledProject : title;
                string scenarioOutputFile = $@"{title}{Resource.ProjectPlan.Suffixes.Suffix_ScenarioChart}";
                string directory = m_SettingService.ProjectDirectory;
                string? filename = await m_DialogService.ShowSaveFileDialogAsync(scenarioOutputFile, directory, s_ExportFileFilters);

                if (!string.IsNullOrWhiteSpace(filename)
                    && ImageBounds is Rect bounds)
                {
                    int boundedWidth = Math.Abs(Convert.ToInt32(bounds.Width));
                    int boundedHeight = Math.Abs(Convert.ToInt32(bounds.Height));

                    await SaveScenarioChartImageFileAsync(filename, boundedWidth, boundedHeight);
                }
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        #endregion

        #region IScenarioChartManagerViewModel Members

        private readonly ObservableAsPropertyHelper<bool> m_IsBusy;
        public bool IsBusy => m_IsBusy.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasStaleOutputs;
        public bool HasStaleOutputs => m_HasStaleOutputs.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasCompilationErrors;
        public bool HasCompilationErrors => m_HasCompilationErrors.Value;

        private readonly ObservableAsPropertyHelper<bool> m_ShowNames;
        public bool ShowNames
        {
            get => m_ShowNames.Value;
            set
            {
                lock (m_Lock) m_ProjectScenarioManagerViewModel.ScenarioChartShowNames = value;
            }
        }

        private readonly ObservableAsPropertyHelper<TrackedMetrics> m_TrackedMetricXAxis;
        public TrackedMetrics TrackedMetricXAxis
        {
            get => m_TrackedMetricXAxis.Value;
            set
            {
                lock (m_Lock) m_ProjectScenarioManagerViewModel.ScenarioChartTrackedMetricXAxis = value;
            }
        }

        private readonly ObservableAsPropertyHelper<TrackedMetrics> m_TrackedMetricY1Axis;
        public TrackedMetrics TrackedMetricY1Axis
        {
            get => m_TrackedMetricY1Axis.Value;
            set
            {
                lock (m_Lock) m_ProjectScenarioManagerViewModel.ScenarioChartTrackedMetricY1Axis = value;
            }
        }

        private readonly ObservableAsPropertyHelper<TrackedMetrics> m_TrackedMetricY2Axis;
        public TrackedMetrics TrackedMetricY2Axis
        {
            get => m_TrackedMetricY2Axis.Value;
            set
            {
                lock (m_Lock) m_ProjectScenarioManagerViewModel.ScenarioChartTrackedMetricY2Axis = value;
            }
        }

        private readonly ObservableAsPropertyHelper<CurveFittingType> m_CurveFittingTypeY1;
        public CurveFittingType CurveFittingTypeY1
        {
            get => m_CurveFittingTypeY1.Value;
            set
            {
                lock (m_Lock) m_ProjectScenarioManagerViewModel.ScenarioChartCurveFittingTypeY1 = value;
            }
        }

        private readonly ObservableAsPropertyHelper<CurveFittingType> m_CurveFittingTypeY2;
        public CurveFittingType CurveFittingTypeY2
        {
            get => m_CurveFittingTypeY2.Value;
            set
            {
                lock (m_Lock) m_ProjectScenarioManagerViewModel.ScenarioChartCurveFittingTypeY2 = value;
            }
        }

        public ICommand SaveScenarioChartImageFileCommand { get; }

        public ICommand ResetScenarioChartCommand { get; }

        public ICommand ChangeTrackedMetricXAxisCommand { get; }

        public ICommand ChangeTrackedMetricY1AxisCommand { get; }

        public ICommand ChangeTrackedMetricY2AxisCommand { get; }

        public ICommand ChangeCurveFittingTypeY1Command { get; }

        public ICommand ChangeCurveFittingTypeY2Command { get; }

        public async Task SaveScenarioChartImageFileAsync(
            string? filename,
            int width,
            int height)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    Resource.ProjectPlan.Messages.Message_EmptyFilename);
            }
            else
            {
                try
                {
                    await m_ScottPlotImageExporter.SavePlotImageAsync(ScenarioChartPlotModel.Plot, filename, width, height);
                }
                catch (Exception ex)
                {
                    await m_DialogService.ShowErrorAsync(
                        Resource.ProjectPlan.Titles.Title_Error,
                        string.Empty,
                        ex.Message);
                }
            }
        }

        private string m_CurveFittingFormulaY1;
        public string CurveFittingFormulaY1
        {
            get => string.IsNullOrWhiteSpace(m_CurveFittingFormulaY1) ? string.Empty : m_CurveFittingFormulaY1;
            private set
            {
                lock (m_Lock)
                {
                    m_CurveFittingFormulaY1 = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        private string m_CurveFittingFormulaY2;
        public string CurveFittingFormulaY2
        {
            get => string.IsNullOrWhiteSpace(m_CurveFittingFormulaY2) ? string.Empty : m_CurveFittingFormulaY2;
            private set
            {
                lock (m_Lock)
                {
                    m_CurveFittingFormulaY2 = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public void BuildScenarioChartPlotModel()
        {
            CascadeDiagnostics.RecordBuild($@"{nameof(ScenarioChartManagerViewModel)}.{nameof(BuildScenarioChartPlotModel)}");
            AvaPlot? plotModel = null;
            string curveFittingFormulaY1 = string.Empty;
            string curveFittingFormulaY2 = string.Empty;

            lock (m_Lock)
            {
                if (!HasCompilationErrors)
                {
                    (plotModel, curveFittingFormulaY1, curveFittingFormulaY2) = BuildScenarioChartPlotModelInternal(
                        m_ProjectScenarioManagerViewModel.TrackedMetricsSet,
                        m_ProjectScenarioManagerViewModel.ScenarioChartShowNames,
                        m_ProjectScenarioManagerViewModel.ScenarioChartTrackedMetricXAxis,
                        m_ProjectScenarioManagerViewModel.ScenarioChartTrackedMetricY1Axis,
                        m_ProjectScenarioManagerViewModel.ScenarioChartTrackedMetricY2Axis,
                        m_ProjectScenarioManagerViewModel.ScenarioChartCurveFittingTypeY1,
                        m_ProjectScenarioManagerViewModel.ScenarioChartCurveFittingTypeY2,
                        m_CoreViewModel.BaseTheme);
                }
            }

            plotModel ??= new AvaPlot();
            plotModel.ClearContextMenu();
            AvaPlot outgoing = ScenarioChartPlotModel;
            ScenarioChartPlotModel = plotModel;
            m_PlotRetirer.Retire(outgoing);
            CurveFittingFormulaY1 = curveFittingFormulaY1;
            CurveFittingFormulaY2 = curveFittingFormulaY2;
        }

        #endregion

        #region IScottPlotViewModel Members

        public async Task<byte[]?> RenderChartImageAsync()
        {
            if (ImageBounds is not Rect bounds)
            {
                return null;
            }

            int width = Math.Abs(Convert.ToInt32(bounds.Width));
            int height = Math.Abs(Convert.ToInt32(bounds.Height));

            if (width <= 0 || height <= 0)
            {
                return null;
            }

            return await m_ScottPlotImageExporter.RenderPlotImageAsync(ScenarioChartPlotModel.Plot, width, height);
        }

        public Task ReportErrorAsync(string message)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
            return m_DialogService.ShowErrorAsync(
                Resource.ProjectPlan.Titles.Title_Error,
                string.Empty,
                message);
        }

        #endregion

        #region IKillSubscriptions Members

        public void KillSubscriptions()
        {
            m_BuildScenarioChartPlotModelSub?.Dispose();
        }

        #endregion

        #region IDisposable Members

        private bool m_Disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (m_Disposed)
            {
                return;
            }

            if (disposing)
            {
                KillSubscriptions();
                m_PlotRetirer.Dispose();
                m_ScenarioChartPlotModel.Plot.Dispose();
                m_IsBusy?.Dispose();
                m_HasStaleOutputs?.Dispose();
                m_HasCompilationErrors?.Dispose();
                m_ShowNames?.Dispose();
                m_TrackedMetricXAxis?.Dispose();
                m_TrackedMetricY1Axis?.Dispose();
                m_TrackedMetricY2Axis?.Dispose();
                m_CurveFittingTypeY1?.Dispose();
                m_CurveFittingTypeY2?.Dispose();
            }

            m_Disposed = true;
        }

        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
