using Avalonia;
using ReactiveUI;
using ScottPlot;
using ScottPlot.Avalonia;
using ScottPlot.Plottables;
using System.Data;
using System.Globalization;
using System.Reactive;
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
        private readonly ISettingService m_SettingService;
        private readonly IDialogService m_DialogService;
        private readonly IDateTimeCalculator m_DateTimeCalculator;

        private readonly IDisposable? m_BuildGanttChartPlotModelSub;

        private const double c_ExportLabelHeightCorrection = 1.2;
        private const double c_YAxisMinimum = -1.0;
        private const double c_BarSize = 0.5;
        private const double c_TrackerCorrection = c_BarSize / 2.0;

        private const double c_ArrowHeadDelta = 0.03;
        private const float c_ArrowHeadWidth = 6.0f;
        private const float c_ArrowHeadLength = 14.0f;
        private const float c_ArrowHeadHeight = 8.0f;

        private const float c_VerticalLineWidth = 2.0f;

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

            ActivitySelector = new ActivitySelectorViewModel(m_CoreViewModel, []);
            m_GanttChartPlotModel = new AvaPlot();

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
                .WhenAnyValue(rcm => rcm.m_CoreViewModel.DisplaySettingsViewModel.GanttChartGroupByMode)
                .ToProperty(this, rcm => rcm.GroupByMode);

            m_AnnotationStyle = this
                .WhenAnyValue(rcm => rcm.m_CoreViewModel.DisplaySettingsViewModel.GanttChartAnnotationStyle)
                .ToProperty(this, rcm => rcm.AnnotationStyle);

            m_ShowGroupLabels = this
                .WhenAnyValue(rcm => rcm.m_CoreViewModel.DisplaySettingsViewModel.GanttChartShowGroupLabels)
                .ToProperty(this, rcm => rcm.ShowGroupLabels);

            m_ShowProjectFinish = this
                .WhenAnyValue(rcm => rcm.m_CoreViewModel.DisplaySettingsViewModel.GanttChartShowProjectFinish)
                .ToProperty(this, rcm => rcm.ShowProjectFinish);

            m_ShowTracking = this
                .WhenAnyValue(rcm => rcm.m_CoreViewModel.DisplaySettingsViewModel.GanttChartShowTracking)
                .ToProperty(this, rcm => rcm.ShowTracking);

            m_ShowToday = this
                .WhenAnyValue(rcm => rcm.m_CoreViewModel.DisplaySettingsViewModel.GanttChartShowToday)
                .ToProperty(this, rcm => rcm.ShowToday);

            m_ShowMilestones = this
                .WhenAnyValue(rcm => rcm.m_CoreViewModel.DisplaySettingsViewModel.GanttChartShowMilestones)
                .ToProperty(this, rcm => rcm.ShowMilestones);

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

            // We need to use an enum because raised changes on bools aren't always captured.
            // https://github.com/reactiveui/ReactiveUI/issues/3846
            m_BoolAccumulator = this
                .WhenAnyValue(
                    rcm => rcm.m_CoreViewModel.DisplaySettingsViewModel.ShowDates,
                    rcm => rcm.m_CoreViewModel.DisplaySettingsViewModel.UseClassicDates,
                    rcm => rcm.ShowGroupLabels,
                    rcm => rcm.ShowProjectFinish,
                    rcm => rcm.ShowTracking,
                    rcm => rcm.ShowToday,
                    rcm => rcm.ShowMilestones,
                    (a, b, c, d, e, f, g) =>
                    {
                        if (m_BoolAccumulator is null
                            || m_BoolAccumulator.Value == BoolToggle.Up)
                        {
                            return BoolToggle.Down;
                        }
                        return BoolToggle.Up;
                    })
                .ToProperty(this, rcm => rcm.BoolAccumulator);

            m_BuildGanttChartPlotModelSub = this
                .WhenAnyValue(
                    rcm => rcm.m_CoreViewModel.ResourceSeriesSet,
                    rcm => rcm.m_CoreViewModel.ResourceSettings,
                    rcm => rcm.m_CoreViewModel.ArrowGraphSettings,
                    rcm => rcm.m_CoreViewModel.ProjectStart,
                    rcm => rcm.m_CoreViewModel.Duration,
                    rcm => rcm.m_CoreViewModel.Today,
                    rcm => rcm.m_CoreViewModel.BaseTheme,
                    rcm => rcm.GroupByMode,
                    rcm => rcm.AnnotationStyle,
                    rcm => rcm.BoolAccumulator,
                    rcm => rcm.ActivitySelector.TargetActivitiesString,
                    (a, b, c, d, e, f, g, h, i, j, k) => (a, b, c, d, e, f, g, h, i, j, k)) // Do this as a workaround because WhenAnyValue cannot handle this many individual inputs.
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(async _ => await BuildGanttChartPlotModelAsync());

            Id = Resource.ProjectPlan.Titles.Title_GanttChartView;
            Title = Resource.ProjectPlan.Titles.Title_GanttChartView;
        }

        #endregion

        #region Properties

        private readonly ObservableAsPropertyHelper<BoolToggle> m_BoolAccumulator;
        public BoolToggle BoolAccumulator => m_BoolAccumulator.Value;

        private AvaPlot m_GanttChartPlotModel;
        public AvaPlot GanttChartPlotModel
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

        private static AvaPlot BuildGanttChartPlotModelInternal(
            IDateTimeCalculator dateTimeCalculator,
            ResourceSeriesSetModel resourceSeriesSet,
            ResourceSettingsModel resourceSettingsSettings,
            ArrowGraphSettingsModel arrowGraphSettings,
            WorkStreamSettingsModel workStreamSettings,
            DateTimeOffset projectStart,
            int? duration,
            DateTimeOffset today,
            bool showToday,
            bool showMilestones,
            bool showDates,
            IGraphCompilation<int, int, int, IDependentActivity> graphCompilation,
            GroupByMode groupByMode,
            AnnotationStyle annotationStyle,
            bool labelGroups,
            bool showProjectFinish,
            bool showTracking,
            IEnumerable<int> highlightActivityConnections,
            BaseTheme baseTheme)
        {
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            ArgumentNullException.ThrowIfNull(resourceSeriesSet);
            ArgumentNullException.ThrowIfNull(resourceSettingsSettings);
            ArgumentNullException.ThrowIfNull(arrowGraphSettings);
            ArgumentNullException.ThrowIfNull(workStreamSettings);
            ArgumentNullException.ThrowIfNull(graphCompilation);

            var plotModel = new AvaPlot();

            foreach (var grid in plotModel.Plot.Axes.AllGrids)
            {
                grid.YAxisStyle.IsVisible = false;
            }

            if (!graphCompilation.DependentActivities.Any())
            {
                return plotModel.SetBaseTheme(baseTheme);
            }

            int startTime = resourceSeriesSet.ResourceSchedules
                .Select(x => x.StartTime)
                .DefaultIfEmpty().Max();

            int finishTime = resourceSeriesSet.ResourceSchedules
                .Select(x => x.FinishTime)
                .DefaultIfEmpty().Max();

            if (duration is not null
                && duration > finishTime)
            {
                finishTime = duration.GetValueOrDefault();
            }

            IXAxis xAxis = BuildResourceChartXAxis(plotModel, dateTimeCalculator, startTime, finishTime, showDates, projectStart);
            double minXValue = xAxis.Min;
            double maxXValue = xAxis.Max;

            var colorFormatLookup = new SlackColorFormatLookup(arrowGraphSettings.ActivitySeverities);
            string startEndFormat = showDates ? DateTimeCalculator.DateFormat : "0";

            var bars = new List<Bar>();
            var highlights = new List<IPlottable>();
            var labels = new List<string>();

            switch (groupByMode)
            {
                case GroupByMode.None:
                    {
                        // Add all the activities (in reverse display order).

                        IOrderedEnumerable<IDependentActivity> orderedActivities = graphCompilation
                            .DependentActivities
                            .OrderByDescending(x => x.EarliestStartTime)
                            .ThenByDescending(x => x.TotalSlack);

                        foreach (IDependentActivity activity in orderedActivities)
                        {
                            AddBarItemToSeries(
                                dateTimeCalculator,
                                projectStart,
                                showDates,
                                showTracking,
                                colorFormatLookup,
                                bars,
                                labels,
                                highlights,
                                activity,
                                highlightActivityConnections);
                        }

                        // Add an extra row for padding.
                        // This item will appear at the top of the grouping.

                        bars.Add(BuildEmptyBar(minXValue));
                        labels.Add(string.Empty);
                    }

                    break;
                case GroupByMode.Resource:
                    {
                        // Find all the resource series with at least 1 scheduled activity.

                        IEnumerable<ResourceSeriesModel> scheduledResourceSeriesSet = resourceSeriesSet.Combined
                            .Where(x => x.ResourceSchedule.ScheduledActivities.Count != 0);

                        // Record the resource name, and the scheduled activities (in order).

                        var scheduledResourceActivitiesSet =
                            new List<(string ResourceName, ColorFormatModel ColorFormat, int DisplayOrder, IList<ScheduledActivityModel> ScheduledActivities)>();

                        foreach (ResourceSeriesModel resourceSeries in scheduledResourceSeriesSet)
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

                            ScheduledActivityModel? firstItem = orderedScheduledActivities.OrderBy(x => x.StartTime).FirstOrDefault();
                            ScheduledActivityModel? lastItem = orderedScheduledActivities.OrderByDescending(x => x.FinishTime).FirstOrDefault();

                            int resourceStartTime = firstItem?.StartTime ?? 0;
                            int resourceFinishTime = lastItem?.FinishTime ?? 0;

                            int minimumY = labels.Count;

                            // Now add the scheduled activities (again, in reverse display order).

                            foreach (ScheduledActivityModel scheduledActivity in orderedScheduledActivities)
                            {
                                if (activityLookup.TryGetValue(scheduledActivity.Id, out IDependentActivity? activity))
                                {
                                    AddBarItemToSeries(
                                        dateTimeCalculator,
                                        projectStart,
                                        showDates,
                                        showTracking,
                                        colorFormatLookup,
                                        bars,
                                        labels,
                                        highlights,
                                        activity,
                                        highlightActivityConnections);
                                }
                            }

                            // Add an extra row for padding.
                            // IntervalBarItems are added in reverse order to how they will be displayed.
                            // So, this item will appear at the bottom of the grouping.

                            bars.Add(BuildEmptyBar(minXValue));
                            labels.Add(string.Empty);

                            int maximumY = labels.Count;

                            switch (annotationStyle)
                            {
                                case AnnotationStyle.None:
                                    break;
                                case AnnotationStyle.Plain:
                                    {
                                        Color resourceColor = Colors.Blue;
                                        Color fillColor = resourceColor.WithAlpha(ColorHelper.AnnotationATransparent);

                                        AddAnnotationToPlot(
                                            dateTimeCalculator,
                                            projectStart,
                                            showDates,
                                            labelGroups,
                                            plotModel,
                                            bars,
                                            labels,
                                            resourceName,
                                            resourceStartTime,
                                            resourceFinishTime,
                                            minimumY,
                                            maximumY,
                                            resourceColor,
                                            fillColor);
                                    }
                                    break;
                                case AnnotationStyle.Color:
                                    {
                                        Color resourceColor = new(
                                            colorFormat.R,
                                            colorFormat.G,
                                            colorFormat.B,
                                            colorFormat.A);

                                        Color fillColor = resourceColor.WithAlpha(ColorHelper.AnnotationALight);

                                        AddAnnotationToPlot(
                                            dateTimeCalculator,
                                            projectStart,
                                            showDates,
                                            labelGroups,
                                            plotModel,
                                            bars,
                                            labels,
                                            resourceName,
                                            resourceStartTime,
                                            resourceFinishTime,
                                            minimumY,
                                            maximumY,
                                            resourceColor,
                                            fillColor);
                                    }
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException(nameof(annotationStyle));
                            }
                        }

                        // Add an extra row for padding.
                        // This item will appear at the top of the grouping.

                        bars.Add(BuildEmptyBar(minXValue));
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

                        Dictionary<int, ScheduledActivityModel> scheduledActivityLookup = resourceSeriesSet.Combined
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

                            ScheduledActivityModel? firstItem = orderedScheduledActivities.OrderBy(x => x.StartTime).FirstOrDefault();
                            ScheduledActivityModel? lastItem = orderedScheduledActivities.OrderByDescending(x => x.FinishTime).FirstOrDefault();

                            int workStreamStartTime = firstItem?.StartTime ?? 0;
                            int workStreamFinishTime = lastItem?.FinishTime ?? 0;

                            int minimumY = labels.Count;

                            // Now add the scheduled activities (again, in reverse display order).

                            foreach (ScheduledActivityModel scheduledActivity in orderedScheduledActivities)
                            {
                                if (activityLookup.TryGetValue(scheduledActivity.Id, out IDependentActivity? activity))
                                {
                                    AddBarItemToSeries(
                                        dateTimeCalculator,
                                        projectStart,
                                        showDates,
                                        showTracking,
                                        colorFormatLookup,
                                        bars,
                                        labels,
                                        highlights,
                                        activity,
                                        highlightActivityConnections);
                                }
                            }

                            // Add an extra row for padding.
                            // IntervalBarItems are added in reverse order to how they will be displayed.
                            // So, this item will appear at the bottom of the grouping.

                            bars.Add(BuildEmptyBar(minXValue));
                            labels.Add(string.Empty);

                            int maximumY = labels.Count;

                            switch (annotationStyle)
                            {
                                case AnnotationStyle.None:
                                    break;
                                case AnnotationStyle.Plain:
                                    {
                                        WorkStreamModel workStreamModel = workStreamLookup[workStreamId];
                                        Color workStreamColor = Colors.Blue;
                                        Color fillColor = workStreamColor.WithAlpha(ColorHelper.AnnotationATransparent);

                                        AddAnnotationToPlot(
                                            dateTimeCalculator,
                                            projectStart,
                                            showDates,
                                            labelGroups,
                                            plotModel,
                                            bars,
                                            labels,
                                            workStreamModel.Name,
                                            workStreamStartTime,
                                            workStreamFinishTime,
                                            minimumY,
                                            maximumY,
                                            workStreamColor,
                                            fillColor);
                                    }
                                    break;
                                case AnnotationStyle.Color:
                                    {
                                        WorkStreamModel workStreamModel = workStreamLookup[workStreamId];
                                        var workStreamColor = new Color(
                                            workStreamModel.ColorFormat.R,
                                            workStreamModel.ColorFormat.G,
                                            workStreamModel.ColorFormat.B,
                                            workStreamModel.ColorFormat.A);

                                        Color fillColor = workStreamColor.WithAlpha(ColorHelper.AnnotationALight);

                                        AddAnnotationToPlot(
                                            dateTimeCalculator,
                                            projectStart,
                                            showDates,
                                            labelGroups,
                                            plotModel,
                                            bars,
                                            labels,
                                            workStreamModel.Name,
                                            workStreamStartTime,
                                            workStreamFinishTime,
                                            minimumY,
                                            maximumY,
                                            workStreamColor,
                                            fillColor);
                                    }
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException(nameof(annotationStyle));
                            }
                        }

                        // Add an extra row for padding.
                        // This item will appear at the top of the grouping.

                        bars.Add(BuildEmptyBar(minXValue));
                        labels.Add(string.Empty);
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(groupByMode), @$"{Resource.ProjectPlan.Messages.Message_UnknownGroupByMode} {groupByMode}");
            }

            // Add an extra row for to ensure the graph has a right edge near the project finish time.
            bars.Add(BuildEmptyBar(maxXValue));

            if (showProjectFinish)
            {
                var projectFinish = new StringBuilder(Resource.ProjectPlan.Labels.Label_ProjectFinish);
                projectFinish.Append(' ');

                if (showDates)
                {
                    DateTimeOffset startAndFinish = dateTimeCalculator.AddDays(projectStart, finishTime);
                    projectFinish.Append(
                        dateTimeCalculator
                            .DisplayFinishDate(startAndFinish, startAndFinish, 1)
                            .ToString(DateTimeCalculator.DateFormat));
                }
                else
                {
                    projectFinish.Append(finishTime);
                }

                double finishTimeX = ChartHelper.CalculateChartFinishTimeXValue(
                    finishTime,
                    showDates,
                    projectStart,
                    dateTimeCalculator);
                double finishTimeY = labels.Count;

                Annotation annotation = plotModel.Plot.Add.Annotation(projectFinish.ToString(), Alignment.UpperRight);
                annotation.LabelBackgroundColor = Colors.Transparent;
                annotation.LabelBorderColor = Colors.Transparent;
                annotation.LabelShadowColor = Colors.Transparent;
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

                    plotModel.Plot.Add.VerticalLine(
                        todayTimeX,
                        width: c_VerticalLineWidth,
                        pattern: LinePattern.Dotted);
                }
            }

            // Enumerate the bar series and set the position of each bar.
            for (int i = 0; i < bars.Count; i++)
            {
                var bar = bars[i];
                bar.Position = i + 1;
            }

            BarPlot barPlot = plotModel.Plot.Add.Bars(bars);
            barPlot.Horizontal = true;

            // Highlights (above the bar plot).
            plotModel.Plot.PlottableList.AddRange(highlights);

            if (showMilestones)
            {
                List<IDependentActivity> milestones = [.. graphCompilation
                    .DependentActivities
                    .OrderBy(x => x.EarliestStartTime)
                    .Where(x => x.Duration == 0)];

                var milestoneArrows = new List<AnnotatedArrow>();

                foreach (IDependentActivity milestone in milestones)
                {
                    int id = milestone.Id;
                    string formattedId = id.ToString(CultureInfo.InvariantCulture);
                    string label = string.IsNullOrWhiteSpace(milestone.Name) ? formattedId : $"{milestone.Name} ({formattedId})";

                    double milestoneTimeX = ChartHelper.CalculateChartStartTimeXValue(
                        milestone.EarliestStartTime.GetValueOrDefault(),
                        showDates,
                        projectStart,
                        dateTimeCalculator);

                    // When no activity connections are selected.
                    bool hasNoHighlightActivityConnections = !highlightActivityConnections.Any();

                    // When the activity is highlighted as a connection.
                    bool highlightAsActivity = highlightActivityConnections.Contains(id);

                    AnnotatedArrow milestoneArrow = MilestoneAnnotation(
                        milestoneTimeX,
                        c_ArrowHeadHeight,
                        label,
                        hasNoHighlightActivityConnections || highlightAsActivity ? Colors.Yellow : Colors.White);

                    milestoneArrows.Add(milestoneArrow);
                }

                plotModel.Plot.PlottableList.AddRange(milestoneArrows);
            }

            // Style the plot so the bars start on the left edge.
            plotModel.Plot.Axes.Margins(left: 0, right: 0, bottom: 0, top: 0);

            BuildResourceChartYAxis(plotModel, labels);

            plotModel.Plot.Axes.AutoScale();

            return plotModel.SetBaseTheme(baseTheme);
        }

        private static Bar BuildEmptyBar(double minValue)
        {
            return new Bar
            {
                ValueBase = minValue,
                Value = minValue,
                LineColor = Colors.Transparent,
                FillColor = Colors.Transparent,
                Size = c_BarSize
            };
        }

        private static void AddAnnotationToPlot(
            IDateTimeCalculator dateTimeCalculator,
            DateTimeOffset projectStart,
            bool showDates,
            bool labelGroups,
            AvaPlot plotModel,
            List<Bar> series,
            List<string> labels,
            string itemName,
            int itemStartTime,
            int itemFinishTime,
            int minimumY,
            int maximumY,
            Color strokeColor,
            Color fillColor)
        {
            double minimumX = ChartHelper.CalculateChartStartTimeXValue(
                itemStartTime,
                showDates,
                projectStart,
                dateTimeCalculator);
            double maximumX = ChartHelper.CalculateChartFinishTimeXValue(
                itemFinishTime,
                showDates,
                projectStart,
                dateTimeCalculator);

            AnnotatedRectangle rp = new()
            {
                Annotation = itemName,
                X1 = minimumX,
                X2 = maximumX,
                Y1 = minimumY,
                Y2 = maximumY,
                LineColor = strokeColor,
                FillColor = fillColor,
                LineWidth = 1,
            };

            plotModel.Plot.PlottableList.Add(rp);

            if (labelGroups)
            {
                Text text = plotModel.Plot.Add.Text(itemName, minimumX, maximumY);

                text.OffsetY = -PlotHelper.FontOffset;
                text.LabelBackgroundColor = Colors.Transparent;
                text.LabelFontSize = PlotHelper.FontSize;

                series.Add(BuildEmptyBar(minimumX));
                labels.Add(string.Empty);
            }
        }

        private static void AddBarItemToSeries(
            IDateTimeCalculator dateTimeCalculator,
            DateTimeOffset projectStart,
            bool showDates,
            bool showTracking,
            SlackColorFormatLookup colorFormatLookup,
            List<Bar> series,
            List<string> labels,
            List<IPlottable> highlights,
            IDependentActivity activity,
            IEnumerable<int> highlightActivityConnections)
        {
            if (activity.EarliestStartTime.HasValue
                && activity.EarliestFinishTime.HasValue
                && activity.Duration > 0)
            {
                string id = activity.Id.ToString(CultureInfo.InvariantCulture);
                string label = string.IsNullOrWhiteSpace(activity.Name) ? id : $"{activity.Name} ({id})";
                Avalonia.Media.Color slackColor = colorFormatLookup.FindSlackColor(activity.TotalSlack);

                Color backgroundColor = Colors.White.WithAlpha(ColorHelper.AnnotationATrackerOverlay);

                double start = ChartHelper.CalculateChartStartTimeXValue(
                    activity.EarliestStartTime.GetValueOrDefault(),
                    showDates,
                    projectStart,
                    dateTimeCalculator);
                double end = ChartHelper.CalculateChartFinishTimeXValue(
                    activity.EarliestFinishTime.GetValueOrDefault(),
                    showDates,
                    projectStart,
                    dateTimeCalculator);

                // When no activity connections are selected.
                bool hasNoHighlightActivityConnections = !highlightActivityConnections.Any();

                // When the activity is highlighted as a connection.
                bool highlightAsActivity = highlightActivityConnections.Contains(activity.Id);

                // When the activity is highlighted as a successor.
                bool highlightAsSuccessor = activity.Dependencies.Union(activity.PlanningDependencies)
                    .Intersect(highlightActivityConnections)
                    .Any();

                // When the activity is highlighted as a dependency.
                bool highlightAsDependency = activity.Successors
                    .Intersect(highlightActivityConnections)
                    .Any();

                // Only use the slack color if the activity is not
                // highlighted as an activity or a connection.
                if (hasNoHighlightActivityConnections
                    || highlightAsActivity
                    || highlightAsSuccessor
                    || highlightAsDependency)
                {
                    backgroundColor = new Color(
                        slackColor.R,
                        slackColor.G,
                        slackColor.B,
                        slackColor.A);
                }

                string from = ChartHelper.FormatStartScheduleOutput(
                     activity.EarliestStartTime.GetValueOrDefault(),
                     showDates,
                     projectStart,
                     activity.Duration,
                     dateTimeCalculator);
                string to = ChartHelper.FormatFinishScheduleOutput(
                    activity.EarliestFinishTime.GetValueOrDefault(),
                    showDates,
                    projectStart,
                    activity.Duration,
                    dateTimeCalculator);

                string barAnnotation = $"{Resource.ProjectPlan.Labels.Label_Activity}: {label}\n{Resource.ProjectPlan.Labels.Label_Start}: {from}\n{Resource.ProjectPlan.Labels.Label_End}: {to}";

                var item = new AnnotatedBar
                {
                    Annotation = barAnnotation,
                    ValueBase = start,
                    Value = end,
                    FillColor = backgroundColor,
                    Size = c_BarSize,
                };

                series.Add(item);
                labels.Add(label);

                int labelCount = labels.Count;

                if (highlightAsDependency)
                {
                    Arrow arrow = ForwardArrowHeadAnnotation(start, labelCount, Colors.Blue);
                    highlights.Add(arrow);
                }
                if (highlightAsActivity)
                {
                    double xPosition = (start + end) / 2.0;
                    Arrow fArrow = UpArrowHeadAnnotation(xPosition, labelCount, Colors.Yellow);
                    highlights.Add(fArrow);
                    Arrow bArrow = DownArrowHeadAnnotation(xPosition, labelCount, Colors.Yellow);
                    highlights.Add(bArrow);

                }
                if (highlightAsSuccessor)
                {
                    Arrow arrow = BackwardArrowHeadAnnotation(end, labelCount, Colors.Red);
                    highlights.Add(arrow);
                }

                if (showTracking)
                {
                    // Get the tracker with the highest Time value.
                    ActivityTrackerModel? lastTracker = activity.Trackers.LastOrDefault();
                    Rectangle? rectangle = TrackerAnnotation(start, end, labelCount, lastTracker);

                    if (rectangle is not null)
                    {
                        highlights.Add(rectangle);
                    }
                }
            }
        }

        private static Arrow ForwardArrowHeadAnnotation(
            double start,
            int labelCount,
            Color color)
        {
            return HorizontalArrowHeadAnnotation(start, startDelta: -c_ArrowHeadDelta, labelCount, color);
        }

        private static Arrow BackwardArrowHeadAnnotation(
            double start,
            int labelCount,
            Color color)
        {
            return HorizontalArrowHeadAnnotation(start, startDelta: c_ArrowHeadDelta, labelCount, color);
        }

        private static Arrow HorizontalArrowHeadAnnotation(
            double start,
            double startDelta,
            int labelCount,
            Color color)
        {
            double Y = labelCount;
            var startPoint = new Coordinates(start + startDelta, Y);
            var endPoint = new Coordinates(start, Y);

            return new Arrow
            {
                Base = startPoint,
                Tip = endPoint,
                ArrowLineColor = color,
                ArrowFillColor = color,
                ArrowShape = ArrowShape.Arrowhead.GetShape(),
                ArrowheadWidth = c_ArrowHeadWidth,
                ArrowheadLength = c_ArrowHeadLength,
                ArrowLineWidth = 1.0f,
            };
        }

        private static Arrow UpArrowHeadAnnotation(
            double start,
            int labelCount,
            Color color)
        {
            return VerticalArrowHeadAnnotation(start, -1, labelCount, color);
        }

        private static Arrow DownArrowHeadAnnotation(
            double start,
            int labelCount,
            Color color)
        {
            return VerticalArrowHeadAnnotation(start, 1, labelCount, color);
        }

        private static Arrow VerticalArrowHeadAnnotation(
            double start,
            int startDelta,
            int labelCount,
            Color color)
        {
            double Y = labelCount;
            var startPoint = new Coordinates(start, Y);
            var endPoint = new Coordinates(start, Y + startDelta);

            return new Arrow
            {
                Base = endPoint,
                Tip = startPoint,
                ArrowLineColor = color,
                ArrowFillColor = color,
                ArrowShape = ArrowShape.Arrowhead.GetShape(),
                ArrowheadWidth = c_ArrowHeadWidth,
                ArrowheadLength = c_ArrowHeadLength,
                ArrowLineWidth = 1.0f,
            };
        }

        private static AnnotatedArrow MilestoneAnnotation(
            double start,
            float startDelta,
            string label,
            Color color)
        {
            double Y = 0.0;
            var startPoint = new Coordinates(start, Y);
            var endPoint = new Coordinates(start, Y + startDelta);

            return new AnnotatedArrow
            {
                Annotation = label,
                Base = endPoint,
                Tip = startPoint,
                ArrowLineColor = color,
                ArrowFillColor = color,
                ArrowShape = ArrowShape.Arrowhead.GetShape(),
                ArrowheadWidth = c_ArrowHeadWidth,
                ArrowheadLength = c_ArrowHeadLength,
                ArrowLineWidth = 1.0f,
            };
        }

        private static Rectangle? TrackerAnnotation(
            double start,
            double end,
            int labelCount,
            ActivityTrackerModel? tracker)
        {
            if (tracker is null)
            {
                return null;
            }

            Color strokeColor = Colors.Black;
            Color fillColor = Colors.White.WithAlpha(ColorHelper.AnnotationATrackerOverlay);
            double maxX = start + ((end - start) * tracker.PercentageComplete / 100);
            double minY = labelCount - c_TrackerCorrection;
            double maxY = labelCount + c_TrackerCorrection;

            return new Rectangle
            {
                X1 = start,
                X2 = maxX,
                Y1 = minY,
                Y2 = maxY,
                FillColor = fillColor,
                LineColor = strokeColor,
                LineWidth = 1,
            };
        }

        private static IXAxis BuildResourceChartXAxis(
            AvaPlot plotModel,
            IDateTimeCalculator dateTimeCalculator,
            int startTime,
            int finishTime,
            bool showDates,
            DateTimeOffset projectStart)
        {
            ArgumentNullException.ThrowIfNull(plotModel);
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);

            IXAxis xAxis = plotModel.Plot.Axes.Bottom;

            if (finishTime != default)
            {
                double minValue = ChartHelper.CalculateChartStartTimeXValue(startTime - 1, showDates, projectStart, dateTimeCalculator);
                double maxValue = ChartHelper.CalculateChartFinishTimeXValue(finishTime + 1, showDates, projectStart, dateTimeCalculator);

                if (showDates)
                {
                    // Setup the plot to display X axis tick labels using date time format.
                    xAxis = plotModel.Plot.Axes.DateTimeTicksBottom();
                }

                xAxis.Min = minValue;
                xAxis.Max = maxValue;
            }

            xAxis.Label.Text = Resource.ProjectPlan.Labels.Label_TimeAxisTitle;
            xAxis.Label.FontSize = PlotHelper.FontSize;
            xAxis.Label.Bold = false;
            return xAxis;
        }

        private static IYAxis BuildResourceChartYAxis(
            AvaPlot plotModel,
            List<string> labels)
        {
            ArgumentNullException.ThrowIfNull(plotModel);
            ArgumentNullException.ThrowIfNull(labels);

            IYAxis yAxis = plotModel.Plot.Axes.Left;

            double minValue = c_YAxisMinimum;
            double maxValue = labels.Count;

            yAxis.Min = minValue;
            yAxis.Max = maxValue;
            yAxis.Label.Text = Resource.ProjectPlan.Labels.Label_GanttAxisTitle;
            yAxis.Label.FontSize = PlotHelper.FontSize;
            yAxis.Label.Bold = false;

            double[] tickPositions = [.. Enumerable.Range(1, labels.Count).Select(Convert.ToDouble)];
            string[] tickLabels = [.. labels];
            plotModel.Plot.Axes.Left.SetTicks(tickPositions, tickLabels);

            return yAxis;
        }

        private async Task SaveGanttChartImageFileAsync()
        {
            try
            {
                string title = m_SettingService.ProjectTitle;
                title = string.IsNullOrWhiteSpace(title) ? Resource.ProjectPlan.Titles.Title_UntitledProject : title;
                string ganttOutputFile = $@"{title}{Resource.ProjectPlan.Suffixes.Suffix_GanttChart}";
                string directory = m_SettingService.ProjectDirectory;
                string? filename = await m_DialogService.ShowSaveFileDialogAsync(ganttOutputFile, directory, s_ExportFileFilters);

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
                lock (m_Lock) m_CoreViewModel.DisplaySettingsViewModel.GanttChartGroupByMode = value;
            }
        }

        private readonly ObservableAsPropertyHelper<AnnotationStyle> m_AnnotationStyle;
        public AnnotationStyle AnnotationStyle
        {
            get => m_AnnotationStyle.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.DisplaySettingsViewModel.GanttChartAnnotationStyle = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_ShowGroupLabels;
        public bool ShowGroupLabels
        {
            get => m_ShowGroupLabels.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.DisplaySettingsViewModel.GanttChartShowGroupLabels = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_ShowProjectFinish;
        public bool ShowProjectFinish
        {
            get => m_ShowProjectFinish.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.DisplaySettingsViewModel.GanttChartShowProjectFinish = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_ShowTracking;
        public bool ShowTracking
        {
            get => m_ShowTracking.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.DisplaySettingsViewModel.GanttChartShowTracking = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_ShowToday;
        public bool ShowToday
        {
            get => m_ShowToday.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.DisplaySettingsViewModel.GanttChartShowToday = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_ShowMilestones;
        public bool ShowMilestones
        {
            get => m_ShowMilestones.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.DisplaySettingsViewModel.GanttChartShowMilestones = value;
            }
        }

        public IActivitySelectorViewModel ActivitySelector { get; }

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

                    if (GanttChartPlotModel.Plot.GetPlottables<BarPlot>().FirstOrDefault() is BarPlot barPlot)
                    {
                        int barCount = barPlot.Bars.Count;
                        calculatedHeight = Convert.ToInt32(barPlot.Axes.YAxis.TickLabelStyle.FontSize * barCount * c_ExportLabelHeightCorrection);
                    }

                    if (calculatedHeight <= height)
                    {
                        calculatedHeight = height;
                    }

                    fileExtension.ValueSwitchOn()
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImageJpegFileExtension}", _ =>
                        {
                            GanttChartPlotModel.Plot.Save(
                                filename, width, calculatedHeight, ImageFormats.FromFilename(filename), 100);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImagePngFileExtension}", _ =>
                        {
                            GanttChartPlotModel.Plot.Save(
                                filename, width, calculatedHeight, ImageFormats.FromFilename(filename), 100);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImageBmpFileExtension}", _ =>
                        {
                            GanttChartPlotModel.Plot.Save(
                                filename, width, calculatedHeight, ImageFormats.FromFilename(filename), 100);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImageWebpFileExtension}", _ =>
                        {
                            GanttChartPlotModel.Plot.Save(
                                filename, width, calculatedHeight, ImageFormats.FromFilename(filename), 100);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImageSvgFileExtension}", _ =>
                        {
                            GanttChartPlotModel.Plot.Save(
                                filename, width, calculatedHeight, ImageFormats.FromFilename(filename), 100);
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

        public void BuildGanttChartPlotModel()
        {
            AvaPlot? plotModel = null;

            lock (m_Lock)
            {
                plotModel = BuildGanttChartPlotModelInternal(
                    m_DateTimeCalculator,
                    m_CoreViewModel.ResourceSeriesSet,
                    m_CoreViewModel.ResourceSettings,
                    m_CoreViewModel.ArrowGraphSettings,
                    m_CoreViewModel.WorkStreamSettings,
                    m_CoreViewModel.ProjectStart,
                    m_CoreViewModel.Duration,
                    m_CoreViewModel.Today,
                    ShowToday,
                    ShowMilestones,
                    m_CoreViewModel.DisplaySettingsViewModel.ShowDates,
                    m_CoreViewModel.GraphCompilation,
                    GroupByMode,
                    AnnotationStyle,
                    ShowGroupLabels,
                    ShowProjectFinish,
                    ShowTracking,
                    ActivitySelector.SelectedActivityIds,
                    m_CoreViewModel.BaseTheme);
            }

            plotModel ??= new AvaPlot();

            // Clear existing menu items.
            plotModel.Menu?.Clear();

            // Add menu items with custom actions.
            plotModel.Menu?.Add(Resource.ProjectPlan.Menus.Menu_SaveAs, (plot) =>
            {
                SaveGanttChartImageFileCommand.Execute(null);
            });
            plotModel.Menu?.Add(Resource.ProjectPlan.Menus.Menu_Reset, (plot) =>
            {
                plot.Axes.AutoScale();
            });

            GanttChartPlotModel = plotModel;
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
                m_ShowGroupLabels?.Dispose();
                m_ShowProjectFinish?.Dispose();
                m_ShowTracking?.Dispose();
                m_ShowToday?.Dispose();
                m_ShowMilestones?.Dispose();
                m_IsGrouped?.Dispose();
                m_IsAnnotated?.Dispose();
                m_BoolAccumulator?.Dispose();
                ActivitySelector?.Dispose();
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
