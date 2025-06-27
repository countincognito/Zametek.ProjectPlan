using Avalonia;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using ReactiveUI;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public class EarnedValueChartManagerViewModel
        : ToolViewModelBase, IEarnedValueChartManagerViewModel
    {
        #region Fields

        private readonly object m_Lock;

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
                    Name = Resource.ProjectPlan.Filters.Filter_PdfFileType,
                    Patterns =
                    [
                        Resource.ProjectPlan.Filters.Filter_PdfFilePattern
                    ]
                }
            ];

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly ISettingService m_SettingService;
        private readonly IDialogService m_DialogService;
        private readonly IDateTimeCalculator m_DateTimeCalculator;

        private readonly IDisposable? m_BuildEarnedValueChartPlotModelSub;

        #endregion

        #region Ctors

        public EarnedValueChartManagerViewModel(
            ICoreViewModel coreViewModel,
            ISettingService settingService,
            IDialogService dialogService,
            IDateTimeCalculator dateTimeCalculator)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(settingService);
            ArgumentNullException.ThrowIfNull(dialogService);
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            m_Lock = new object();
            m_CoreViewModel = coreViewModel;
            m_SettingService = settingService;
            m_DialogService = dialogService;
            m_DateTimeCalculator = dateTimeCalculator;
            m_EarnedValueChartPlotModel = new PlotModel();

            {
                ReactiveCommand<Unit, Unit> saveEarnedValueChartImageFileCommand = ReactiveCommand.CreateFromTask(SaveEarnedValueChartImageFileAsync);
                SaveEarnedValueChartImageFileCommand = saveEarnedValueChartImageFileCommand;
            }

            m_IsBusy = this
                .WhenAnyValue(rcm => rcm.m_CoreViewModel.IsBusy)
                .ToProperty(this, rcm => rcm.IsBusy);

            m_HasStaleOutputs = this
                .WhenAnyValue(rcm => rcm.m_CoreViewModel.HasStaleOutputs)
                .ToProperty(this, rcm => rcm.HasStaleOutputs);

            m_HasCompilationErrors = this
                .WhenAnyValue(rcm => rcm.m_CoreViewModel.HasCompilationErrors)
                .ToProperty(this, rcm => rcm.HasCompilationErrors);

            m_ShowProjections = this
                .WhenAnyValue(main => main.m_CoreViewModel.EarnedValueShowProjections)
                .ToProperty(this, main => main.ShowProjections);

            m_ShowToday = this
                .WhenAnyValue(rcm => rcm.m_CoreViewModel.EarnedValueShowToday)
                .ToProperty(this, rcm => rcm.ShowToday);

            m_BuildEarnedValueChartPlotModelSub = this
                .WhenAnyValue(
                    rcm => rcm.m_CoreViewModel.TrackingSeriesSet,
                    rcm => rcm.m_CoreViewModel.ShowDates,
                    rcm => rcm.m_CoreViewModel.UseClassicDates,
                    rcm => rcm.m_CoreViewModel.UseBusinessDays,
                    rcm => rcm.ShowToday,
                    rcm => rcm.m_CoreViewModel.ProjectStart,
                    rcm => rcm.m_CoreViewModel.Today,
                    rcm => rcm.m_CoreViewModel.EarnedValueShowProjections,
                    rcm => rcm.m_CoreViewModel.BaseTheme,
                    (a, b, c, d, e, f, g, h, i) => (a, b, c, d, e, f, g, h, i))
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(async _ => await BuildEarnedValueChartPlotModelAsync());

            Id = Resource.ProjectPlan.Titles.Title_EarnedValueChartView;
            Title = Resource.ProjectPlan.Titles.Title_EarnedValueChartView;
        }

        #endregion

        #region Properties

        private PlotModel m_EarnedValueChartPlotModel;
        public PlotModel EarnedValueChartPlotModel
        {
            get
            {
                return m_EarnedValueChartPlotModel;
            }
            private set
            {
                lock (m_Lock) this.RaiseAndSetIfChanged(ref m_EarnedValueChartPlotModel, value);
            }
        }

        public object? ImageBounds { get; set; }

        #endregion

        #region Private Methods

        private async Task BuildEarnedValueChartPlotModelAsync()
        {
            try
            {
                lock (m_Lock)
                {
                    BuildEarnedValueChartPlotModel();
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

        private static PlotModel BuildEarnedValueChartPlotModelInternal(
            IDateTimeCalculator dateTimeCalculator,
            TrackingSeriesSetModel trackingSeriesSet,
            bool showToday,
            bool showDates,
            DateTimeOffset projectStart,
            DateTimeOffset today,
            bool showProjections,
            BaseTheme baseTheme)
        {
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            ArgumentNullException.ThrowIfNull(trackingSeriesSet);
            var plotModel = new PlotModel();

            if (trackingSeriesSet.Plan.Count == 0)
            {
                return plotModel.SetBaseTheme(baseTheme);
            }

            const double defaultMaxPercentage = 100.0;

            int chartEnd = trackingSeriesSet.Plan
                .Concat(trackingSeriesSet.Progress)
                .Concat(trackingSeriesSet.Effort)
                .Select(x => x.Time).DefaultIfEmpty().Max();

            double maxPercentage = trackingSeriesSet.Plan
                .Concat(trackingSeriesSet.Progress)
                .Concat(trackingSeriesSet.Effort)
                .Select(x => x.ValuePercentage).DefaultIfEmpty(defaultMaxPercentage).Max();

            if (showProjections)
            {
                chartEnd = Math.Max(chartEnd, trackingSeriesSet.PlanProjection
                    .Concat(trackingSeriesSet.ProgressProjection)
                    .Concat(trackingSeriesSet.EffortProjection)
                    .Select(x => x.Time).DefaultIfEmpty().Max());

                maxPercentage = Math.Max(maxPercentage, trackingSeriesSet.PlanProjection
                    .Concat(trackingSeriesSet.ProgressProjection)
                    .Concat(trackingSeriesSet.EffortProjection)
                    .Select(x => x.ValuePercentage).DefaultIfEmpty(defaultMaxPercentage).Max());
            }

            plotModel.Axes.Add(BuildEarnedValueChartXAxis(dateTimeCalculator, chartEnd, showDates, projectStart));
            plotModel.Axes.Add(BuildEarnedValueChartYAxis(maxPercentage));

            var legend = new Legend()
            {
                LegendBorder = OxyColors.Black,
                LegendBackground = OxyColors.Transparent,
                LegendPosition = LegendPosition.RightMiddle,
                LegendPlacement = LegendPlacement.Outside,
                //LegendOrientation = this.LegendOrientation,
                //LegendItemOrder = this.LegendItemOrder,
                //LegendItemAlignment = this.LegendItemAlignment,
                //LegendSymbolPlacement = this.LegendSymbolPlacement,
                //LegendMaxWidth = this.LegendMaxWidth,
                //LegendMaxHeight = this.LegendMaxHeight
            };

            plotModel.Legends.Add(legend);

            if (showProjections)
            {
                plotModel.Annotations.Add(new LineAnnotation()
                {
                    StrokeThickness = 1,
                    LineStyle = LineStyle.Dash,
                    Color = OxyColors.Black,
                    Text = Resource.ProjectPlan.Labels.Label_ProjectCompletion,
                    TextHorizontalAlignment = HorizontalAlignment.Left,
                    TextLinePosition = 0.05,
                    Type = LineAnnotationType.Horizontal,
                    Y = defaultMaxPercentage
                });
            }

            void PopulateLineSeries(
                LineSeries lineSeries,
                IList<TrackingPointModel> pointSeries)
            {
                ArgumentNullException.ThrowIfNull(lineSeries);
                ArgumentNullException.ThrowIfNull(pointSeries);
                if (pointSeries.Any())
                {
                    foreach (TrackingPointModel planPoint in pointSeries)
                    {
                        lineSeries.Points.Add(
                            new DataPoint(
                                ChartHelper.CalculateChartStartTimeXValue(
                                    planPoint.Time,
                                    showDates,
                                    projectStart,
                                    dateTimeCalculator),
                            planPoint.ValuePercentage));
                    }
                    plotModel.Series.Add(lineSeries);
                }
            }

            const double mainStrokeThickness = 2.0;
            const double projectionStrokeThickness = 1.0;
            string startEndFormat = showDates ? DateTimeCalculator.DateFormat : "0";
            string trackerFormatString = $"{{0}}\n{Resource.ProjectPlan.Labels.Label_TimeAxisTitle}: {{2:{startEndFormat}}}\n{Resource.ProjectPlan.Labels.Label_PercentageAxisTitle}: {{4:0.00}}";

            PopulateLineSeries(
                new LineSeries
                {
                    Title = Resource.ProjectPlan.Labels.Label_Plan,
                    Color = OxyColor.FromAColor(ColorHelper.AnnotationAFull, OxyColors.Blue),
                    StrokeThickness = mainStrokeThickness,
                    CanTrackerInterpolatePoints = true,
                    TrackerFormatString = trackerFormatString,
                },
                trackingSeriesSet.Plan);

            if (showProjections)
            {
                PopulateLineSeries(
                    new LineSeries
                    {
                        Title = Resource.ProjectPlan.Labels.Label_PlanProjection,
                        Color = OxyColor.FromAColor(ColorHelper.AnnotationAMedium, OxyColors.Blue),
                        StrokeThickness = projectionStrokeThickness,
                        CanTrackerInterpolatePoints = true,
                        TrackerFormatString = trackerFormatString,
                    },
                    trackingSeriesSet.PlanProjection);
            }

            PopulateLineSeries(
                new LineSeries
                {
                    Title = Resource.ProjectPlan.Labels.Label_Progress,
                    Color = OxyColor.FromAColor(ColorHelper.AnnotationAFull, OxyColors.Green),
                    StrokeThickness = mainStrokeThickness,
                    CanTrackerInterpolatePoints = true,
                    TrackerFormatString = trackerFormatString,
                },
                trackingSeriesSet.Progress);

            if (showProjections)
            {
                PopulateLineSeries(
                    new LineSeries
                    {
                        Title = Resource.ProjectPlan.Labels.Label_ProgressProjection,
                        Color = OxyColor.FromAColor(ColorHelper.AnnotationAMedium, OxyColors.Green),
                        StrokeThickness = projectionStrokeThickness,
                        CanTrackerInterpolatePoints = true,
                        TrackerFormatString = trackerFormatString,
                    },
                    trackingSeriesSet.ProgressProjection);
            }

            PopulateLineSeries(
                new LineSeries
                {
                    Title = Resource.ProjectPlan.Labels.Label_Effort,
                    Color = OxyColor.FromAColor(ColorHelper.AnnotationAFull, OxyColors.Red),
                    StrokeThickness = mainStrokeThickness,
                    CanTrackerInterpolatePoints = true,
                    TrackerFormatString = trackerFormatString,
                },
                trackingSeriesSet.Effort);

            if (showProjections)
            {
                PopulateLineSeries(
                    new LineSeries
                    {
                        Title = Resource.ProjectPlan.Labels.Label_EffortProjection,
                        Color = OxyColor.FromAColor(ColorHelper.AnnotationAMedium, OxyColors.Red),
                        StrokeThickness = projectionStrokeThickness,
                        CanTrackerInterpolatePoints = true,
                        TrackerFormatString = trackerFormatString,
                    },
                    trackingSeriesSet.EffortProjection);
            }

            if (showToday)
            {
                (int? intValue, _) = dateTimeCalculator.CalculateTimeAndDateTime(projectStart, today);

                if (intValue is not null)
                {
                    double todayTimeX = ChartHelper.CalculateChartStartTimeXValue(
                        intValue.GetValueOrDefault(),
                        showDates,
                        projectStart,
                        dateTimeCalculator);

                    var todayLine = new LineAnnotation
                    {
                        StrokeThickness = 2,
                        Color = OxyColors.Red,
                        LineStyle = LineStyle.Dot,
                        Type = LineAnnotationType.Vertical,
                        X = todayTimeX,
                        Y = 0.0
                    };

                    plotModel.Annotations.Add(todayLine);
                }
            }

            if (plotModel is IPlotModel plotModelInterface)
            {
                plotModelInterface.Update(true);
            }

            return plotModel.SetBaseTheme(baseTheme);
        }

        private static Axis BuildEarnedValueChartXAxis(
            IDateTimeCalculator dateTimeCalculator,
            int chartEnd,
            bool showDates,
            DateTimeOffset projectStart)
        {
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            if (chartEnd != default)
            {
                double minValue = ChartHelper.CalculateChartStartTimeXValue(
                    0,
                    showDates,
                    projectStart,
                    dateTimeCalculator);
                double maxValue = ChartHelper.CalculateChartFinishTimeXValue(
                    chartEnd,
                    showDates,
                    projectStart,
                    dateTimeCalculator);

                if (showDates)
                {
                    return new DateTimeAxis
                    {
                        Position = AxisPosition.Bottom,
                        Minimum = minValue,
                        Maximum = maxValue,
                        Title = Resource.ProjectPlan.Labels.Label_TimeAxisTitle,
                        StringFormat = DateTimeCalculator.DateFormat
                    };
                }

                return new LinearAxis
                {
                    Position = AxisPosition.Bottom,
                    Minimum = minValue,
                    Maximum = maxValue,
                    Title = Resource.ProjectPlan.Labels.Label_TimeAxisTitle
                };
            }
            return new LinearAxis();
        }

        private static LinearAxis BuildEarnedValueChartYAxis(double maximum)
        {
            return new LinearAxis
            {
                Position = AxisPosition.Left,
                Minimum = 0.0,
                Maximum = maximum,
                Title = Resource.ProjectPlan.Labels.Label_PercentageAxisTitle
            };
        }

        private async Task SaveEarnedValueChartImageFileAsync()
        {
            try
            {
                string title = m_SettingService.ProjectTitle;
                title = string.IsNullOrWhiteSpace(title) ? Resource.ProjectPlan.Titles.Title_UntitledProject : title;
                string evOutputFile = $@"{title}{Resource.ProjectPlan.Suffixes.Suffix_EarnedValueChart}";
                string directory = m_SettingService.ProjectDirectory;
                string? filename = await m_DialogService.ShowSaveFileDialogAsync(evOutputFile, directory, s_ExportFileFilters);

                if (!string.IsNullOrWhiteSpace(filename)
                    && ImageBounds is Rect bounds)
                {
                    int boundedWidth = Math.Abs(Convert.ToInt32(bounds.Width));
                    int boundedHeight = Math.Abs(Convert.ToInt32(bounds.Height));

                    await SaveEarnedValueChartImageFileAsync(filename, boundedWidth, boundedHeight);
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

        #region IEarnedValueChartManagerViewModel Members

        private readonly ObservableAsPropertyHelper<bool> m_IsBusy;
        public bool IsBusy => m_IsBusy.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasStaleOutputs;
        public bool HasStaleOutputs => m_HasStaleOutputs.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasCompilationErrors;
        public bool HasCompilationErrors => m_HasCompilationErrors.Value;

        private readonly ObservableAsPropertyHelper<bool> m_ShowProjections;
        public bool ShowProjections
        {
            get => m_ShowProjections.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.EarnedValueShowProjections = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_ShowToday;
        public bool ShowToday
        {
            get => m_ShowToday.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.EarnedValueShowToday = value;
            }
        }

        public ICommand SaveEarnedValueChartImageFileCommand { get; }

        public async Task SaveEarnedValueChartImageFileAsync(
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
                            using var stream = File.OpenWrite(filename);
                            OxyPlot.SkiaSharp.JpegExporter.Export(
                                EarnedValueChartPlotModel,
                                stream,
                                width,
                                height,
                                100);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImagePngFileExtension}", _ =>
                        {
                            using var stream = File.OpenWrite(filename);
                            OxyPlot.SkiaSharp.PngExporter.Export(
                                EarnedValueChartPlotModel,
                                stream,
                                width,
                                height);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_PdfFileExtension}", _ =>
                        {
                            using var stream = File.OpenWrite(filename);
                            OxyPlot.SkiaSharp.PdfExporter.Export(
                                EarnedValueChartPlotModel,
                                stream,
                                width,
                                height);
                        })
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

        public void BuildEarnedValueChartPlotModel()
        {
            PlotModel? plotModel = null;

            lock (m_Lock)
            {
                if (!HasCompilationErrors)
                {
                    plotModel = BuildEarnedValueChartPlotModelInternal(
                        m_DateTimeCalculator,
                        m_CoreViewModel.TrackingSeriesSet,
                        ShowToday,
                        m_CoreViewModel.ShowDates,
                        m_CoreViewModel.ProjectStart,
                        m_CoreViewModel.Today,
                        m_CoreViewModel.EarnedValueShowProjections,
                        m_CoreViewModel.BaseTheme);
                }
            }

            EarnedValueChartPlotModel = plotModel ?? new PlotModel();
        }

        #endregion

        #region IKillSubscriptions Members

        public void KillSubscriptions()
        {
            m_BuildEarnedValueChartPlotModelSub?.Dispose();
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
                m_ShowProjections?.Dispose();
                m_ShowToday?.Dispose();
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
