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
        : ToolViewModelBase, IEarnedValueChartManagerViewModel, IDisposable
    {
        #region Fields

        private readonly object m_Lock;

        private static readonly IList<IFileFilter> s_ExportFileFilters =
            new List<IFileFilter>
            {
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
            };

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

            m_ViewProjections = this
                .WhenAnyValue(main => main.m_CoreViewModel.ViewEarnedValueProjections)
                .ToProperty(this, main => main.ViewProjections);

            m_BuildEarnedValueChartPlotModelSub = this
                .WhenAnyValue(
                    rcm => rcm.m_CoreViewModel.TrackingSeriesSet,
                    rcm => rcm.m_CoreViewModel.ShowDates,
                    rcm => rcm.m_CoreViewModel.ProjectStartDateTime,
                    rcm => rcm.m_CoreViewModel.ViewEarnedValueProjections)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(async result =>
                {
                    EarnedValueChartPlotModel = await BuildEarnedValueChartPlotModelAsync(
                        m_DateTimeCalculator,
                        result.Item1,
                        result.Item2,
                        result.Item3,
                        result.Item4);
                });

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

        private void ToggleViewProjections() => ViewProjections = !ViewProjections;

        private async Task<PlotModel> BuildEarnedValueChartPlotModelAsync(
            IDateTimeCalculator dateTimeCalculator,
            TrackingSeriesSetModel trackingSeriesSet,
            bool showDates,
            DateTime projectStartDateTime,
            bool showProjections)
        {
            try
            {
                lock (m_Lock)
                {
                    return BuildEarnedValueChartPlotModel(
                        dateTimeCalculator,
                        trackingSeriesSet,
                        showDates,
                        projectStartDateTime,
                        showProjections);
                }
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    ex.Message);
            }

            return new PlotModel();
        }

        private static PlotModel BuildEarnedValueChartPlotModel(
            IDateTimeCalculator dateTimeCalculator,
            TrackingSeriesSetModel trackingSeriesSet,
            bool showDates,
            DateTime projectStartDateTime,
            bool showProjections)
        {
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            ArgumentNullException.ThrowIfNull(trackingSeriesSet);
            var plotModel = new PlotModel();
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

            plotModel.Axes.Add(BuildEarnedValueChartXAxis(dateTimeCalculator, chartEnd, showDates, projectStartDateTime));
            plotModel.Axes.Add(BuildEarnedValueChartYAxis(maxPercentage));

            var legend = new Legend()
            {
                LegendBorder = OxyColors.Black,
                LegendBackground = OxyColor.FromAColor(200, OxyColors.White),
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
                            new DataPoint(ChartHelper.CalculateChartTimeXValue(planPoint.Time, showDates, projectStartDateTime, dateTimeCalculator),
                            planPoint.ValuePercentage));
                    }
                    plotModel.Series.Add(lineSeries);
                }
            }

            byte mainLineOpacity = 255;
            double mainStrokeThickness = 2.0;

            byte projectionLineOpacity = 100;
            double projectionStrokeThickness = 1.0;

            PopulateLineSeries(
                new LineSeries
                {
                    Title = Resource.ProjectPlan.Labels.Label_Plan,
                    Color = OxyColor.FromAColor(mainLineOpacity, OxyColors.Blue),
                    StrokeThickness = mainStrokeThickness
                },
                trackingSeriesSet.Plan);

            if (showProjections)
            {
                PopulateLineSeries(
                    new LineSeries
                    {
                        Title = Resource.ProjectPlan.Labels.Label_PlanProjection,
                        Color = OxyColor.FromAColor(projectionLineOpacity, OxyColors.Blue),
                        StrokeThickness = projectionStrokeThickness
                    },
                    trackingSeriesSet.PlanProjection);
            }

            PopulateLineSeries(
                new LineSeries
                {
                    Title = Resource.ProjectPlan.Labels.Label_Progress,
                    Color = OxyColor.FromAColor(mainLineOpacity, OxyColors.Green),
                    StrokeThickness = mainStrokeThickness
                },
                trackingSeriesSet.Progress);

            if (showProjections)
            {
                PopulateLineSeries(
                    new LineSeries
                    {
                        Title = Resource.ProjectPlan.Labels.Label_ProgressProjection,
                        Color = OxyColor.FromAColor(projectionLineOpacity, OxyColors.Green),
                        StrokeThickness = projectionStrokeThickness
                    },
                    trackingSeriesSet.ProgressProjection);
            }

            PopulateLineSeries(
                new LineSeries
                {
                    Title = Resource.ProjectPlan.Labels.Label_Effort,
                    Color = OxyColor.FromAColor(mainLineOpacity, OxyColors.Red),
                    StrokeThickness = mainStrokeThickness
                },
                trackingSeriesSet.Effort);

            if (showProjections)
            {
                PopulateLineSeries(
                    new LineSeries
                    {
                        Title = Resource.ProjectPlan.Labels.Label_EffortProjection,
                        Color = OxyColor.FromAColor(projectionLineOpacity, OxyColors.Red),
                        StrokeThickness = projectionStrokeThickness
                    },
                    trackingSeriesSet.EffortProjection);
            }

            return plotModel;
        }

        private static Axis BuildEarnedValueChartXAxis(
            IDateTimeCalculator dateTimeCalculator,
            int chartEnd,
            bool showDates,
            DateTime projectStartDateTime)
        {
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            if (chartEnd != default)
            {
                double minValue = ChartHelper.CalculateChartTimeXValue(0, showDates, projectStartDateTime, dateTimeCalculator);
                double maxValue = ChartHelper.CalculateChartTimeXValue(chartEnd, showDates, projectStartDateTime, dateTimeCalculator);

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

        private async Task SaveEarnedValueChartImageFileInternalAsync(string? filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    Resource.ProjectPlan.Messages.Message_EmptyFilename);
            }
            else
            {
                if (ImageBounds is Rect bounds)
                {
                    string fileExtension = Path.GetExtension(filename);
                    //byte[]? data = null;

                    fileExtension.ValueSwitchOn()
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImageJpegFileExtension}", _ =>
                        {
                            using var stream = File.OpenWrite(filename);
                            OxyPlot.SkiaSharp.JpegExporter.Export(
                                EarnedValueChartPlotModel,
                                stream,
                                Convert.ToInt32(bounds.Width),
                                Convert.ToInt32(bounds.Height),
                                100);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImagePngFileExtension}", _ =>
                        {
                            // Use Avalonia exporter so the background can be white.
                            using var stream = File.OpenWrite(filename);
                            OxyPlot.Avalonia.PngExporter.Export(
                                EarnedValueChartPlotModel,
                                stream,
                                Convert.ToInt32(bounds.Width),
                                Convert.ToInt32(bounds.Height),
                                OxyColors.White);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_PdfFileExtension}", _ =>
                        {
                            using var stream = File.OpenWrite(filename);
                            OxyPlot.SkiaSharp.PdfExporter.Export(
                                EarnedValueChartPlotModel,
                                stream,
                                Convert.ToInt32(bounds.Width),
                                Convert.ToInt32(bounds.Height));
                        })
                        .Default(_ => throw new ArgumentOutOfRangeException(nameof(filename), @$"{Resource.ProjectPlan.Messages.Message_UnableToSaveFile} {filename}"));

                    //if (data is not null)
                    //{
                    //    using var stream = File.OpenWrite(filename);
                    //    await stream.WriteAsync(data);
                    //}
                }
            }
        }

        private async Task SaveEarnedValueChartImageFileAsync()
        {
            try
            {
                string projectTitle = m_SettingService.ProjectTitle;
                string directory = m_SettingService.ProjectDirectory;
                string? filename = await m_DialogService.ShowSaveFileDialogAsync(projectTitle, directory, s_ExportFileFilters);

                if (!string.IsNullOrWhiteSpace(filename))
                {
                    await SaveEarnedValueChartImageFileInternalAsync(filename);
                }
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
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

        private readonly ObservableAsPropertyHelper<bool> m_ViewProjections;
        public bool ViewProjections
        {
            get => m_ViewProjections.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.ViewEarnedValueProjections = value;
            }
        }

        public ICommand SaveEarnedValueChartImageFileCommand { get; }

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
                m_BuildEarnedValueChartPlotModelSub?.Dispose();
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
