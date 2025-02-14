using Avalonia;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using ReactiveUI;
using System.Data;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ResourceChartManagerViewModel
        : ToolViewModelBase, IResourceChartManagerViewModel, IDisposable
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

        private readonly IDisposable? m_BuildResourceChartPlotModelSub;

        #endregion

        #region Ctors

        public ResourceChartManagerViewModel(
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
            m_ResourceChartPlotModel = new PlotModel();

            {
                ReactiveCommand<Unit, Unit> saveResourceChartImageFileCommand = ReactiveCommand.CreateFromTask(SaveResourceChartImageFileAsync);
                SaveResourceChartImageFileCommand = saveResourceChartImageFileCommand;
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

            m_AllocationMode = this
                .WhenAnyValue(rcm => rcm.m_CoreViewModel.ResourceChartAllocationMode)
                .ToProperty(this, rcm => rcm.AllocationMode);

            m_ScheduleMode = this
                .WhenAnyValue(rcm => rcm.m_CoreViewModel.ResourceChartScheduleMode)
                .ToProperty(this, rcm => rcm.ScheduleMode);

            m_DisplayStyle = this
                .WhenAnyValue(rcm => rcm.m_CoreViewModel.ResourceChartDisplayStyle)
                .ToProperty(this, rcm => rcm.DisplayStyle);

            m_ShowToday = this
                .WhenAnyValue(rcm => rcm.m_CoreViewModel.ResourceChartShowToday)
                .ToProperty(this, rcm => rcm.ShowToday);

            m_BuildResourceChartPlotModelSub = this
                .WhenAnyValue(
                    rcm => rcm.m_CoreViewModel.ResourceSeriesSet,
                    rcm => rcm.m_CoreViewModel.ShowDates,
                    rcm => rcm.m_CoreViewModel.ProjectStartDateTime,
                    rcm => rcm.m_CoreViewModel.TodayDateTime,
                    rcm => rcm.AllocationMode,
                    rcm => rcm.ScheduleMode,
                    rcm => rcm.DisplayStyle,
                    rcm => rcm.ShowToday,
                    rcm => rcm.m_CoreViewModel.BaseTheme,
                    (a, b, c, d, e, f, g, h, i) => (a, b, c, d, e, f, g, h, i)) // Do this as a workaround because WhenAnyValue cannot handle this many individual inputs.
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(async _ => await BuildResourceChartPlotModelAsync());

            Id = Resource.ProjectPlan.Titles.Title_ResourceChartView;
            Title = Resource.ProjectPlan.Titles.Title_ResourceChartView;
        }

        #endregion

        #region Properties

        private PlotModel m_ResourceChartPlotModel;
        public PlotModel ResourceChartPlotModel
        {
            get
            {
                return m_ResourceChartPlotModel;
            }
            private set
            {
                lock (m_Lock) this.RaiseAndSetIfChanged(ref m_ResourceChartPlotModel, value);
            }
        }

        public object? ImageBounds { get; set; }

        #endregion

        #region Private Methods

        private async Task BuildResourceChartPlotModelAsync()
        {
            try
            {
                lock (m_Lock)
                {
                    BuildResourceChartPlotModel();
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

        private static PlotModel BuildResourceChartPlotModelInternal(
            IDateTimeCalculator dateTimeCalculator,
            ResourceSeriesSetModel resourceSeriesSet,
            bool showDates,
            DateTime projectStartDateTime,
            DateTime todayDateTime,
            AllocationMode allocationMode,
            ScheduleMode scheduleMode,
            DisplayStyle displayStyle,
            bool showToday,
            BaseTheme baseTheme)
        {
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            ArgumentNullException.ThrowIfNull(resourceSeriesSet);
            var plotModel = new PlotModel();

            // Select the type of allocation to be displayed.

            Func<ResourceScheduleModel, List<bool>>? allocationFunction = null;

            allocationFunction = allocationMode switch
            {
                AllocationMode.Activity => (ResourceScheduleModel model) => model.ActivityAllocation,
                AllocationMode.Cost => (ResourceScheduleModel model) => model.CostAllocation,
                AllocationMode.Effort => (ResourceScheduleModel model) => model.EffortAllocation,
                _ => throw new ArgumentOutOfRangeException(nameof(allocationMode), @$"{Resource.ProjectPlan.Messages.Message_UnknownAllocationMode} {allocationMode}"),
            };

            // Select the type of schedule to be displayed.

            Func<ResourceSeriesSetModel, List<ResourceSeriesModel>>? scheduleFunction = null;

            scheduleFunction = scheduleMode switch
            {
                ScheduleMode.Combined => (ResourceSeriesSetModel model) => model.Combined,
                ScheduleMode.Scheduled => (ResourceSeriesSetModel model) => model.Scheduled,
                ScheduleMode.Unscheduled => (ResourceSeriesSetModel model) => model.Unscheduled,
                _ => throw new ArgumentOutOfRangeException(nameof(scheduleMode), @$"{Resource.ProjectPlan.Messages.Message_UnknownScheduleMode} {scheduleMode}"),
            };

            // Now build the plot.

            if (scheduleFunction(resourceSeriesSet).Count == 0)
            {
                return plotModel.SetBaseTheme(baseTheme);
            }

            IEnumerable<ResourceSeriesModel> resourceSeries = scheduleFunction(resourceSeriesSet).OrderBy(x => x.DisplayOrder);
            int finishTime = resourceSeriesSet.ResourceSchedules.Select(x => x.FinishTime).DefaultIfEmpty().Max();

            plotModel.Axes.Add(BuildResourceChartXAxis(dateTimeCalculator, finishTime, showDates, projectStartDateTime));
            plotModel.Axes.Add(BuildResourceChartYAxis());

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

            if (resourceSeries.Any())
            {
                IList<int> total1 = [];
                IList<int> total2 = [];
                string startEndFormat = showDates ? DateTimeCalculator.DateFormat : "0";

                foreach (ResourceSeriesModel series in resourceSeries)
                {
                    if (series != null)
                    {
                        var color = OxyColor.FromArgb(
                            series.ColorFormat.A,
                            series.ColorFormat.R,
                            series.ColorFormat.G,
                            series.ColorFormat.B);

                        var areaSeries = new AreaSeries
                        {
                            StrokeThickness = 0.0,
                            Title = series.Title,
                            Fill = color,
                            Color = color,
                            CanTrackerInterpolatePoints = false,
                            TrackerFormatString = $"{{0}}\n{Resource.ProjectPlan.Labels.Label_TimeAxisTitle}: {{2:{startEndFormat}}}\n{Resource.ProjectPlan.Labels.Label_ResourcesAxisTitle}: {{4}}",
                        };

                        switch (displayStyle)
                        {
                            case DisplayStyle.Slanted:
                                {
                                    if (allocationFunction(series.ResourceSchedule).Count != 0)
                                    {
                                        // Mark the start of the plot.
                                        areaSeries.Points.Add(new DataPoint(0.0, 0.0));
                                        areaSeries.Points2.Add(new DataPoint(0.0, 0.0));

                                        for (int i = 0; i < allocationFunction(series.ResourceSchedule).Count; i++)
                                        {
                                            bool allocationExists = allocationFunction(series.ResourceSchedule)[i];
                                            if (i >= total1.Count)
                                            {
                                                total1.Add(0);
                                            }
                                            int dayNumber = i + 1;
                                            areaSeries.Points.Add(
                                                new DataPoint(ChartHelper.CalculateChartTimeXValue(dayNumber, showDates, projectStartDateTime, dateTimeCalculator),
                                                total1[i]));
                                            total1[i] += allocationExists ? 1 : 0;
                                            areaSeries.Points2.Add(
                                                new DataPoint(ChartHelper.CalculateChartTimeXValue(dayNumber, showDates, projectStartDateTime, dateTimeCalculator),
                                                total1[i]));
                                        }
                                    }
                                }
                                break;
                            case DisplayStyle.Block:
                                {
                                    if (allocationFunction(series.ResourceSchedule).Count != 0)
                                    {
                                        for (int i = 0; i < allocationFunction(series.ResourceSchedule).Count; i++)
                                        {
                                            bool allocationExists = allocationFunction(series.ResourceSchedule)[i];

                                            // First point.
                                            int dayNumber = i;

                                            if (dayNumber >= total1.Count)
                                            {
                                                total1.Add(0);
                                            }
                                            areaSeries.Points.Add(
                                                new DataPoint(ChartHelper.CalculateChartTimeXValue(dayNumber, showDates, projectStartDateTime, dateTimeCalculator),
                                                total1[dayNumber]));
                                            total1[dayNumber] += allocationExists ? 1 : 0;
                                            areaSeries.Points2.Add(
                                                new DataPoint(ChartHelper.CalculateChartTimeXValue(dayNumber, showDates, projectStartDateTime, dateTimeCalculator),
                                                total1[dayNumber]));

                                            // Second point.
                                            if (dayNumber >= total2.Count)
                                            {
                                                total2.Add(0);
                                            }

                                            dayNumber++;

                                            if (dayNumber >= total2.Count)
                                            {
                                                total2.Add(0);
                                            }
                                            areaSeries.Points.Add(
                                                new DataPoint(ChartHelper.CalculateChartTimeXValue(dayNumber, showDates, projectStartDateTime, dateTimeCalculator),
                                                total2[dayNumber]));
                                            total2[dayNumber] += allocationExists ? 1 : 0;
                                            areaSeries.Points2.Add(
                                                new DataPoint(ChartHelper.CalculateChartTimeXValue(dayNumber, showDates, projectStartDateTime, dateTimeCalculator),
                                                total2[dayNumber]));
                                        }
                                    }
                                }
                                break;
                            default:
                                throw new ArgumentOutOfRangeException(nameof(displayStyle), @$"{Resource.ProjectPlan.Messages.Message_UnknownDisplayStyle} {displayStyle}");
                        }

                        plotModel.Series.Add(areaSeries);
                    }
                }

                if (showToday)
                {
                    (int? intValue, _) = dateTimeCalculator.CalculateTimeAndDateTime(projectStartDateTime, todayDateTime);

                    if (intValue is not null)
                    {
                        double todayTimeX = ChartHelper.CalculateChartTimeXValue(intValue.GetValueOrDefault(), showDates, projectStartDateTime, dateTimeCalculator);

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
            }

            if (plotModel is IPlotModel plotModelInterface)
            {
                plotModelInterface.Update(true);
            }

            return plotModel.SetBaseTheme(baseTheme);
        }

        private static Axis BuildResourceChartXAxis(
            IDateTimeCalculator dateTimeCalculator,
            int finishTime,
            bool showDates,
            DateTime projectStartDateTime)
        {
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            if (finishTime != default)
            {
                double minValue = ChartHelper.CalculateChartTimeXValue(0, showDates, projectStartDateTime, dateTimeCalculator);
                double maxValue = ChartHelper.CalculateChartTimeXValue(finishTime, showDates, projectStartDateTime, dateTimeCalculator);

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

        private static LinearAxis BuildResourceChartYAxis()
        {
            return new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = Resource.ProjectPlan.Labels.Label_ResourcesAxisTitle,
                MinorStep = 1
            };
        }

        private async Task SaveResourceChartImageFileAsync()
        {
            try
            {
                string title = m_SettingService.ProjectTitle;
                title = string.IsNullOrWhiteSpace(title) ? Resource.ProjectPlan.Titles.Title_UntitledProject : title;
                string resourceOutputFile = $@"{title}{Resource.ProjectPlan.Suffixes.Suffix_ResourceChart}";
                string directory = m_SettingService.ProjectDirectory;
                string? filename = await m_DialogService.ShowSaveFileDialogAsync(resourceOutputFile, directory, s_ExportFileFilters);

                if (!string.IsNullOrWhiteSpace(filename)
                    && ImageBounds is Rect bounds)
                {
                    int boundedWidth = Math.Abs(Convert.ToInt32(bounds.Width));
                    int boundedHeight = Math.Abs(Convert.ToInt32(bounds.Height));

                    await SaveResourceChartImageFileAsync(filename, boundedWidth, boundedHeight);
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

        #region IResourceChartManagerViewModel Members

        private readonly ObservableAsPropertyHelper<bool> m_IsBusy;
        public bool IsBusy => m_IsBusy.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasStaleOutputs;
        public bool HasStaleOutputs => m_HasStaleOutputs.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasCompilationErrors;
        public bool HasCompilationErrors => m_HasCompilationErrors.Value;

        private readonly ObservableAsPropertyHelper<AllocationMode> m_AllocationMode;
        public AllocationMode AllocationMode
        {
            get => m_AllocationMode.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.ResourceChartAllocationMode = value;
            }
        }

        private readonly ObservableAsPropertyHelper<ScheduleMode> m_ScheduleMode;
        public ScheduleMode ScheduleMode
        {
            get => m_ScheduleMode.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.ResourceChartScheduleMode = value;
            }
        }

        private readonly ObservableAsPropertyHelper<DisplayStyle> m_DisplayStyle;
        public DisplayStyle DisplayStyle
        {
            get => m_DisplayStyle.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.ResourceChartDisplayStyle = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_ShowToday;
        public bool ShowToday
        {
            get => m_ShowToday.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.ResourceChartShowToday = value;
            }
        }

        public ICommand SaveResourceChartImageFileCommand { get; }

        public async Task SaveResourceChartImageFileAsync(
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
                                ResourceChartPlotModel,
                                stream,
                                width,
                                height,
                                100);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImagePngFileExtension}", _ =>
                        {
                            using var stream = File.OpenWrite(filename);
                            OxyPlot.SkiaSharp.PngExporter.Export(
                                ResourceChartPlotModel,
                                stream,
                                width,
                                height);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_PdfFileExtension}", _ =>
                        {
                            using var stream = File.OpenWrite(filename);
                            OxyPlot.SkiaSharp.PdfExporter.Export(
                                ResourceChartPlotModel,
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

        public void BuildResourceChartPlotModel()
        {
            PlotModel? plotModel = null;

            lock (m_Lock)
            {
                plotModel = BuildResourceChartPlotModelInternal(
                    m_DateTimeCalculator,
                    m_CoreViewModel.ResourceSeriesSet,
                    m_CoreViewModel.ShowDates,
                    m_CoreViewModel.ProjectStartDateTime,
                    m_CoreViewModel.TodayDateTime,
                    AllocationMode,
                    ScheduleMode,
                    DisplayStyle,
                    ShowToday,
                    m_CoreViewModel.BaseTheme);
            }

            ResourceChartPlotModel = plotModel ?? new PlotModel();
        }

        #endregion

        #region IKillSubscriptions Members

        public void KillSubscriptions()
        {
            m_BuildResourceChartPlotModelSub?.Dispose();
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
                m_AllocationMode?.Dispose();
                m_ScheduleMode?.Dispose();
                m_DisplayStyle?.Dispose();
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
