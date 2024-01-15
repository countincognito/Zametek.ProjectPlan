using Avalonia;
using Avalonia.Media;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using ReactiveUI;
using System.Data;
using System.Globalization;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public class GanttChartManagerViewModel
        : ToolViewModelBase, IGanttChartManagerViewModel, IDisposable
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

        private readonly IDisposable? m_BuildGanttChartPlotModelSub;

        private const double c_ExportLabelHeightCorrection = 1.2;

        #endregion

        #region Ctors

        public GanttChartManagerViewModel(
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
            m_GanttChartPlotModel = new PlotModel();

            {
                ReactiveCommand<Unit, Unit> saveGanttChartImageFileCommand = ReactiveCommand.CreateFromTask(SaveGanttChartImageFileAsync);
                SaveGanttChartImageFileCommand = saveGanttChartImageFileCommand;
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

            m_BuildGanttChartPlotModelSub = this
                .WhenAnyValue(
                    rcm => rcm.m_CoreViewModel.GraphCompilation,
                    rcm => rcm.m_CoreViewModel.ResourceSeriesSet,
                    rcm => rcm.m_CoreViewModel.ArrowGraphSettings,
                    rcm => rcm.m_CoreViewModel.ShowDates,
                    rcm => rcm.GroupByResource,
                    rcm => rcm.AnnotateResources,
                    rcm => rcm.m_CoreViewModel.ProjectStartDateTime)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(async result =>
                {
                    GanttChartPlotModel = await BuildGanttChartPlotModelAsync(
                        m_DateTimeCalculator,
                        result.Item1,
                        result.Item2,
                        result.Item3,
                        result.Item4,
                        result.Item5,
                        result.Item6,
                        result.Item7);
                });

            Id = Resource.ProjectPlan.Titles.Title_GanttChartView;
            Title = Resource.ProjectPlan.Titles.Title_GanttChartView;
        }

        #endregion

        #region Properties

        private PlotModel m_GanttChartPlotModel;
        public PlotModel GanttChartPlotModel
        {
            get
            {
                return m_GanttChartPlotModel;
            }
            private set
            {
                lock (m_Lock) this.RaiseAndSetIfChanged(ref m_GanttChartPlotModel, value);
            }
        }

        public object? ImageBounds { get; set; }

        #endregion

        #region Private Methods

        private async Task<PlotModel> BuildGanttChartPlotModelAsync(
            IDateTimeCalculator dateTimeCalculator,
            IGraphCompilation<int, int, IDependentActivity<int, int>> graphCompilation,
            ResourceSeriesSetModel resourceSeriesSet,
            ArrowGraphSettingsModel arrowGraphSettings,
            bool showDates,
            bool groupByResource,
            bool annotateResources,
            DateTime projectStartDateTime)
        {
            try
            {
                lock (m_Lock)
                {
                    return BuildGanttChartPlotModel(
                        dateTimeCalculator,
                        graphCompilation,
                        resourceSeriesSet,
                        arrowGraphSettings,
                        showDates,
                        groupByResource,
                        annotateResources,
                        projectStartDateTime);
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

        private static PlotModel BuildGanttChartPlotModel(
            IDateTimeCalculator dateTimeCalculator,
            IGraphCompilation<int, int, IDependentActivity<int, int>> graphCompilation,
            ResourceSeriesSetModel resourceSeriesSet,
            ArrowGraphSettingsModel arrowGraphSettings,
            bool showDates,
            bool groupByResource,
            bool annotateResources,
            DateTime projectStartDateTime)
        {
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            ArgumentNullException.ThrowIfNull(graphCompilation);
            ArgumentNullException.ThrowIfNull(resourceSeriesSet);
            ArgumentNullException.ThrowIfNull(arrowGraphSettings);
            var plotModel = new PlotModel();

            int finishTime = resourceSeriesSet.ResourceSchedules.Select(x => x.FinishTime).DefaultIfEmpty().Max();

            plotModel.Axes.Add(BuildResourceChartXAxis(dateTimeCalculator, finishTime, showDates, projectStartDateTime));

            var legend = new Legend
            {
                LegendBorder = OxyColors.Black,
                LegendBackground = OxyColor.FromAColor(200, OxyColors.White),
                LegendPosition = LegendPosition.RightMiddle,
                LegendPlacement = LegendPlacement.Outside,
            };

            plotModel.Legends.Add(legend);
            plotModel.IsLegendVisible = false;

            var colorFormatLookup = new SlackColorFormatLookup(arrowGraphSettings.ActivitySeverities);

            var series = new IntervalBarSeries
            {
                Title = Resource.ProjectPlan.Labels.Label_GanttChartSeries,
                LabelFormatString = @"",
                TrackerFormatString = $"{Resource.ProjectPlan.Labels.Label_Activity}: {{2}}\n{Resource.ProjectPlan.Labels.Label_Start}: {{4}}\n{Resource.ProjectPlan.Labels.Label_End}: {{5}}",
            };

            var labels = new List<string>();

            if (!graphCompilation.CompilationErrors.Any())
            {
                if (groupByResource)
                {
                    // Find all the resource series with at least 1 scheduled activity.

                    IEnumerable<ResourceSeriesModel> scheduledResourceSeries = resourceSeriesSet.Scheduled
                        .Where(x => x.ResourceSchedule.ScheduledActivities.Count != 0);

                    // Record the resource name, and the scheduled activities (in order).

                    var scheduledResourceActivitiesSet = new List<(string ResourceName, IList<ScheduledActivityModel> ScheduledActivities)>();

                    foreach (ResourceSeriesModel resourceSeries in scheduledResourceSeries)
                    {
                        IList<ScheduledActivityModel> orderedScheduledActivities = resourceSeries
                            .ResourceSchedule.ScheduledActivities
                            .OrderByDescending(x => x.StartTime)
                            .ToList();
                        scheduledResourceActivitiesSet.Add(
                            (resourceSeries.Title, orderedScheduledActivities));
                    }

                    // Order the set according to the start times of the first activity for each resource.

                    IList<(string, IList<ScheduledActivityModel>)> orderedScheduledResourceActivitiesSet = scheduledResourceActivitiesSet
                        .OrderByDescending(x => x.ScheduledActivities.OrderBy(y => y.StartTime)
                            .FirstOrDefault()?.StartTime ?? 0)
                        .ThenBy(x => x.ScheduledActivities.OrderBy(y => y.StartTime)
                            .LastOrDefault()?.FinishTime ?? 0)
                        .ToList();

                    foreach ((string resourceName, IList<ScheduledActivityModel> scheduledActivities) in orderedScheduledResourceActivitiesSet)
                    {
                        IEnumerable<ScheduledActivityModel> orderedScheduledActivities = scheduledActivities;
                        IDictionary<int, IDependentActivity<int, int>> activityLookup = graphCompilation.DependentActivities.ToDictionary(x => x.Id);

                        int resourceStartTime = orderedScheduledActivities.LastOrDefault()?.StartTime ?? 0;
                        int resourceFinishTime = orderedScheduledActivities.FirstOrDefault()?.FinishTime ?? 0;
                        int minimumY = labels.Count;

                        // Add an extra row for padding.
                        // IntervalBarItems are added in reverse order to how they will be displayed.
                        // So, this item will appear at the bottom of the grouping.

                        series.Items.Add(new IntervalBarItem { Start = -1, End = -1 });
                        labels.Add(string.Empty);

                        // Now add the scheduled activities (again, in reverse display order).

                        foreach (ScheduledActivityModel scheduledActivity in orderedScheduledActivities)
                        {
                            if (activityLookup.TryGetValue(scheduledActivity.Id, out IDependentActivity<int, int>? activity))
                            {
                                if (activity.EarliestStartTime.HasValue
                                    && activity.EarliestFinishTime.HasValue
                                    && activity.Duration > 0)
                                {
                                    string id = activity.Id.ToString(CultureInfo.InvariantCulture);
                                    string label = string.IsNullOrWhiteSpace(activity.Name) ? id : $"{activity.Name} ({id})";
                                    Color slackColor = colorFormatLookup.FindSlackColor(activity.TotalSlack);

                                    var backgroundColor = OxyColor.FromArgb(
                                          slackColor.A,
                                          slackColor.R,
                                          slackColor.G,
                                          slackColor.B);

                                    var item = new IntervalBarItem
                                    {
                                        Title = label,
                                        Start = ChartHelper.CalculateChartTimeXValue(activity.EarliestStartTime.GetValueOrDefault(), showDates, projectStartDateTime, dateTimeCalculator),
                                        End = ChartHelper.CalculateChartTimeXValue(activity.EarliestFinishTime.GetValueOrDefault(), showDates, projectStartDateTime, dateTimeCalculator),
                                        Color = backgroundColor,
                                    };

                                    series.Items.Add(item);
                                    labels.Add(label);
                                }
                            }
                        }

                        int maximumY = labels.Count;

                        if (annotateResources)
                        {
                            plotModel.Annotations.Add(
                                 new OxyPlot.Annotations.RectangleAnnotation
                                 {
                                     MinimumX = ChartHelper.CalculateChartTimeXValue(resourceStartTime, showDates, projectStartDateTime, dateTimeCalculator),
                                     MaximumX = ChartHelper.CalculateChartTimeXValue(resourceFinishTime, showDates, projectStartDateTime, dateTimeCalculator),
                                     MinimumY = minimumY,
                                     MaximumY = maximumY,
                                     ToolTip = resourceName,
                                     Fill = OxyColor.FromAColor(10, OxyColors.Blue),
                                     Stroke = OxyColors.Black,
                                     StrokeThickness = 1
                                 });
                        }
                    }

                    // Add an extra row for padding.
                    // This item will appear at the top of the grouping.
                    series.Items.Add(new IntervalBarItem { Start = -1, End = -1 });
                    labels.Add(string.Empty);
                }
                else
                {
                    // Add all the activities (in reverse display order).

                    IOrderedEnumerable<IDependentActivity<int, int>> orderedActivities = graphCompilation
                        .DependentActivities
                        .OrderByDescending(x => x.EarliestStartTime)
                        .ThenByDescending(x => x.TotalSlack);

                    foreach (IDependentActivity<int, int> activity in orderedActivities)
                    {
                        if (activity.EarliestStartTime.HasValue
                            && activity.EarliestFinishTime.HasValue
                            && activity.Duration > 0)
                        {
                            string id = activity.Id.ToString(CultureInfo.InvariantCulture);
                            string label = string.IsNullOrWhiteSpace(activity.Name) ? id : $"{activity.Name} ({id})";
                            Color slackColor = colorFormatLookup.FindSlackColor(activity.TotalSlack);

                            var backgroundColor = OxyColor.FromArgb(
                                  slackColor.A,
                                  slackColor.R,
                                  slackColor.G,
                                  slackColor.B);

                            var item = new IntervalBarItem
                            {
                                Title = label,
                                Start = ChartHelper.CalculateChartTimeXValue(activity.EarliestStartTime.GetValueOrDefault(), showDates, projectStartDateTime, dateTimeCalculator),
                                End = ChartHelper.CalculateChartTimeXValue(activity.EarliestFinishTime.GetValueOrDefault(), showDates, projectStartDateTime, dateTimeCalculator),
                                Color = backgroundColor,
                            };

                            series.Items.Add(item);
                            labels.Add(label);
                        }
                    }
                }
            }

            plotModel.Axes.Add(BuildResourceChartYAxis(labels));
            plotModel.Series.Add(series);
            return plotModel;
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
                double minValue = ChartHelper.CalculateChartTimeXValue(-1, showDates, projectStartDateTime, dateTimeCalculator);
                double maxValue = ChartHelper.CalculateChartTimeXValue(finishTime + 1, showDates, projectStartDateTime, dateTimeCalculator);

                if (showDates)
                {
                    return new DateTimeAxis
                    {
                        Position = AxisPosition.Bottom,
                        AbsoluteMinimum = minValue,
                        AbsoluteMaximum = maxValue,
                        MajorGridlineStyle = LineStyle.Solid,
                        MinorGridlineStyle = LineStyle.Dot,
                        MaximumPadding = 0.1,
                        MinimumPadding = 0.1,
                        Title = Resource.ProjectPlan.Labels.Label_TimeAxisTitle,
                        StringFormat = DateTimeCalculator.DateFormat
                    };
                }

                return new LinearAxis
                {
                    Position = AxisPosition.Bottom,
                    AbsoluteMinimum = minValue,
                    AbsoluteMaximum = maxValue,
                    MajorGridlineStyle = LineStyle.Solid,
                    MinorGridlineStyle = LineStyle.Dot,
                    MaximumPadding = 0.1,
                    MinimumPadding = 0.1,
                    Title = Resource.ProjectPlan.Labels.Label_TimeAxisTitle
                };
            }
            return new LinearAxis
            {
                Position = AxisPosition.Bottom,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                MaximumPadding = 0.1,
                MinimumPadding = 0.1,
                Title = Resource.ProjectPlan.Labels.Label_TimeAxisTitle
            };
        }

        private static CategoryAxis BuildResourceChartYAxis(IEnumerable<string> labels)
        {
            ArgumentNullException.ThrowIfNull(labels);
            var categoryAxis = new CategoryAxis
            {
                Position = AxisPosition.Left,
                AbsoluteMinimum = -1,
                AbsoluteMaximum = labels.Count(),
                Title = Resource.ProjectPlan.Labels.Label_GanttAxisTitle,
            };
            categoryAxis.Labels.AddRange(labels);
            return categoryAxis;
        }

        private async Task SaveGanttChartImageFileInternalAsync(string? filename)
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

                    int width = Math.Abs(Convert.ToInt32(bounds.Width));
                    int height = 0;
                    int boundedHeight = Math.Abs(Convert.ToInt32(bounds.Height));

                    if (GanttChartPlotModel.DefaultYAxis is CategoryAxis yAxis)
                    {
                        int labelCount = yAxis.ActualLabels.Count;
                        height = Convert.ToInt32(GanttChartPlotModel.DefaultFontSize * labelCount * c_ExportLabelHeightCorrection);
                    }

                    if (height <= boundedHeight)
                    {
                        height = boundedHeight;
                    }

                    fileExtension.ValueSwitchOn()
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImageJpegFileExtension}", _ =>
                        {
                            using var stream = File.OpenWrite(filename);
                            OxyPlot.SkiaSharp.JpegExporter.Export(
                                GanttChartPlotModel,
                                stream,
                                width,
                                height,
                                200);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImagePngFileExtension}", _ =>
                        {
                            // Use Avalonia exporter so the background can be white.
                            using var stream = File.OpenWrite(filename);
                            OxyPlot.Avalonia.PngExporter.Export(
                                GanttChartPlotModel,
                                stream,
                                width,
                                height,
                                OxyColors.White);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_PdfFileExtension}", _ =>
                        {
                            using var stream = File.OpenWrite(filename);
                            OxyPlot.SkiaSharp.PdfExporter.Export(
                                GanttChartPlotModel,
                                stream,
                                width,
                                height);
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

        private async Task SaveGanttChartImageFileAsync()
        {
            try
            {
                string projectTitle = m_SettingService.ProjectTitle;
                string directory = m_SettingService.ProjectDirectory;
                string? filename = await m_DialogService.ShowSaveFileDialogAsync(projectTitle, directory, s_ExportFileFilters);

                if (!string.IsNullOrWhiteSpace(filename))
                {
                    await SaveGanttChartImageFileInternalAsync(filename);
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

        #region IGanttChartManagerViewModel Members

        private readonly ObservableAsPropertyHelper<bool> m_IsBusy;
        public bool IsBusy => m_IsBusy.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasStaleOutputs;
        public bool HasStaleOutputs => m_HasStaleOutputs.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasCompilationErrors;
        public bool HasCompilationErrors => m_HasCompilationErrors.Value;

        private bool m_GroupByResource;
        public bool GroupByResource
        {
            get => m_GroupByResource;
            set => this.RaiseAndSetIfChanged(ref m_GroupByResource, value);
        }

        private bool m_AnnotateResources;
        public bool AnnotateResources
        {
            get => m_AnnotateResources;
            set => this.RaiseAndSetIfChanged(ref m_AnnotateResources, value);
        }

        public ICommand SaveGanttChartImageFileCommand { get; }

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
                m_BuildGanttChartPlotModelSub?.Dispose();
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
