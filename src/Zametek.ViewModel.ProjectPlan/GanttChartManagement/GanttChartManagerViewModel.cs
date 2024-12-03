using Avalonia;
using Avalonia.Media;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using ReactiveUI;
using System.Data;
using System.Globalization;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public class GanttChartManagerViewModel
        : ToolViewModelBase, IGanttChartManagerViewModel
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

        private readonly IDisposable? m_BuildGanttChartPlotModelSub;

        private const double c_ExportLabelHeightCorrection = 1.2;
        private const double c_YAxisMinimum = -1.0;
        private const double c_TrackerAnnotationMinCorrection = -1.25;
        private const double c_TrackerAnnotationMaxCorrection = -0.75;

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

            m_GroupByMode = this
                .WhenAnyValue(rcm => rcm.m_CoreViewModel.GanttChartGroupByMode)
                .ToProperty(this, rcm => rcm.GroupByMode);

            m_AnnotationStyle = this
                .WhenAnyValue(rcm => rcm.m_CoreViewModel.GanttChartAnnotationStyle)
                .ToProperty(this, rcm => rcm.AnnotationStyle);

            m_LabelGroups = this
                .WhenAnyValue(rcm => rcm.m_CoreViewModel.ViewGanttChartGroupLabels)
                .ToProperty(this, rcm => rcm.LabelGroups);

            m_ShowProjectFinish = this
                .WhenAnyValue(rcm => rcm.m_CoreViewModel.ViewGanttChartProjectFinish)
                .ToProperty(this, rcm => rcm.ShowProjectFinish);

            m_ShowTracking = this
                .WhenAnyValue(rcm => rcm.m_CoreViewModel.ViewGanttChartTracking)
                .ToProperty(this, rcm => rcm.ShowTracking);

            m_IsGrouped = this
                .WhenAnyValue(
                    rcm => rcm.GroupByMode,
                    (groupByMode) => groupByMode != GroupByMode.None)
                .ToProperty(this, rcm => rcm.IsGrouped);

            m_IsAnnotated = this
                .WhenAnyValue(
                    rcm => rcm.IsGrouped,
                    rcm => rcm.AnnotationStyle,
                    (isGrouped, annotationStyle) => isGrouped && annotationStyle != AnnotationStyle.None)
                .ToProperty(this, rcm => rcm.IsAnnotated);

            m_BuildGanttChartPlotModelSub = this
                .WhenAnyValue(
                    rcm => rcm.m_CoreViewModel.ResourceSeriesSet,
                    rcm => rcm.m_CoreViewModel.ResourceSettings,
                    rcm => rcm.m_CoreViewModel.ArrowGraphSettings,
                    //rcm => rcm.m_CoreViewModel.WorkStreamSettings,
                    rcm => rcm.m_CoreViewModel.ProjectStartDateTime,
                    rcm => rcm.m_CoreViewModel.ShowDates,
                    rcm => rcm.m_CoreViewModel.UseClassicDates,
                    //rcm => rcm.m_CoreViewModel.GraphCompilation,
                    rcm => rcm.m_CoreViewModel.BaseTheme,
                    rcm => rcm.GroupByMode,
                    rcm => rcm.AnnotationStyle,
                    rcm => rcm.LabelGroups,
                    rcm => rcm.ShowProjectFinish,
                    rcm => rcm.ShowTracking,
                    (a, b, c, d, e, f, g, h, i, j, k, l) => (a, b, c, d, e, f, g, h, i, j, k, l)) // Do this as a workaround because WhenAnyValue cannot handle this many individual inputs.
                .ObserveOn(Scheduler.CurrentThread)
                .Subscribe(async _ => await BuildGanttChartPlotModelAsync());

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

        private readonly ObservableAsPropertyHelper<bool> m_IsGrouped;
        public bool IsGrouped => m_IsGrouped.Value;

        private readonly ObservableAsPropertyHelper<bool> m_IsAnnotated;
        public bool IsAnnotated => m_IsAnnotated.Value;

        public object? ImageBounds { get; set; }

        #endregion

        #region Private Methods

        private async Task BuildGanttChartPlotModelAsync()
        {
            try
            {
                lock (m_Lock)
                {
                    BuildGanttChartPlotModel();
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

        private static PlotModel BuildGanttChartPlotModelInternal(
            IDateTimeCalculator dateTimeCalculator,
            ResourceSeriesSetModel resourceSeriesSet,
            ResourceSettingsModel resourceSettingsSettings,
            ArrowGraphSettingsModel arrowGraphSettings,
            WorkStreamSettingsModel workStreamSettings,
            DateTime projectStartDateTime,
            bool showDates,
            IGraphCompilation<int, int, int, IDependentActivity> graphCompilation,
            GroupByMode groupByMode,
            AnnotationStyle annotationStyle,
            bool labelGroups,
            bool showProjectFinish,
            bool showTracking,
            BaseTheme baseTheme)
        {
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            ArgumentNullException.ThrowIfNull(resourceSeriesSet);
            ArgumentNullException.ThrowIfNull(resourceSettingsSettings);
            ArgumentNullException.ThrowIfNull(arrowGraphSettings);
            ArgumentNullException.ThrowIfNull(workStreamSettings);
            ArgumentNullException.ThrowIfNull(graphCompilation);
            var plotModel = new PlotModel();

            if (!graphCompilation.DependentActivities.Any())
            {
                return plotModel.SetBaseTheme(baseTheme);
            }

            int finishTime = resourceSeriesSet.ResourceSchedules.Select(x => x.FinishTime).DefaultIfEmpty().Max();

            plotModel.Axes.Add(BuildResourceChartXAxis(dateTimeCalculator, finishTime, showDates, projectStartDateTime));

            var legend = new Legend
            {
                LegendBorder = OxyColors.Black,
                LegendBackground = OxyColors.Transparent,
                //LegendTextColor = OxyColors.Black,
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
                switch (groupByMode)
                {
                    case GroupByMode.None:
                        {
                            // Add an extra row for padding.
                            // IntervalBarItems are added in reverse order to how they will be displayed.
                            // So, this item will appear at the bottom of the grouping.

                            series.Items.Add(new IntervalBarItem { Start = -1, End = -1 });
                            labels.Add(string.Empty);

                            // Add all the activities (in reverse display order).

                            IOrderedEnumerable<IDependentActivity> orderedActivities = graphCompilation
                                .DependentActivities
                                .OrderByDescending(x => x.EarliestStartTime)
                                .ThenByDescending(x => x.TotalSlack);

                            foreach (IDependentActivity activity in orderedActivities)
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

                                    double start = ChartHelper.CalculateChartTimeXValue(activity.EarliestStartTime.GetValueOrDefault(), showDates, projectStartDateTime, dateTimeCalculator);
                                    double end = ChartHelper.CalculateChartTimeXValue(activity.EarliestFinishTime.GetValueOrDefault(), showDates, projectStartDateTime, dateTimeCalculator);

                                    var item = new IntervalBarItem
                                    {
                                        Title = label,
                                        Start = start,
                                        End = end,
                                        Color = backgroundColor,
                                    };

                                    series.Items.Add(item);
                                    labels.Add(label);

                                    if (showTracking)
                                    {
                                        int labelCount = labels.Count;

                                        // Get the tracker with the highest Time value.
                                        ActivityTrackerModel? lastTracker = activity.Trackers.LastOrDefault();
                                        RectangleAnnotation? trackerAnnotation = TrackerAnnotation(start, end, labelCount, lastTracker);

                                        if (trackerAnnotation is not null)
                                        {
                                            plotModel.Annotations.Add(trackerAnnotation);
                                        }
                                    }
                                }
                            }

                            // Add an extra row for padding.
                            // This item will appear at the top of the grouping.
                            series.Items.Add(new IntervalBarItem { Start = -1, End = -1 });
                            labels.Add(string.Empty);
                        }

                        break;
                    case GroupByMode.Resource:
                        {
                            // Find all the resource series with at least 1 scheduled activity.

                            IEnumerable<ResourceSeriesModel> scheduledResourceSeries = resourceSeriesSet.Scheduled
                                .Where(x => x.ResourceSchedule.ScheduledActivities.Count != 0);

                            // Record the resource name, and the scheduled activities (in order).

                            var scheduledResourceActivitiesSet =
                                new List<(string ResourceName, ColorFormatModel ColorFormat, int DisplayOrder, IList<ScheduledActivityModel> ScheduledActivities)>();

                            foreach (ResourceSeriesModel resourceSeries in scheduledResourceSeries)
                            {
                                IList<ScheduledActivityModel> orderedScheduledActivities = [.. resourceSeries
                                    .ResourceSchedule.ScheduledActivities
                                    .OrderByDescending(x => x.StartTime)];
                                scheduledResourceActivitiesSet.Add(
                                    (resourceSeries.Title, resourceSeries.ColorFormat, resourceSeries.DisplayOrder, orderedScheduledActivities));
                            }

                            // Order the set according to the display order, followed by the start times of the first activity for each resource.

                            IList<(string, ColorFormatModel, int, IList<ScheduledActivityModel>)> orderedScheduledResourceActivitiesSet = scheduledResourceActivitiesSet
                                .OrderBy(x => x.DisplayOrder)
                                .ThenBy(x => x.ScheduledActivities.OrderBy(y => y.StartTime)
                                    .FirstOrDefault()?.StartTime ?? 0)
                                .ThenByDescending(x => x.ScheduledActivities.OrderBy(y => y.FinishTime)
                                    .LastOrDefault()?.FinishTime ?? 0)
                                .ToList();

                            foreach ((string resourceName, ColorFormatModel colorFormat, int displayOrder, IList<ScheduledActivityModel> scheduledActivities) in orderedScheduledResourceActivitiesSet)
                            {
                                IEnumerable<ScheduledActivityModel> orderedScheduledActivities = scheduledActivities;
                                Dictionary<int, IDependentActivity> activityLookup = graphCompilation.DependentActivities.ToDictionary(x => x.Id);

                                int resourceStartTime = orderedScheduledActivities.Select(x => x.StartTime).DefaultIfEmpty().Min();
                                int resourceFinishTime = orderedScheduledActivities.Select(x => x.FinishTime).DefaultIfEmpty().Max();
                                int minimumY = labels.Count;

                                // Add an extra row for padding.
                                // IntervalBarItems are added in reverse order to how they will be displayed.
                                // So, this item will appear at the bottom of the grouping.

                                series.Items.Add(new IntervalBarItem { Start = -1, End = -1 });
                                labels.Add(string.Empty);

                                // Now add the scheduled activities (again, in reverse display order).

                                foreach (ScheduledActivityModel scheduledActivity in orderedScheduledActivities)
                                {
                                    if (activityLookup.TryGetValue(scheduledActivity.Id, out IDependentActivity? activity))
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

                                            double start = ChartHelper.CalculateChartTimeXValue(activity.EarliestStartTime.GetValueOrDefault(), showDates, projectStartDateTime, dateTimeCalculator);
                                            double end = ChartHelper.CalculateChartTimeXValue(activity.EarliestFinishTime.GetValueOrDefault(), showDates, projectStartDateTime, dateTimeCalculator);

                                            var item = new IntervalBarItem
                                            {
                                                Title = label,
                                                Start = start,
                                                End = end,
                                                Color = backgroundColor,
                                            };

                                            series.Items.Add(item);
                                            labels.Add(label);

                                            if (showTracking)
                                            {
                                                int labelCount = labels.Count;

                                                // Get the tracker with the highest Time value.
                                                ActivityTrackerModel? lastTracker = activity.Trackers.LastOrDefault();
                                                RectangleAnnotation? trackerAnnotation = TrackerAnnotation(start, end, labelCount, lastTracker);

                                                if (trackerAnnotation is not null)
                                                {
                                                    plotModel.Annotations.Add(trackerAnnotation);
                                                }
                                            }
                                        }
                                    }
                                }

                                int maximumY = labels.Count;

                                switch (annotationStyle)
                                {
                                    case AnnotationStyle.None:
                                        break;
                                    case AnnotationStyle.Plain or AnnotationStyle.Color:
                                        {
                                            OxyColor resourceColor = OxyColors.Blue;
                                            byte aLevel = ColorHelper.AnnotationATransparent;

                                            if (annotationStyle == AnnotationStyle.Color)
                                            {
                                                resourceColor = OxyColor.FromArgb(
                                                    colorFormat.A,
                                                    colorFormat.R,
                                                    colorFormat.G,
                                                    colorFormat.B);
                                                aLevel = ColorHelper.AnnotationALight;
                                            }

                                            OxyColor fillColor = OxyColor.FromAColor(aLevel, resourceColor);

                                            double minimumX = ChartHelper.CalculateChartTimeXValue(resourceStartTime, showDates, projectStartDateTime, dateTimeCalculator);
                                            double maximumX = ChartHelper.CalculateChartTimeXValue(resourceFinishTime, showDates, projectStartDateTime, dateTimeCalculator);

                                            var annotation = new RectangleAnnotation
                                            {
                                                MinimumX = minimumX,
                                                MaximumX = maximumX,
                                                MinimumY = minimumY,
                                                MaximumY = maximumY,
                                                ToolTip = resourceName,
                                                Fill = fillColor,
                                                Stroke = resourceColor,
                                                StrokeThickness = 1,
                                                Layer = AnnotationLayer.BelowSeries,
                                            };

                                            if (labelGroups)
                                            {
                                                annotation.Text = resourceName;
                                                annotation.TextPosition = new DataPoint(minimumX, maximumY);
                                                annotation.TextHorizontalAlignment = HorizontalAlignment.Left;
                                                annotation.TextVerticalAlignment = VerticalAlignment.Bottom;

                                                series.Items.Add(new IntervalBarItem { Start = -1, End = -1 });
                                                labels.Add(string.Empty);
                                            }

                                            plotModel.Annotations.Add(annotation);
                                        }

                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException(nameof(annotationStyle));
                                }
                            }

                            // Add an extra row for padding.
                            // This item will appear at the top of the grouping.
                            series.Items.Add(new IntervalBarItem { Start = -1, End = -1 });
                            labels.Add(string.Empty);
                        }

                        break;
                    case GroupByMode.WorkStream:
                        {
                            // Pivot the scheduled activities so that they are grouped by work stream IDs.

                            // Gather all the used work stream

                            var workStreamLookup = new Dictionary<int, WorkStreamModel>();

                            // Include a catch-all work stream as default.

                            workStreamLookup.TryAdd(
                                default,
                                new()
                                {
                                    Id = default,
                                    Name = Resource.ProjectPlan.Labels.Label_DefaultWorkStream,
                                    ColorFormat = ColorHelper.Black(),
                                    DisplayOrder = -1
                                });

                            foreach (WorkStreamModel workStream in workStreamSettings.WorkStreams)
                            {
                                workStreamLookup.TryAdd(workStream.Id, workStream);
                            }

                            // Go through all the activities (in reverse display order).

                            IOrderedEnumerable<IDependentActivity> orderedActivities = graphCompilation
                                .DependentActivities
                                .OrderByDescending(x => x.EarliestStartTime)
                                .ThenByDescending(x => x.TotalSlack);

                            // Create a scheduled activity lookup.

                            Dictionary<int, ScheduledActivityModel> scheduledActivityLookup = resourceSeriesSet.Scheduled
                                .SelectMany(x => x.ResourceSchedule.ScheduledActivities)
                                .DistinctBy(x => x.Id)
                                .ToDictionary(x => x.Id);

                            // Split the activities according to work stream ID.

                            Dictionary<int, IList<ScheduledActivityModel>> activitiesByWorkStream = [];

                            foreach (IDependentActivity activity in orderedActivities)
                            {
                                if (!scheduledActivityLookup.ContainsKey(activity.Id))
                                {
                                    continue;
                                }

                                ScheduledActivityModel scheduledActivity = scheduledActivityLookup[activity.Id];

                                HashSet<int> targetWorkStreams = activity.TargetWorkStreams;

                                // If no work streams are targetted, then use the default.

                                if (targetWorkStreams.Count == 0)
                                {
                                    targetWorkStreams.Add(default);
                                }

                                // Cycle through each work stream and add the scheduled activity where required.

                                foreach (int workStreamId in targetWorkStreams)
                                {
                                    if (!activitiesByWorkStream.TryGetValue(workStreamId, out IList<ScheduledActivityModel>? scheduledActivities))
                                    {
                                        scheduledActivities = [];
                                        activitiesByWorkStream.Add(workStreamId, scheduledActivities);
                                    }

                                    scheduledActivities.Add(scheduledActivity);
                                }
                            }

                            // Check all the work stream IDs that are used can be found in the lookup.
                            // Otherwise return early.

                            if (!activitiesByWorkStream.Keys.ToHashSet().IsSubsetOf(workStreamLookup.Keys))
                            {
                                return plotModel.SetBaseTheme(baseTheme);
                            }

                            // Order the set according to display order, followed by the start times of the first activity for each work stream.

                            IList<(int WorkStreamId, IList<ScheduledActivityModel>)> orderedActivitiesByWorkStream = activitiesByWorkStream
                                .OrderBy(x => workStreamLookup[x.Key].DisplayOrder)
                                .ThenBy(x => x.Value.OrderBy(y => y.StartTime)
                                    .FirstOrDefault()?.StartTime ?? 0)
                                .ThenByDescending(x => x.Value.OrderBy(y => y.FinishTime)
                                    .LastOrDefault()?.FinishTime ?? 0)
                                .Select(x => (x.Key, x.Value))
                                .ToList();

                            foreach ((int workStreamId, IList<ScheduledActivityModel> scheduledActivities) in orderedActivitiesByWorkStream)
                            {
                                IEnumerable<ScheduledActivityModel> orderedScheduledActivities = scheduledActivities;
                                Dictionary<int, IDependentActivity> activityLookup = graphCompilation.DependentActivities.ToDictionary(x => x.Id);

                                int workStreamStartTime = orderedScheduledActivities.Select(x => x.StartTime).DefaultIfEmpty().Min();
                                int workStreamFinishTime = orderedScheduledActivities.Select(x => x.FinishTime).DefaultIfEmpty().Max();
                                int minimumY = labels.Count;

                                // Add an extra row for padding.
                                // IntervalBarItems are added in reverse order to how they will be displayed.
                                // So, this item will appear at the bottom of the grouping.

                                series.Items.Add(new IntervalBarItem { Start = -1, End = -1 });
                                labels.Add(string.Empty);

                                // Now add the scheduled activities (again, in reverse display order).

                                foreach (ScheduledActivityModel scheduledActivity in orderedScheduledActivities)
                                {
                                    if (activityLookup.TryGetValue(scheduledActivity.Id, out IDependentActivity? activity))
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

                                            double start = ChartHelper.CalculateChartTimeXValue(activity.EarliestStartTime.GetValueOrDefault(), showDates, projectStartDateTime, dateTimeCalculator);
                                            double end = ChartHelper.CalculateChartTimeXValue(activity.EarliestFinishTime.GetValueOrDefault(), showDates, projectStartDateTime, dateTimeCalculator);

                                            var item = new IntervalBarItem
                                            {
                                                Title = label,
                                                Start = start,
                                                End = end,
                                                Color = backgroundColor,
                                            };

                                            series.Items.Add(item);
                                            labels.Add(label);

                                            if (showTracking)
                                            {
                                                int labelCount = labels.Count;

                                                // Get the tracker with the highest Time value.
                                                ActivityTrackerModel? lastTracker = activity.Trackers.LastOrDefault();
                                                RectangleAnnotation? trackerAnnotation = TrackerAnnotation(start, end, labelCount, lastTracker);

                                                if (trackerAnnotation is not null)
                                                {
                                                    plotModel.Annotations.Add(trackerAnnotation);
                                                }
                                            }
                                        }
                                    }
                                }

                                int maximumY = labels.Count;

                                switch (annotationStyle)
                                {
                                    case AnnotationStyle.None:
                                        break;
                                    case AnnotationStyle.Plain or AnnotationStyle.Color:
                                        {
                                            OxyColor workStreamColor = OxyColors.Blue;
                                            byte aLevel = ColorHelper.AnnotationATransparent;

                                            WorkStreamModel workStreamModel = workStreamLookup[workStreamId];

                                            if (annotationStyle == AnnotationStyle.Color)
                                            {
                                                workStreamColor = OxyColor.FromArgb(
                                                    workStreamModel.ColorFormat.A,
                                                    workStreamModel.ColorFormat.R,
                                                    workStreamModel.ColorFormat.G,
                                                    workStreamModel.ColorFormat.B);
                                                aLevel = ColorHelper.AnnotationALight;
                                            }

                                            double minimumX = ChartHelper.CalculateChartTimeXValue(workStreamStartTime, showDates, projectStartDateTime, dateTimeCalculator);
                                            double maximumX = ChartHelper.CalculateChartTimeXValue(workStreamFinishTime, showDates, projectStartDateTime, dateTimeCalculator);

                                            var annotation = new RectangleAnnotation
                                            {
                                                MinimumX = minimumX,
                                                MaximumX = maximumX,
                                                MinimumY = minimumY,
                                                MaximumY = maximumY,
                                                ToolTip = workStreamModel.Name,
                                                Fill = OxyColor.FromAColor(aLevel, workStreamColor),
                                                Stroke = workStreamColor,
                                                StrokeThickness = 1,
                                                Layer = AnnotationLayer.BelowSeries,
                                            };

                                            if (labelGroups)
                                            {
                                                annotation.Text = workStreamModel.Name;
                                                annotation.TextPosition = new DataPoint(minimumX, maximumY);
                                                annotation.TextHorizontalAlignment = HorizontalAlignment.Left;
                                                annotation.TextVerticalAlignment = VerticalAlignment.Bottom;

                                                series.Items.Add(new IntervalBarItem { Start = -1, End = -1 });
                                                labels.Add(string.Empty);
                                            }

                                            plotModel.Annotations.Add(annotation);
                                        }

                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException(nameof(annotationStyle));
                                }
                            }

                            // Add an extra row for padding.
                            // This item will appear at the top of the grouping.
                            series.Items.Add(new IntervalBarItem { Start = -1, End = -1 });
                            labels.Add(string.Empty);
                        }

                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(groupByMode));
                }

                if (showProjectFinish)
                {
                    var projectFinish = new StringBuilder(Resource.ProjectPlan.Labels.Label_ProjectFinish);
                    projectFinish.Append(' ');

                    if (showDates)
                    {
                        projectFinish.Append(
                            dateTimeCalculator.DisplayFinishDate(
                                dateTimeCalculator.AddDays(
                                    projectStartDateTime,
                                    finishTime),
                                dateTimeCalculator.AddDays(
                                    projectStartDateTime,
                                    finishTime),
                                1).ToString(DateTimeCalculator.DateFormat));
                    }
                    else
                    {
                        projectFinish.Append(finishTime);
                    }

                    double finishTimeX = ChartHelper.CalculateChartTimeXValue(finishTime, showDates, projectStartDateTime, dateTimeCalculator);
                    double finishTimeY = labels.Count;

                    var finishTimeAnnotation = new RectangleAnnotation
                    {
                        Text = projectFinish.ToString(),
                        TextPosition = new DataPoint(finishTimeX, finishTimeY),
                        TextHorizontalAlignment = HorizontalAlignment.Right,
                        TextVerticalAlignment = VerticalAlignment.Top,
                        StrokeThickness = 0,
                        Fill = OxyColors.Transparent,
                        Layer = AnnotationLayer.BelowSeries,
                    };

                    plotModel.Annotations.Add(finishTimeAnnotation);
                }
            }

            plotModel.Axes.Add(BuildResourceChartYAxis(labels));
            plotModel.Series.Add(series);

            if (plotModel is IPlotModel plotModelInterface)
            {
                plotModelInterface.Update(true);
            }

            return plotModel.SetBaseTheme(baseTheme);
        }

        private static RectangleAnnotation? TrackerAnnotation(
            double start,
            double end,
            int labelCount,
            ActivityTrackerModel? tracker)
        {
            if (tracker is null)
            {
                return null;
            }
            OxyColor strokeColor = OxyColors.Black;
            OxyColor fillColor = OxyColor.FromAColor(ColorHelper.AnnotationATrackerOverlay, OxyColors.White);

            double maxX = start + ((end - start) * tracker.PercentageComplete / 100);
            double minY = labelCount + c_TrackerAnnotationMinCorrection;
            double maxY = labelCount + c_TrackerAnnotationMaxCorrection;

            return new RectangleAnnotation
            {
                MinimumX = start,
                MaximumX = maxX,
                MinimumY = minY,
                MaximumY = maxY,
                Fill = fillColor,
                Stroke = strokeColor,
                StrokeThickness = 1,
                Layer = AnnotationLayer.AboveSeries,
            };
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
                        Minimum = minValue,
                        AbsoluteMaximum = maxValue,
                        Maximum = maxValue,
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
                    Minimum = minValue,
                    AbsoluteMaximum = maxValue,
                    Maximum = maxValue,
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

        private static CategoryAxis BuildResourceChartYAxis(IList<string> labels)
        {
            ArgumentNullException.ThrowIfNull(labels);

            double minValue = c_YAxisMinimum;
            double maxValue = labels.Count;

            var categoryAxis = new CategoryAxis
            {
                Position = AxisPosition.Left,
                AbsoluteMinimum = minValue,
                Minimum = minValue,
                AbsoluteMaximum = maxValue,
                Maximum = maxValue,
                Title = Resource.ProjectPlan.Labels.Label_GanttAxisTitle,
            };
            categoryAxis.Labels.AddRange(labels);
            return categoryAxis;
        }

        private async Task SaveGanttChartImageFileAsync()
        {
            try
            {
                string projectTitle = m_SettingService.ProjectTitle;
                string directory = m_SettingService.ProjectDirectory;
                string? filename = await m_DialogService.ShowSaveFileDialogAsync(projectTitle, directory, s_ExportFileFilters);

                if (!string.IsNullOrWhiteSpace(filename)
                    && ImageBounds is Rect bounds)
                {
                    int boundedWidth = Math.Abs(Convert.ToInt32(bounds.Width));
                    int boundedHeight = Math.Abs(Convert.ToInt32(bounds.Height));

                    await SaveGanttChartImageFileAsync(filename, boundedWidth, boundedHeight);
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

        #region IGanttChartManagerViewModel Members

        private readonly ObservableAsPropertyHelper<bool> m_IsBusy;
        public bool IsBusy => m_IsBusy.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasStaleOutputs;
        public bool HasStaleOutputs => m_HasStaleOutputs.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasCompilationErrors;
        public bool HasCompilationErrors => m_HasCompilationErrors.Value;

        private readonly ObservableAsPropertyHelper<GroupByMode> m_GroupByMode;
        public GroupByMode GroupByMode
        {
            get => m_GroupByMode.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.GanttChartGroupByMode = value;
            }
        }

        private readonly ObservableAsPropertyHelper<AnnotationStyle> m_AnnotationStyle;
        public AnnotationStyle AnnotationStyle
        {
            get => m_AnnotationStyle.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.GanttChartAnnotationStyle = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_LabelGroups;
        public bool LabelGroups
        {
            get => m_LabelGroups.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.ViewGanttChartGroupLabels = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_ShowProjectFinish;
        public bool ShowProjectFinish
        {
            get => m_ShowProjectFinish.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.ViewGanttChartProjectFinish = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_ShowTracking;
        public bool ShowTracking
        {
            get => m_ShowTracking.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.ViewGanttChartTracking = value;
            }
        }

        public ICommand SaveGanttChartImageFileCommand { get; }

        public async Task SaveGanttChartImageFileAsync(
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
                    int calculatedHeight = 0;

                    if (GanttChartPlotModel.DefaultYAxis is CategoryAxis yAxis)
                    {
                        int labelCount = yAxis.ActualLabels.Count;
                        calculatedHeight = Convert.ToInt32(GanttChartPlotModel.DefaultFontSize * labelCount * c_ExportLabelHeightCorrection);
                    }

                    if (calculatedHeight <= height)
                    {
                        calculatedHeight = height;
                    }

                    fileExtension.ValueSwitchOn()
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImageJpegFileExtension}", _ =>
                        {
                            using var stream = File.OpenWrite(filename);
                            OxyPlot.SkiaSharp.JpegExporter.Export(
                                GanttChartPlotModel,
                                stream,
                                width,
                                calculatedHeight,
                                200);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImagePngFileExtension}", _ =>
                        {
                            using var stream = File.OpenWrite(filename);
                            OxyPlot.SkiaSharp.PngExporter.Export(
                                GanttChartPlotModel,
                                stream,
                                width,
                                calculatedHeight);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_PdfFileExtension}", _ =>
                        {
                            using var stream = File.OpenWrite(filename);
                            OxyPlot.SkiaSharp.PdfExporter.Export(
                                GanttChartPlotModel,
                                stream,
                                width,
                                calculatedHeight);
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

        public void BuildGanttChartPlotModel()
        {
            PlotModel? plotModel = null;

            lock (m_Lock)
            {
                plotModel = BuildGanttChartPlotModelInternal(
                    m_DateTimeCalculator,
                    m_CoreViewModel.ResourceSeriesSet,
                    m_CoreViewModel.ResourceSettings,
                    m_CoreViewModel.ArrowGraphSettings,
                    m_CoreViewModel.WorkStreamSettings,
                    m_CoreViewModel.ProjectStartDateTime,
                    m_CoreViewModel.ShowDates,
                    m_CoreViewModel.GraphCompilation,
                    GroupByMode,
                    AnnotationStyle,
                    LabelGroups,
                    ShowProjectFinish,
                    ShowTracking,
                    m_CoreViewModel.BaseTheme);
            }

            GanttChartPlotModel = plotModel ?? new PlotModel();
        }

        #endregion

        #region IKillSubscriptions Members

        public void KillSubscriptions()
        {
            m_BuildGanttChartPlotModelSub?.Dispose();
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
                m_GroupByMode?.Dispose();
                m_AnnotationStyle?.Dispose();
                m_LabelGroups?.Dispose();
                m_ShowProjectFinish?.Dispose();
                m_ShowTracking?.Dispose();
                m_IsGrouped?.Dispose();
                m_IsAnnotated?.Dispose();
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
