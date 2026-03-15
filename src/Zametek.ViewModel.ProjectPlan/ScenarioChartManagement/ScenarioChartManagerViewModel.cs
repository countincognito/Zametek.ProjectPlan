using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
using com.sun.tools.javadoc;
using ReactiveUI;
using ScottPlot;
using ScottPlot.Avalonia;
using ScottPlot.Plottables;
using System.Reactive;
using System.Reactive.Linq;
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

            {
                ReactiveCommand<Unit, Unit> saveScenarioChartImageFileCommand = ReactiveCommand.CreateFromTask(SaveScenarioChartImageFileAsync);
                SaveScenarioChartImageFileCommand = saveScenarioChartImageFileCommand;
            }

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

            m_TrackedMetricXAxis = this
                .WhenAnyValue(rcm => rcm.m_SettingService.ScenarioChartTrackedMetricXAxis)
                .ToProperty(this, rcm => rcm.TrackedMetricXAxis);

            m_TrackedMetricYAxis = this
                .WhenAnyValue(rcm => rcm.m_SettingService.ScenarioChartTrackedMetricYAxis)
                .ToProperty(this, rcm => rcm.TrackedMetricYAxis);

            m_BuildScenarioChartPlotModelSub = this
                .WhenAnyValue(
                    rcm => rcm.m_ProjectScenarioManagerViewModel.TrackedMetricsSet,
                    //rcm => rcm.m_CoreViewModel.DisplaySettingsViewModel.ShowDates,
                    //rcm => rcm.m_CoreViewModel.DisplaySettingsViewModel.UseClassicDates,
                    //rcm => rcm.m_CoreViewModel.DisplaySettingsViewModel.UseBusinessDays,
                    rcm => rcm.m_SettingService.ScenarioChartTrackedMetricXAxis,
                    rcm => rcm.m_SettingService.ScenarioChartTrackedMetricYAxis,
                    rcm => rcm.m_CoreViewModel.ProjectStart,
                    rcm => rcm.m_CoreViewModel.BaseTheme)//,
                                                         //(x, _, _, _, _, _, _, _) => x) // Do this as a workaround because WhenAnyValue cannot handle this many individual inputs.)
                .ObserveOn(RxApp.TaskpoolScheduler)
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

        private static AvaPlot BuildScenarioChartPlotModelInternal(
            TrackedMetricsSetModel trackedMetricsSet,
            //IDateTimeCalculator dateTimeCalculator,
            //bool showDates,
            //DateTimeOffset projectStart,
            TrackedMetrics xMetric,
            TrackedMetrics yMetric,
            BaseTheme baseTheme)
        {
            ArgumentNullException.ThrowIfNull(trackedMetricsSet);
            //ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            var plotModel = new AvaPlot();
            plotModel.Plot.HideGrid();

            // Select the metric for the X axis.
            Func<MetricsModel, double> xMetricFunction = GetMetricFunction(xMetric);

            // Select the metric for the Y axis.
            Func<MetricsModel, double> yMetricFunction = GetMetricFunction(yMetric);

            // Now build the plot.

            if (trackedMetricsSet.TrackedMetrics.Count == 0)
            {
                return plotModel.SetBaseTheme(baseTheme);
            }

            // Gather the data points for the selected metrics.

            var dataX = new List<double>();
            var dataY = new List<double>();

            foreach (TrackedMetricsModel trackedMetrics in trackedMetricsSet.TrackedMetrics)
            {
                var marker = new AnnotatedMarker
                {
                    X = xMetricFunction(trackedMetrics.Metrics),
                    Y = yMetricFunction(trackedMetrics.Metrics),
                    Size = 10,
                    Color = Colors.Blue,
                    Shape = MarkerShape.FilledCircle,
                    Annotation = trackedMetrics.Path
                };

                plotModel.Plot.PlottableList.Add(marker);
            }

            plotModel.Plot.Axes.AutoScale();

            return plotModel.SetBaseTheme(baseTheme);
        }

        private static Func<MetricsModel, double> GetMetricFunction(TrackedMetrics metric)
        {
            return metric switch
            {
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

        private readonly ObservableAsPropertyHelper<TrackedMetrics> m_TrackedMetricXAxis;
        public TrackedMetrics TrackedMetricXAxis
        {
            get => m_TrackedMetricXAxis.Value;
            set
            {
                lock (m_Lock) m_SettingService.ScenarioChartTrackedMetricXAxis = value;
            }
        }

        private readonly ObservableAsPropertyHelper<TrackedMetrics> m_TrackedMetricYAxis;
        public TrackedMetrics TrackedMetricYAxis
        {
            get => m_TrackedMetricYAxis.Value;
            set
            {
                lock (m_Lock) m_SettingService.ScenarioChartTrackedMetricYAxis = value;
            }
        }

        public ICommand SaveScenarioChartImageFileCommand { get; }

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

        public void BuildScenarioChartPlotModel()
        {
            AvaPlot? plotModel = null;

            lock (m_Lock)
            {
                if (!HasCompilationErrors)
                {
                    plotModel = BuildScenarioChartPlotModelInternal(
                        m_ProjectScenarioManagerViewModel.TrackedMetricsSet,
                        //m_DateTimeCalculator,
                        //m_CoreViewModel.DisplaySettingsViewModel.ShowDates,
                        //m_CoreViewModel.ProjectStart,
                        m_SettingService.ScenarioChartTrackedMetricXAxis,
                        m_SettingService.ScenarioChartTrackedMetricYAxis,
                        m_CoreViewModel.BaseTheme);
                }
            }

            plotModel ??= new AvaPlot();

            // Clear existing menu items.
            plotModel.Menu?.Clear();

            // Add menu items with custom actions.
            plotModel.Menu?.Add(Resource.ProjectPlan.Menus.Menu_SaveAs, (plot) =>
            {
                SaveScenarioChartImageFileCommand.Execute(null);
            });
            plotModel.Menu?.Add(Resource.ProjectPlan.Menus.Menu_Reset, (plot) =>
            {
                plot.Axes.AutoScale();
            });

            //plotModel.Plot.Axes.AutoScale();
            ScenarioChartPlotModel = plotModel;
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
                // TODO: dispose managed state (managed objects).
                KillSubscriptions();
                m_IsBusy?.Dispose();
                m_HasStaleOutputs?.Dispose();
                m_HasCompilationErrors?.Dispose();
                m_TrackedMetricXAxis?.Dispose();
                m_TrackedMetricYAxis?.Dispose();
                //m_DisplayStyle?.Dispose();
                //m_ShowToday?.Dispose();
                //m_ShowMilestones?.Dispose();
            }

            // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
            // TODO: set large fields to null.

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
