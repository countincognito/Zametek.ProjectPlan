using Avalonia;
using ReactiveUI;
using ScottPlot;
using ScottPlot.Plottables;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using RxVoid = ReactiveUI.Primitives.RxVoid;

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
        private readonly PlotRetirer m_PlotRetirer;

        private readonly IDisposable? m_BuildScenarioChartPlotModelSub;

        private const double c_AnnotatedEllipseRadius = 5.0;

        private static readonly char[] s_PolynomialSuperscripts = ['⁰', '¹', '²', '³', '⁴'];

        // The raw scenario xs can be sparse, so fitted curves and their
        // derivatives are sampled over at least this many evenly spaced
        // points instead (a curve would otherwise render as a few straight
        // segments, and an absolute fit or derivative folds at its zero
        // crossings, which sparse sampling would cut off).
        private const int c_MinimumSamplePoints = 100;

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
            m_ScenarioChartPlotModel = new Plot();
            m_PlotRetirer = new PlotRetirer();
            m_CurveFittingFormulaY1 = string.Empty;
            m_CurveFittingFormulaY2 = string.Empty;

            {
                ReactiveCommand<RxVoid, RxVoid> saveScenarioChartImageFileCommand = ReactiveCommand.CreateFromTask(SaveScenarioChartImageFileAsync);
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

            m_ShowDerivativeY1 = this
                .WhenAnyValue(rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartShowDerivativeY1)
                .ToProperty(this, rcm => rcm.ShowDerivativeY1);

            m_ShowDerivativeY2 = this
                .WhenAnyValue(rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartShowDerivativeY2)
                .ToProperty(this, rcm => rcm.ShowDerivativeY2);

            m_AbsoluteCurveFittingY1 = this
                .WhenAnyValue(rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartAbsoluteCurveFittingY1)
                .ToProperty(this, rcm => rcm.AbsoluteCurveFittingY1);

            m_AbsoluteCurveFittingY2 = this
                .WhenAnyValue(rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartAbsoluteCurveFittingY2)
                .ToProperty(this, rcm => rcm.AbsoluteCurveFittingY2);

            // The derivative toggles only make sense while their metric has a
            // curve fitting selected.
            m_HasCurveFittingY1 = this
                .WhenAnyValue(
                    rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartCurveFittingTypeY1,
                    fittingType => fittingType != CurveFittingType.None)
                .ToProperty(this, rcm => rcm.HasCurveFittingY1);

            m_HasCurveFittingY2 = this
                .WhenAnyValue(
                    rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartCurveFittingTypeY2,
                    fittingType => fittingType != CurveFittingType.None)
                .ToProperty(this, rcm => rcm.HasCurveFittingY2);

            // Split across two streams because WhenAnyValue cannot handle
            // this many individual inputs.
            m_BuildScenarioChartPlotModelSub = Observable.Merge(
                    this.WhenAnyValue(
                        rcm => rcm.m_ProjectScenarioManagerViewModel.TrackedMetricsSet,
                        rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartShowNames,
                        rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartTrackedMetricXAxis,
                        rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartTrackedMetricY1Axis,
                        rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartTrackedMetricY2Axis,
                        rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartCurveFittingTypeY1,
                        rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartCurveFittingTypeY2,
                        rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartShowDerivativeY1,
                        rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartShowDerivativeY2,
                        rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartAbsoluteCurveFittingY1,
                        rcm => rcm.m_CoreViewModel.ProjectStart,
                        rcm => rcm.m_CoreViewModel.BaseTheme,
                        (_, _, _, _, _, _, _, _, _, _, _, _) => Unit.Default),
                    this.WhenAnyValue(rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartAbsoluteCurveFittingY2)
                        .Select(_ => Unit.Default))
                .MuteWhile(this.WhenAnyValue(rcm => rcm.m_CoreViewModel.IsBulkUpdating)) // Conflate redundant notifications while a project scenario is loaded/reset.
                .ObserveOn(RxSchedulers.TaskpoolScheduler)
                .Subscribe(async _ => await BuildScenarioChartPlotModelAsync());

            Id = Resource.ProjectPlan.Titles.Title_ScenarioChartView;
            Title = Resource.ProjectPlan.Titles.Title_ScenarioChartView;
        }

        #endregion

        #region Properties

        private Plot m_ScenarioChartPlotModel;
        public Plot ScenarioChartPlotModel
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
                // Plot models are plain ScottPlot objects with no UI-thread
                // affinity, so build on the subscription's taskpool thread; the
                // property raise marshals to the UI through the binding, and
                // PlotRetirer keeps the outgoing plot alive until the UI has
                // moved on.
                lock (m_Lock)
                {
                    BuildScenarioChartPlotModel();
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

        private static (Plot, string, string) BuildScenarioChartPlotModelInternal(
            TrackedMetricsSetModel trackedMetricsSet,
            bool showNames,
            TrackedMetrics xMetric,
            TrackedMetrics y1Metric,
            TrackedMetrics y2Metric,
            CurveFittingType curveFittingTypeY1,
            CurveFittingType curveFittingTypeY2,
            bool showDerivativeY1,
            bool showDerivativeY2,
            bool absoluteCurveFittingY1,
            bool absoluteCurveFittingY2,
            BaseTheme baseTheme)
        {
            ArgumentNullException.ThrowIfNull(trackedMetricsSet);
            var plotModel = new Plot();
            plotModel.HideGrid();

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
            IXAxis xAxis = plotModel.Axes.Bottom;
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
                    xMetric,
                    y1Metric,
                    curveFittingTypeY1,
                    showDerivativeY1,
                    absoluteCurveFittingY1,
                    plotModel.Axes.Left,
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
                    xMetric,
                    y2Metric,
                    curveFittingTypeY2,
                    showDerivativeY2,
                    absoluteCurveFittingY2,
                    plotModel.Axes.Right,
                    Colors.Red,
                    MarkerShape.FilledSquare);
            }

            plotModel.Axes.AutoScale();

            plotModel.Axes.AutoScaleExpand();

            return (plotModel.SetBaseTheme(baseTheme), curveFittingFormulaY1, curveFittingFormulaY2);
        }

        private static string BuildTrackedMetricSeries(
            Plot plotModel,
            TrackedMetricsSetModel trackedMetricsSet,
            bool showNames,
            Func<MetricsModel, double> xMetricFunction,
            TrackedMetrics xMetric,
            TrackedMetrics yMetric,
            CurveFittingType curveFittingType,
            bool showDerivative,
            bool absoluteCurveFitting,
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

            // Y Axis title.
            yAxis.Label.Text = StringConverters.TrackedMetricsValue(yMetric);
            yAxis.Label.FontSize = PlotHelper.FontSize;
            yAxis.Label.Bold = false;

            if (showDerivative
                && curveFittingType != CurveFittingType.None)
            {
                string derivativeFormula = BuildCurveFitDerivative(plotModel, markers, curveFittingType, absoluteCurveFitting, yAxis, markerFillColor);

                if (!string.IsNullOrEmpty(derivativeFormula))
                {
                    // The raw scenario points belong to the fitted curve, not
                    // to its derivative, so they are not drawn alongside it.
                    string derivativeLabel = $@"d({StringConverters.TrackedMetricsValue(yMetric)}) / d({StringConverters.TrackedMetricsValue(xMetric)})";
                    yAxis.Label.Text = absoluteCurveFitting ? $@"|{derivativeLabel}|" : derivativeLabel;
                    return derivativeFormula;
                }

                // The fit could not be produced (e.g. too few scenarios), so
                // fall back to the raw display rather than an empty chart.
            }

            plotModel.PlottableList.AddRange(markers);

            if (showNames)
            {
                annotations = [.. annotations.OrderBy(m => m.Location.X).ThenBy(m => m.Location.Y)];
                plotModel.PlottableList.AddRange(annotations);
            }

            // Build the curve fitting if requested.
            return BuildCurveFit(plotModel, markers, curveFittingType, absoluteCurveFitting, yAxis, markerFillColor);
        }

        private static string BuildCurveFit(
            Plot plotModel,
            List<AnnotatedMarker> markers,
            CurveFittingType curveFittingType,
            bool absoluteCurveFitting,
            IYAxis yAxis,
            Color color)
        {
            string formula = string.Empty;
            double[] xs = [.. markers.Select(x => x.X)];
            double[] ys = [.. markers.Select(x => x.Y)];

            Debug.Assert(xs.Length == ys.Length);

            // The fits are always computed against the raw data (as are the
            // r² values); the plotted curves are then sampled over an even
            // grid, and the absolute option only passes those sampled outputs
            // through abs() before they are added to the chart.

            switch (curveFittingType)
            {
                case CurveFittingType.None:
                    break;
                case CurveFittingType.Linear:
                    {
                        if (xs.Length >= 2)
                        {
                            (double a, double b) = MathNet.Numerics.Fit.Line(xs, ys);
                            double r2 = MathNet.Numerics.GoodnessOfFit.RSquared(ys, xs.Select(x => a + b * x));
                            string expression = $"{b:F4}x + {a:F4}";
                            double[] sampleXs = BuildSampleXs(xs);
                            double[] fx = [.. sampleXs.Select(x => a + b * x)];

                            if (absoluteCurveFitting)
                            {
                                fx = [.. fx.Select(Math.Abs)];
                                expression = $"|{expression}|";
                            }

                            formula = $"y = {expression} (r²={r2:F4})";
                            AddCurveFitLine(plotModel, sampleXs, fx, yAxis, color);
                        }
                    }
                    break;
                case CurveFittingType.Exponential:
                    {
                        if (xs.Length >= 2)
                        {
                            (double a, double r) = MathNet.Numerics.Fit.Exponential(xs, ys);
                            double r2 = MathNet.Numerics.GoodnessOfFit.RSquared(ys, xs.Select(x => a * Math.Exp(r * x)));
                            string expression = $"{a:F4}e^{r:F4}x";
                            double[] sampleXs = BuildSampleXs(xs);
                            double[] fx = [.. sampleXs.Select(x => a * Math.Exp(r * x))];

                            if (absoluteCurveFitting)
                            {
                                fx = [.. fx.Select(Math.Abs)];
                                expression = $"|{expression}|";
                            }

                            formula = $"y = {expression} (r²={r2:F4})";
                            AddCurveFitLine(plotModel, sampleXs, fx, yAxis, color);
                        }
                    }
                    break;
                case CurveFittingType.Logarithmic:
                    {
                        if (xs.Length >= 2)
                        {
                            (double a, double b) = MathNet.Numerics.Fit.Logarithm(xs, ys);
                            double r2 = MathNet.Numerics.GoodnessOfFit.RSquared(ys, xs.Select(x => a + b * Math.Log(x)));
                            string expression = $"{b:F4}ln(x) + {a:F4}";
                            double[] sampleXs = BuildSampleXs(xs);
                            double[] fx = [.. sampleXs.Select(x => a + b * Math.Log(x))];

                            if (absoluteCurveFitting)
                            {
                                fx = [.. fx.Select(Math.Abs)];
                                expression = $"|{expression}|";
                            }

                            formula = $"y = {expression} (r²={r2:F4})";
                            AddCurveFitLine(plotModel, sampleXs, fx, yAxis, color);
                        }
                    }
                    break;
                case CurveFittingType.Power:
                    {
                        if (xs.Length >= 2)
                        {
                            (double a, double b) = MathNet.Numerics.Fit.Power(xs, ys);
                            double f(double x) => a * Math.Pow(x, b);
                            double r2 = MathNet.Numerics.GoodnessOfFit.RSquared(ys, xs.Select(x => f(x)));
                            string expression = $"{a:F4}x^{b:F4}";
                            double[] sampleXs = BuildSampleXs(xs);
                            double[] fx = [.. sampleXs.Select(x => f(x))];

                            if (absoluteCurveFitting)
                            {
                                fx = [.. fx.Select(Math.Abs)];
                                expression = $"|{expression}|";
                            }

                            formula = $"y = {expression} (r²={r2:F4})";
                            AddCurveFitLine(plotModel, sampleXs, fx, yAxis, color);
                        }
                    }
                    break;
                case CurveFittingType.PolynomialOrder2:
                    {
                        formula = BuildPolynomialCurveFit(plotModel, xs, ys, 2, absoluteCurveFitting, yAxis, color);
                    }
                    break;
                case CurveFittingType.PolynomialOrder3:
                    {
                        formula = BuildPolynomialCurveFit(plotModel, xs, ys, 3, absoluteCurveFitting, yAxis, color);
                    }
                    break;
                case CurveFittingType.PolynomialOrder4:
                    {
                        formula = BuildPolynomialCurveFit(plotModel, xs, ys, 4, absoluteCurveFitting, yAxis, color);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(curveFittingType), @$"{Resource.ProjectPlan.Messages.Message_UnknownCurveFittingType} {curveFittingType}");
            }

            return formula;
        }

        private static string BuildCurveFitDerivative(
            Plot plotModel,
            List<AnnotatedMarker> markers,
            CurveFittingType curveFittingType,
            bool absoluteCurveFitting,
            IYAxis yAxis,
            Color color)
        {
            string formula = string.Empty;
            double[] xs = [.. markers.Select(x => x.X)];
            double[] ys = [.. markers.Select(x => x.Y)];

            Debug.Assert(xs.Length == ys.Length);

            // As with the fits themselves, the absolute option only passes
            // the derivative outputs through abs() before they are added to
            // the chart.

            switch (curveFittingType)
            {
                case CurveFittingType.None:
                    break;
                case CurveFittingType.Linear:
                    {
                        if (xs.Length >= 2)
                        {
                            // y = bx + a differentiates to the constant y′ = b.
                            (double a, double b) = MathNet.Numerics.Fit.Line(xs, ys);
                            double r2 = MathNet.Numerics.GoodnessOfFit.RSquared(ys, xs.Select(x => a + b * x));
                            string expression = $"{b:F4}";
                            double[] sampleXs = BuildSampleXs(xs);
                            double[] dfx = [.. sampleXs.Select(_ => b)];

                            if (absoluteCurveFitting)
                            {
                                dfx = [.. dfx.Select(Math.Abs)];
                                expression = $"|{expression}|";
                            }

                            formula = $"y′ = {expression} (r²={r2:F4})";
                            AddCurveFitLine(plotModel, sampleXs, dfx, yAxis, color);
                        }
                    }
                    break;
                case CurveFittingType.Exponential:
                    {
                        if (xs.Length >= 2)
                        {
                            // y = ae^rx differentiates to y′ = are^rx.
                            (double a, double r) = MathNet.Numerics.Fit.Exponential(xs, ys);
                            double r2 = MathNet.Numerics.GoodnessOfFit.RSquared(ys, xs.Select(x => a * Math.Exp(r * x)));
                            string expression = $"{a * r:F4}e^{r:F4}x";
                            double[] sampleXs = BuildSampleXs(xs);
                            double[] dfx = [.. sampleXs.Select(x => a * r * Math.Exp(r * x))];

                            if (absoluteCurveFitting)
                            {
                                dfx = [.. dfx.Select(Math.Abs)];
                                expression = $"|{expression}|";
                            }

                            formula = $"y′ = {expression} (r²={r2:F4})";
                            AddCurveFitLine(plotModel, sampleXs, dfx, yAxis, color);
                        }
                    }
                    break;
                case CurveFittingType.Logarithmic:
                    {
                        if (xs.Length >= 2)
                        {
                            // y = b·ln(x) + a differentiates to y′ = b/x.
                            (double a, double b) = MathNet.Numerics.Fit.Logarithm(xs, ys);
                            double r2 = MathNet.Numerics.GoodnessOfFit.RSquared(ys, xs.Select(x => a + b * Math.Log(x)));
                            string expression = $"{b:F4}/x";
                            double[] sampleXs = BuildSampleXs(xs);
                            double[] dfx = [.. sampleXs.Select(x => b / x)];

                            if (absoluteCurveFitting)
                            {
                                dfx = [.. dfx.Select(Math.Abs)];
                                expression = $"|{expression}|";
                            }

                            formula = $"y′ = {expression} (r²={r2:F4})";
                            AddCurveFitLine(plotModel, sampleXs, dfx, yAxis, color);
                        }
                    }
                    break;
                case CurveFittingType.Power:
                    {
                        if (xs.Length >= 2)
                        {
                            // y = ax^b differentiates to y′ = abx^(b−1).
                            (double a, double b) = MathNet.Numerics.Fit.Power(xs, ys);
                            double r2 = MathNet.Numerics.GoodnessOfFit.RSquared(ys, xs.Select(x => a * Math.Pow(x, b)));
                            string expression = $"{a * b:F4}x^{b - 1.0:F4}";
                            double[] sampleXs = BuildSampleXs(xs);
                            double[] dfx = [.. sampleXs.Select(x => a * b * Math.Pow(x, b - 1.0))];

                            if (absoluteCurveFitting)
                            {
                                dfx = [.. dfx.Select(Math.Abs)];
                                expression = $"|{expression}|";
                            }

                            formula = $"y′ = {expression} (r²={r2:F4})";
                            AddCurveFitLine(plotModel, sampleXs, dfx, yAxis, color);
                        }
                    }
                    break;
                case CurveFittingType.PolynomialOrder2:
                    {
                        formula = BuildPolynomialCurveFitDerivative(plotModel, xs, ys, 2, absoluteCurveFitting, yAxis, color);
                    }
                    break;
                case CurveFittingType.PolynomialOrder3:
                    {
                        formula = BuildPolynomialCurveFitDerivative(plotModel, xs, ys, 3, absoluteCurveFitting, yAxis, color);
                    }
                    break;
                case CurveFittingType.PolynomialOrder4:
                    {
                        formula = BuildPolynomialCurveFitDerivative(plotModel, xs, ys, 4, absoluteCurveFitting, yAxis, color);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(curveFittingType), @$"{Resource.ProjectPlan.Messages.Message_UnknownCurveFittingType} {curveFittingType}");
            }

            return formula;
        }

        private static double[] BuildSampleXs(double[] xs)
        {
            double minimum = xs.Min();
            double maximum = xs.Max();
            int count = Math.Max(c_MinimumSamplePoints, xs.Length);
            double step = (maximum - minimum) / (count - 1);
            return [.. Enumerable.Range(0, count).Select(i => minimum + (i * step))];
        }

        private static void AddCurveFitLine(
            Plot plotModel,
            double[] xs,
            double[] fx,
            IYAxis yAxis,
            Color color)
        {
            Scatter line = plotModel.Add.ScatterLine(xs, fx);
            line.MarkerSize = 0;
            line.LineWidth = 2;
            line.LinePattern = LinePattern.Dashed;
            line.Color = color;
            line.Axes.YAxis = yAxis;
        }

        private static string BuildPolynomialCurveFit(
            Plot plotModel,
            double[] xs,
            double[] ys,
            int order,
            bool absoluteCurveFitting,
            IYAxis yAxis,
            Color color)
        {
            int minimumOrder = 0;
            int maximumOrder = s_PolynomialSuperscripts.Length - 1;

            if (xs.Length != ys.Length
                || order < minimumOrder
                || order > maximumOrder
                || xs.Length <= order)
            {
                return string.Empty;
            }

            double[] coefficients = MathNet.Numerics.Fit.Polynomial(xs, ys, order);

            // The r² describes the fit against the raw outputs.
            double r2 = MathNet.Numerics.GoodnessOfFit.RSquared(ys, xs.Select(x => MathNet.Numerics.Polynomial.Evaluate(x, coefficients)));

            // Build the expression.
            var expressionBuilder = new StringBuilder();
            AppendPolynomialTerms(expressionBuilder, coefficients);
            string expression = expressionBuilder.ToString();

            double[] sampleXs = BuildSampleXs(xs);
            double[] fx = [.. sampleXs.Select(x => MathNet.Numerics.Polynomial.Evaluate(x, coefficients))];

            if (absoluteCurveFitting)
            {
                fx = [.. fx.Select(Math.Abs)];
                expression = $"|{expression}|";
            }

            // Plot the regression curve.
            AddCurveFitLine(plotModel, sampleXs, fx, yAxis, color);

            return $"y = {expression} (r²={r2:F4})";
        }

        private static string BuildPolynomialCurveFitDerivative(
            Plot plotModel,
            double[] xs,
            double[] ys,
            int order,
            bool absoluteCurveFitting,
            IYAxis yAxis,
            Color color)
        {
            // Differentiating drops one order, so the fit must be at least linear.
            int minimumOrder = 1;
            int maximumOrder = s_PolynomialSuperscripts.Length - 1;

            if (xs.Length != ys.Length
                || order < minimumOrder
                || order > maximumOrder
                || xs.Length <= order)
            {
                return string.Empty;
            }

            double[] coefficients = MathNet.Numerics.Fit.Polynomial(xs, ys, order);

            // Σcᵢxⁱ differentiates to Σi·cᵢx⁽ⁱ⁻¹⁾.
            double[] derivativeCoefficients = new double[coefficients.Length - 1];

            for (int i = 1; i < coefficients.Length; i++)
            {
                derivativeCoefficients[i - 1] = i * coefficients[i];
            }

            double[] sampleXs = BuildSampleXs(xs);
            double[] dfx = [.. sampleXs.Select(x => MathNet.Numerics.Polynomial.Evaluate(x, derivativeCoefficients))];

            // Build the expression. The r² still describes the underlying fit.
            var expressionBuilder = new StringBuilder();
            AppendPolynomialTerms(expressionBuilder, derivativeCoefficients);
            string expression = expressionBuilder.ToString();

            if (absoluteCurveFitting)
            {
                dfx = [.. dfx.Select(Math.Abs)];
                expression = $"|{expression}|";
            }

            AddCurveFitLine(plotModel, sampleXs, dfx, yAxis, color);

            double r2 = MathNet.Numerics.GoodnessOfFit.RSquared(ys, xs.Select(x => MathNet.Numerics.Polynomial.Evaluate(x, coefficients)));
            return $"y′ = {expression} (r²={r2:F4})";
        }

        private static void AppendPolynomialTerms(
            StringBuilder formula,
            double[] coefficients)
        {
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
                    formula.Append($"{Math.Abs(coefficients[i]):F4}x{s_PolynomialSuperscripts[i]}");
                }
                else
                {
                    formula.Append($"{Math.Abs(coefficients[i]):F4}");
                }
            }
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
            ScenarioChartPlotModel.Axes.AutoScale();
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

        private readonly ObservableAsPropertyHelper<bool> m_ShowDerivativeY1;
        public bool ShowDerivativeY1
        {
            get => m_ShowDerivativeY1.Value;
            set
            {
                lock (m_Lock) m_ProjectScenarioManagerViewModel.ScenarioChartShowDerivativeY1 = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_ShowDerivativeY2;
        public bool ShowDerivativeY2
        {
            get => m_ShowDerivativeY2.Value;
            set
            {
                lock (m_Lock) m_ProjectScenarioManagerViewModel.ScenarioChartShowDerivativeY2 = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_AbsoluteCurveFittingY1;
        public bool AbsoluteCurveFittingY1
        {
            get => m_AbsoluteCurveFittingY1.Value;
            set
            {
                lock (m_Lock) m_ProjectScenarioManagerViewModel.ScenarioChartAbsoluteCurveFittingY1 = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_AbsoluteCurveFittingY2;
        public bool AbsoluteCurveFittingY2
        {
            get => m_AbsoluteCurveFittingY2.Value;
            set
            {
                lock (m_Lock) m_ProjectScenarioManagerViewModel.ScenarioChartAbsoluteCurveFittingY2 = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_HasCurveFittingY1;
        public bool HasCurveFittingY1 => m_HasCurveFittingY1.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasCurveFittingY2;
        public bool HasCurveFittingY2 => m_HasCurveFittingY2.Value;

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
                    await m_ScottPlotImageExporter.SavePlotImageAsync(ScenarioChartPlotModel, filename, width, height);
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
            Plot? plotModel = null;
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
                        m_ProjectScenarioManagerViewModel.ScenarioChartShowDerivativeY1,
                        m_ProjectScenarioManagerViewModel.ScenarioChartShowDerivativeY2,
                        m_ProjectScenarioManagerViewModel.ScenarioChartAbsoluteCurveFittingY1,
                        m_ProjectScenarioManagerViewModel.ScenarioChartAbsoluteCurveFittingY2,
                        m_CoreViewModel.BaseTheme);
                }
            }

            plotModel ??= new Plot();
            Plot outgoing = ScenarioChartPlotModel;
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

            return await m_ScottPlotImageExporter.RenderPlotImageAsync(ScenarioChartPlotModel, width, height);
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
                m_ScenarioChartPlotModel.Dispose();
                m_IsBusy?.Dispose();
                m_HasStaleOutputs?.Dispose();
                m_HasCompilationErrors?.Dispose();
                m_ShowNames?.Dispose();
                m_TrackedMetricXAxis?.Dispose();
                m_TrackedMetricY1Axis?.Dispose();
                m_TrackedMetricY2Axis?.Dispose();
                m_CurveFittingTypeY1?.Dispose();
                m_CurveFittingTypeY2?.Dispose();
                m_ShowDerivativeY1?.Dispose();
                m_ShowDerivativeY2?.Dispose();
                m_AbsoluteCurveFittingY1?.Dispose();
                m_AbsoluteCurveFittingY2?.Dispose();
                m_HasCurveFittingY1?.Dispose();
                m_HasCurveFittingY2?.Dispose();
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
