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
        : ToolViewModelBase, IScenarioChartManagerViewModel, IDisposable
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
                //new FileFilter
                //{
                //    Name = Resource.ProjectPlan.Filters.Filter_PdfFileType,
                //    Patterns =
                //    [
                //        Resource.ProjectPlan.Filters.Filter_PdfFilePattern
                //    ]
                //}
            ];

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IProjectScenarioManagerViewModel m_ProjectScenarioManagerViewModel;
        private readonly ISettingService m_SettingService;
        private readonly IDialogService m_DialogService;
        private readonly IDateTimeCalculator m_DateTimeCalculator;

        private readonly IDisposable? m_BuildScenarioChartPlotModelSub;

        private const double c_AnnotatedEllipseRadius = 5.0;

        #endregion

        #region Ctors

        public ScenarioChartManagerViewModel(
            ICoreViewModel coreViewModel,
            IProjectScenarioManagerViewModel projectScenarioManagerViewModel,
            ISettingService settingService,
            IDialogService dialogService,
            IDateTimeCalculator dateTimeCalculator)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(projectScenarioManagerViewModel);
            ArgumentNullException.ThrowIfNull(settingService);
            ArgumentNullException.ThrowIfNull(dialogService);
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            m_Lock = new();
            m_CoreViewModel = coreViewModel;
            m_ProjectScenarioManagerViewModel = projectScenarioManagerViewModel;
            m_SettingService = settingService;
            m_DialogService = dialogService;
            m_DateTimeCalculator = dateTimeCalculator;
            m_ScenarioChartPlotModel = new AvaPlot();
            m_CurveFittingFormula = string.Empty;

            {
                ReactiveCommand<Unit, Unit> saveScenarioChartImageFileCommand = ReactiveCommand.CreateFromTask(SaveScenarioChartImageFileAsync);
                SaveScenarioChartImageFileCommand = saveScenarioChartImageFileCommand;
            }

            ResetScenarioChartCommand = ReactiveCommand.Create(ResetScenarioChart);

            ChangeTrackedMetricXAxisCommand = ReactiveCommand.CreateFromTask<TrackedMetrics>(ChangeTrackedMetricXAxisAsync);
            ChangeTrackedMetricYAxisCommand = ReactiveCommand.CreateFromTask<TrackedMetrics>(ChangeTrackedMetricYAxisAsync);
            ChangeCurveFittingTypeCommand = ReactiveCommand.CreateFromTask<CurveFittingType>(ChangeCurveFittingTypeAsync);

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

            m_TrackedMetricYAxis = this
                .WhenAnyValue(rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartTrackedMetricYAxis)
                .ToProperty(this, rcm => rcm.TrackedMetricYAxis);

            m_CurveFittingType = this
                .WhenAnyValue(rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartCurveFittingType)
                .ToProperty(this, rcm => rcm.CurveFittingType);

            m_BuildScenarioChartPlotModelSub = this
                .WhenAnyValue(
                    rcm => rcm.m_ProjectScenarioManagerViewModel.TrackedMetricsSet,
                    rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartShowNames,
                    rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartTrackedMetricXAxis,
                    rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartTrackedMetricYAxis,
                    rcm => rcm.m_ProjectScenarioManagerViewModel.ScenarioChartCurveFittingType,
                    rcm => rcm.m_CoreViewModel.ProjectStart,
                    rcm => rcm.m_CoreViewModel.BaseTheme)
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

        private static (AvaPlot, string) BuildScenarioChartPlotModelInternal(
            TrackedMetricsSetModel trackedMetricsSet,
            bool showNames,
            TrackedMetrics xMetric,
            TrackedMetrics yMetric,
            CurveFittingType curveFittingType,
            BaseTheme baseTheme)
        {
            ArgumentNullException.ThrowIfNull(trackedMetricsSet);
            var plotModel = new AvaPlot();
            plotModel.Plot.HideGrid();

            // Now build the plot.

            if (trackedMetricsSet.TrackedMetrics.Count == 0
                || xMetric == TrackedMetrics.None
                || yMetric == TrackedMetrics.None)
            {
                return (plotModel.SetBaseTheme(baseTheme), string.Empty);
            }

            // Select the metric for the X axis.
            Func<MetricsModel, double> xMetricFunction = GetMetricFunction(xMetric);

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
                    MarkerFillColor = Colors.Blue,
                    MarkerLineColor = Colors.WhiteSmoke,
                    Shape = MarkerShape.FilledCircle,
                    Annotation = trackedMetrics.Path,
                };

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

            // X Axis title.
            IXAxis xAxis = plotModel.Plot.Axes.Bottom;
            xAxis.Label.Text = StringConverters.TrackedMetricsValue(xMetric);
            xAxis.Label.FontSize = PlotHelper.FontSize;
            xAxis.Label.Bold = false;

            // Y Axis title.
            IYAxis yAxis = plotModel.Plot.Axes.Left;
            yAxis.Label.Text = StringConverters.TrackedMetricsValue(yMetric);
            yAxis.Label.FontSize = PlotHelper.FontSize;
            yAxis.Label.Bold = false;

            // Build the curve fitting if requested.
            string curveFittingFormula = BuildCurveFit(plotModel, markers, curveFittingType);
            plotModel.Plot.Axes.AutoScale();

            plotModel.Plot.Axes.AutoScaleExpand();

            return (plotModel.SetBaseTheme(baseTheme), curveFittingFormula);
        }

        private static string BuildCurveFit(
            AvaPlot plotModel,
            List<AnnotatedMarker> markers,
            CurveFittingType curveFittingType)
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
                        }
                    }
                    break;
                case CurveFittingType.PolynomialOrder2:
                    {
                        formula = BuildPolynomialCurveFit(plotModel, xs, ys, 2);
                    }
                    break;
                case CurveFittingType.PolynomialOrder3:
                    {
                        formula = BuildPolynomialCurveFit(plotModel, xs, ys, 3);
                    }
                    break;
                case CurveFittingType.PolynomialOrder4:
                    {
                        formula = BuildPolynomialCurveFit(plotModel, xs, ys, 4);
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
            int order)
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

        private async Task ChangeTrackedMetricYAxisAsync(TrackedMetrics trackedMetric)
        {
            try
            {
                TrackedMetricYAxis = trackedMetric;
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task ChangeCurveFittingTypeAsync(CurveFittingType curveFittingType)
        {
            try
            {
                CurveFittingType = curveFittingType;
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

        private readonly ObservableAsPropertyHelper<TrackedMetrics> m_TrackedMetricYAxis;
        public TrackedMetrics TrackedMetricYAxis
        {
            get => m_TrackedMetricYAxis.Value;
            set
            {
                lock (m_Lock) m_ProjectScenarioManagerViewModel.ScenarioChartTrackedMetricYAxis = value;
            }
        }

        private readonly ObservableAsPropertyHelper<CurveFittingType> m_CurveFittingType;
        public CurveFittingType CurveFittingType
        {
            get => m_CurveFittingType.Value;
            set
            {
                lock (m_Lock) m_ProjectScenarioManagerViewModel.ScenarioChartCurveFittingType = value;
            }
        }

        public ICommand SaveScenarioChartImageFileCommand { get; }

        public ICommand ResetScenarioChartCommand { get; }

        public ICommand ChangeTrackedMetricXAxisCommand { get; }

        public ICommand ChangeTrackedMetricYAxisCommand { get; }

        public ICommand ChangeCurveFittingTypeCommand { get; }

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
                    string fileExtension = Path.GetExtension(filename);

                    fileExtension.ValueSwitchOn()
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImageJpegFileExtension}", _ =>
                        {
                            ScenarioChartPlotModel.Plot.Save(
                                filename, width, height, ImageFormats.FromFilename(filename), 100);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImagePngFileExtension}", _ =>
                        {
                            ScenarioChartPlotModel.Plot.Save(
                                filename, width, height, ImageFormats.FromFilename(filename), 100);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImageBmpFileExtension}", _ =>
                        {
                            ScenarioChartPlotModel.Plot.Save(
                                filename, width, height, ImageFormats.FromFilename(filename), 100);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImageWebpFileExtension}", _ =>
                        {
                            ScenarioChartPlotModel.Plot.Save(
                                filename, width, height, ImageFormats.FromFilename(filename), 100);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImageSvgFileExtension}", _ =>
                        {
                            ScenarioChartPlotModel.Plot.Save(
                                filename, width, height, ImageFormats.FromFilename(filename), 100);
                        })
                        //.Case($".{Resource.ProjectPlan.Filters.Filter_PdfFileExtension}", _ =>
                        //{
                        //})
                        .Default(_ => throw new ArgumentOutOfRangeException(nameof(filename), @$"{Resource.ProjectPlan.Messages.Message_UnableToSaveFile} {filename}"));
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

        private string m_CurveFittingFormula;
        public string CurveFittingFormula
        {
            get => string.IsNullOrWhiteSpace(m_CurveFittingFormula) ? string.Empty : m_CurveFittingFormula;
            private set
            {
                lock (m_Lock)
                {
                    m_CurveFittingFormula = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public void BuildScenarioChartPlotModel()
        {
            AvaPlot? plotModel = null;
            string curveFittingFormula = string.Empty;

            lock (m_Lock)
            {
                if (!HasCompilationErrors)
                {
                    (plotModel, curveFittingFormula) = BuildScenarioChartPlotModelInternal(
                        m_ProjectScenarioManagerViewModel.TrackedMetricsSet,
                        m_ProjectScenarioManagerViewModel.ScenarioChartShowNames,
                        m_ProjectScenarioManagerViewModel.ScenarioChartTrackedMetricXAxis,
                        m_ProjectScenarioManagerViewModel.ScenarioChartTrackedMetricYAxis,
                        m_ProjectScenarioManagerViewModel.ScenarioChartCurveFittingType,
                        m_CoreViewModel.BaseTheme);
                }
            }

            plotModel ??= new AvaPlot();
            plotModel.ClearContextMenu();
            ScenarioChartPlotModel = plotModel;
            CurveFittingFormula = curveFittingFormula;
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
                m_IsBusy?.Dispose();
                m_HasStaleOutputs?.Dispose();
                m_HasCompilationErrors?.Dispose();
                m_TrackedMetricXAxis?.Dispose();
                m_TrackedMetricYAxis?.Dispose();
                m_CurveFittingType?.Dispose();
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
