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

            ActivitySelector = new ActivitySelectorViewModel(m_CoreViewModel, []);
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

            m_ShowGroupLabels = this
                .WhenAnyValue(rcm => rcm.m_CoreViewModel.GanttChartShowGroupLabels)
                .ToProperty(this, rcm => rcm.ShowGroupLabels);

            m_ShowProjectFinish = this
                .WhenAnyValue(rcm => rcm.m_CoreViewModel.GanttChartShowProjectFinish)
                .ToProperty(this, rcm => rcm.ShowProjectFinish);

            m_ShowTracking = this
                .WhenAnyValue(rcm => rcm.m_CoreViewModel.GanttChartShowTracking)
                .ToProperty(this, rcm => rcm.ShowTracking);

            m_ShowToday = this
                .WhenAnyValue(rcm => rcm.m_CoreViewModel.GanttChartShowToday)
                .ToProperty(this, rcm => rcm.ShowToday);

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
                    rcm => rcm.m_CoreViewModel.ShowDates,
                    rcm => rcm.m_CoreViewModel.UseClassicDates,
                    rcm => rcm.ShowGroupLabels,
                    rcm => rcm.ShowProjectFinish,
                    rcm => rcm.ShowTracking,
                    rcm => rcm.ShowToday,
                    (a, b, c, d, e, f) =>
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
                    //rcm => rcm.m_CoreViewModel.WorkStreamSettings,
                    rcm => rcm.m_CoreViewModel.ProjectStart,
                    rcm => rcm.m_CoreViewModel.Today,
                    //rcm => rcm.m_CoreViewModel.GraphCompilation,
                    rcm => rcm.m_CoreViewModel.BaseTheme,
                    rcm => rcm.GroupByMode,
                    rcm => rcm.AnnotationStyle,
                    rcm => rcm.BoolAccumulator,
                    rcm => rcm.ActivitySelector.TargetActivitiesString,
                    (a, b, c, d, e, f, g, h, i, j) => (a, b, c, d, e, f, g, h, i, j)) // Do this as a workaround because WhenAnyValue cannot handle this many individual inputs.
                .ObserveOn(Scheduler.CurrentThread)
                .Subscribe(async _ => await BuildGanttChartPlotModelAsync());

            Id = Resource.ProjectPlan.Titles.Title_GanttChartView;
            Title = Resource.ProjectPlan.Titles.Title_GanttChartView;
        }

        #endregion

        #region Properties

        private readonly ObservableAsPropertyHelper<BoolToggle> m_BoolAccumulator;
        public BoolToggle BoolAccumulator => m_BoolAccumulator.Value;

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
            DateTimeOffset projectStart,
            DateTimeOffset today,
            bool showToday,
            bool showDates,
            IGraphCompilation<int, int, int, IDependentActivity> graphCompilation,
            GroupByMode groupByMode,
            AnnotationStyle annotationStyle,
            bool labelGroups,
            bool showProjectFinish,
            bool showTracking,
            IEnumerable<int> highlightActivitySuccessors,
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

            plotModel.Axes.Add(BuildResourceChartXAxis(dateTimeCalculator, finishTime, showDates, projectStart));

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
            string startEndFormat = showDates ? DateTimeCalculator.DateFormat : "0";

            var series = new IntervalBarSeries
            {
                Title = Resource.ProjectPlan.Labels.Label_GanttChartSeries,
                LabelFormatString = @"",
                TrackerFormatString = $"{Resource.ProjectPlan.Labels.Label_Activity}: {{2}}\n{Resource.ProjectPlan.Labels.Label_Start}: {{4:{startEndFormat}}}\n{Resource.ProjectPlan.Labels.Label_End}: {{5:{startEndFormat}}}",
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
                                AddBarItemToSeries(
                                    dateTimeCalculator,
                                    projectStart,
                                    showDates,
                                    showTracking,
                                    plotModel,
                                    colorFormatLookup,
                                    series,
                                    labels,
                                    activity,
                                    highlightActivitySuccessors);
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
                                        AddBarItemToSeries(
                                            dateTimeCalculator,
                                            projectStart,
                                            showDates,
                                            showTracking,
                                            plotModel,
                                            colorFormatLookup,
                                            series,
                                            labels,
                                            activity,
                                            highlightActivitySuccessors);
                                    }
                                }

                                int maximumY = labels.Count;

                                switch (annotationStyle)
                                {
                                    case AnnotationStyle.None:
                                        break;
                                    case AnnotationStyle.Plain:
                                        {
                                            OxyColor resourceColor = OxyColors.Blue;

                                            OxyColor fillColor = OxyColor.FromAColor(
                                                ColorHelper.AnnotationATransparent,
                                                resourceColor);

                                            AddAnnotationToPlot(
                                                dateTimeCalculator,
                                                projectStart,
                                                showDates,
                                                labelGroups,
                                                plotModel,
                                                series,
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
                                            OxyColor resourceColor = OxyColor.FromArgb(
                                                colorFormat.A,
                                                colorFormat.R,
                                                colorFormat.G,
                                                colorFormat.B);

                                            OxyColor fillColor = OxyColor.FromAColor(
                                                ColorHelper.AnnotationALight,
                                                resourceColor);

                                            AddAnnotationToPlot(
                                                dateTimeCalculator,
                                                projectStart,
                                                showDates,
                                                labelGroups,
                                                plotModel,
                                                series,
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
                                        AddBarItemToSeries(
                                            dateTimeCalculator,
                                            projectStart,
                                            showDates,
                                            showTracking,
                                            plotModel,
                                            colorFormatLookup,
                                            series,
                                            labels,
                                            activity,
                                            highlightActivitySuccessors);
                                    }
                                }

                                int maximumY = labels.Count;

                                switch (annotationStyle)
                                {
                                    case AnnotationStyle.None:
                                        break;
                                    case AnnotationStyle.Plain:
                                        {
                                            WorkStreamModel workStreamModel = workStreamLookup[workStreamId];
                                            OxyColor workStreamColor = OxyColors.Blue;

                                            OxyColor fillColor = OxyColor.FromAColor(
                                                ColorHelper.AnnotationATransparent,
                                                workStreamColor);

                                            AddAnnotationToPlot(
                                                dateTimeCalculator,
                                                projectStart,
                                                showDates,
                                                labelGroups,
                                                plotModel,
                                                series,
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
                                            OxyColor workStreamColor = OxyColor.FromArgb(
                                                workStreamModel.ColorFormat.A,
                                                workStreamModel.ColorFormat.R,
                                                workStreamModel.ColorFormat.G,
                                                workStreamModel.ColorFormat.B);

                                            OxyColor fillColor = OxyColor.FromAColor(
                                                ColorHelper.AnnotationALight,
                                                workStreamColor);

                                            AddAnnotationToPlot(
                                                dateTimeCalculator,
                                                projectStart,
                                                showDates,
                                                labelGroups,
                                                plotModel,
                                                series,
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
                            series.Items.Add(new IntervalBarItem { Start = -1, End = -1 });
                            labels.Add(string.Empty);
                        }

                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(groupByMode), @$"{Resource.ProjectPlan.Messages.Message_UnknownGroupByMode} {groupByMode}");
                }

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
            }

            plotModel.Axes.Add(BuildResourceChartYAxis(labels));
            plotModel.Series.Add(series);

            if (plotModel is IPlotModel plotModelInterface)
            {
                plotModelInterface.Update(true);
            }

            return plotModel.SetBaseTheme(baseTheme);
        }

        private static void AddAnnotationToPlot(
            IDateTimeCalculator dateTimeCalculator,
            DateTimeOffset projectStart,
            bool showDates,
            bool labelGroups,
            PlotModel plotModel,
            IntervalBarSeries series,
            List<string> labels,
            string itemName,
            int itemStartTime,
            int itemFinishTime,
            int minimumY,
            int maximumY,
            OxyColor strokeColor,
            OxyColor fillColor)
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

            var annotation = new RectangleAnnotation
            {
                MinimumX = minimumX,
                MaximumX = maximumX,
                MinimumY = minimumY,
                MaximumY = maximumY,
                ToolTip = itemName,
                Fill = fillColor,
                Stroke = strokeColor,
                StrokeThickness = 1,
                Layer = AnnotationLayer.BelowSeries,
            };

            if (labelGroups)
            {
                annotation.Text = itemName;
                annotation.TextPosition = new DataPoint(minimumX, maximumY);
                annotation.TextHorizontalAlignment = HorizontalAlignment.Left;
                annotation.TextVerticalAlignment = VerticalAlignment.Bottom;

                series.Items.Add(new IntervalBarItem { Start = -1, End = -1 });
                labels.Add(string.Empty);
            }

            plotModel.Annotations.Add(annotation);
        }

        private static void AddBarItemToSeries(
            IDateTimeCalculator dateTimeCalculator,
            DateTimeOffset projectStart,
            bool showDates,
            bool showTracking,
            PlotModel plotModel,
            SlackColorFormatLookup colorFormatLookup,
            IntervalBarSeries series,
            List<string> labels,
            IDependentActivity activity,
            IEnumerable<int> highlightActivitySuccessors)
        {
            if (activity.EarliestStartTime.HasValue
                && activity.EarliestFinishTime.HasValue
                && activity.Duration > 0)
            {
                string id = activity.Id.ToString(CultureInfo.InvariantCulture);
                string label = string.IsNullOrWhiteSpace(activity.Name) ? id : $"{activity.Name} ({id})";
                Color slackColor = colorFormatLookup.FindSlackColor(activity.TotalSlack);

                OxyColor backgroundColor = OxyColor.FromAColor(ColorHelper.AnnotationATrackerOverlay, OxyColors.White);

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

                bool hasNoHighlightActivitySuccessors = !highlightActivitySuccessors.Any();

                bool highlightAsActivity = highlightActivitySuccessors.Contains(activity.Id);

                bool highlightAsSuccessor = activity.Dependencies.Union(activity.ManualDependencies)
                    .Intersect(highlightActivitySuccessors)
                    .Any();

                // Only use the slack color if the activity is not
                // highlighted as an activity or a successor.
                if (hasNoHighlightActivitySuccessors
                    || highlightAsActivity
                    || highlightAsSuccessor)
                {
                    backgroundColor = OxyColor.FromArgb(
                        slackColor.A,
                        slackColor.R,
                        slackColor.G,
                        slackColor.B);
                }

                var item = new IntervalBarItem
                {
                    Title = label,
                    Start = start,
                    End = end,
                    Color = backgroundColor,
                };

                series.Items.Add(item);
                labels.Add(label);

                int labelCount = labels.Count;

                if (highlightAsActivity)
                {
                    ArrowAnnotation activityAnnotation = ActivityAnnotation(start, labelCount, OxyColors.LimeGreen);
                    plotModel.Annotations.Add(activityAnnotation);
                }
                if (highlightAsSuccessor)
                {
                    ArrowAnnotation activityAnnotation = ActivityAnnotation(start, labelCount, OxyColors.Red);
                    plotModel.Annotations.Add(activityAnnotation);
                }

                if (showTracking)
                {
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

        private static ArrowAnnotation ActivityAnnotation(
            double start,
            int labelCount,
            OxyColor color)
        {
            double Y = labelCount + (c_TrackerAnnotationMinCorrection + c_TrackerAnnotationMaxCorrection) / 2.0;

            return new ArrowAnnotation
            {
                StartPoint = new DataPoint(start - 0.1, Y),
                EndPoint = new DataPoint(start, Y),
                Color = color,
                StrokeThickness = 1,
                Layer = AnnotationLayer.AboveSeries,
            };
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
            DateTimeOffset projectStart)
        {
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            if (finishTime != default)
            {
                double minValue = ChartHelper.CalculateChartStartTimeXValue(-1, showDates, projectStart, dateTimeCalculator);
                double maxValue = ChartHelper.CalculateChartFinishTimeXValue(finishTime + 1, showDates, projectStart, dateTimeCalculator);

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

        private readonly ObservableAsPropertyHelper<bool> m_ShowGroupLabels;
        public bool ShowGroupLabels
        {
            get => m_ShowGroupLabels.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.GanttChartShowGroupLabels = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_ShowProjectFinish;
        public bool ShowProjectFinish
        {
            get => m_ShowProjectFinish.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.GanttChartShowProjectFinish = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_ShowTracking;
        public bool ShowTracking
        {
            get => m_ShowTracking.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.GanttChartShowTracking = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_ShowToday;
        public bool ShowToday
        {
            get => m_ShowToday.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.GanttChartShowToday = value;
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
                    m_CoreViewModel.ProjectStart,
                    m_CoreViewModel.Today,
                    ShowToday,
                    m_CoreViewModel.ShowDates,
                    m_CoreViewModel.GraphCompilation,
                    GroupByMode,
                    AnnotationStyle,
                    ShowGroupLabels,
                    ShowProjectFinish,
                    ShowTracking,
                    ActivitySelector.SelectedActivityIds,
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
                m_ShowGroupLabels?.Dispose();
                m_ShowProjectFinish?.Dispose();
                m_ShowTracking?.Dispose();
                m_ShowToday?.Dispose();
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
