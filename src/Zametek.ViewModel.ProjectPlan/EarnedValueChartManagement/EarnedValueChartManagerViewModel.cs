using Avalonia;
using ReactiveUI;
using ScottPlot;
using ScottPlot.Avalonia;
using ScottPlot.Plottables;
using System.Globalization;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    public class EarnedValueChartManagerViewModel
        : ToolViewModelBase, IEarnedValueChartManagerViewModel, IScottPlotViewModel
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
        private readonly ISettingService m_SettingService;
        private readonly IDialogService m_DialogService;
        private readonly IDateTimeCalculator m_DateTimeCalculator;
        private readonly IScottPlotImageExporter m_ScottPlotImageExporter;
        private readonly IResourceSchedulingService m_ResourceSchedulingService;

        private readonly EarnedValueResourceSelectorViewModel m_ResourceSelector;

        // Reclaims the unmanaged Skia memory of each plot this view model replaces.
        private readonly AvaPlotRetirer m_PlotRetirer;

        private readonly IDisposable? m_BuildEarnedValueChartPlotModelSub;

        private const float c_ArrowHeadWidth = 6.0f;
        private const float c_ArrowHeadLength = 14.0f;
        private const float c_ArrowHeadHeight = 8.0f;

        private const float c_VerticalLineWidth = 2.0f;

        #endregion

        #region Ctors

        public EarnedValueChartManagerViewModel(
            ICoreViewModel coreViewModel,
            ISettingService settingService,
            IDialogService dialogService,
            IDateTimeCalculator dateTimeCalculator,
            IScottPlotImageExporter scottPlotImageExporter,
            IResourceSchedulingService resourceSchedulingService)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(settingService);
            ArgumentNullException.ThrowIfNull(dialogService);
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            ArgumentNullException.ThrowIfNull(scottPlotImageExporter);
            ArgumentNullException.ThrowIfNull(resourceSchedulingService);
            m_Lock = new();
            m_CoreViewModel = coreViewModel;
            m_SettingService = settingService;
            m_DialogService = dialogService;
            m_DateTimeCalculator = dateTimeCalculator;
            m_ScottPlotImageExporter = scottPlotImageExporter;
            m_ResourceSchedulingService = resourceSchedulingService;
            m_EarnedValueChartPlotModel = new AvaPlot();
            m_PlotRetirer = new AvaPlotRetirer();

            m_ResourceSelector = new EarnedValueResourceSelectorViewModel(coreViewModel);
            ResourceSelector = m_ResourceSelector;

            ResetEarnedValueChartCommand = ReactiveCommand.Create(ResetEarnedValueChart);

            {
                ReactiveCommand<Unit, Unit> saveEarnedValueChartImageFileCommand = ReactiveCommand.CreateFromTask(SaveEarnedValueChartImageFileAsync);
                SaveEarnedValueChartImageFileCommand = saveEarnedValueChartImageFileCommand;
            }

            m_IsBusy = this
                .WhenAnyValue(evc => evc.m_CoreViewModel.IsBusy)
                .ToProperty(this, evc => evc.IsBusy);

            m_HasStaleOutputs = this
                .WhenAnyValue(evc => evc.m_CoreViewModel.HasStaleOutputs)
                .ToProperty(this, evc => evc.HasStaleOutputs);

            m_HasCompilationErrors = this
                .WhenAnyValue(evc => evc.m_CoreViewModel.HasCompilationErrors)
                .ToProperty(this, evc => evc.HasCompilationErrors);

            m_ShowProjections = this
                .WhenAnyValue(main => main.m_CoreViewModel.DisplaySettingsViewModel.EarnedValueShowProjections)
                .ToProperty(this, main => main.ShowProjections);

            m_ShowToday = this
                .WhenAnyValue(evc => evc.m_CoreViewModel.DisplaySettingsViewModel.EarnedValueShowToday)
                .ToProperty(this, evc => evc.ShowToday);

            m_ShowMilestones = this
                .WhenAnyValue(evc => evc.m_CoreViewModel.DisplaySettingsViewModel.EarnedValueShowMilestones)
                .ToProperty(this, evc => evc.ShowMilestones);

            m_CombineResources = this
                .WhenAnyValue(evc => evc.m_CoreViewModel.DisplaySettingsViewModel.EarnedValueCombineResources)
                .ToProperty(this, evc => evc.CombineResources);

            m_ScaleToOwnPlan = this
                .WhenAnyValue(evc => evc.m_CoreViewModel.DisplaySettingsViewModel.EarnedValueScaleToOwnPlan)
                .ToProperty(this, evc => evc.ScaleToOwnPlan);

            m_HasResources = this
                .WhenAnyValue(evc => evc.m_CoreViewModel.HasResources)
                .ToProperty(this, evc => evc.HasResources);

            m_HasSingleTrackingSeriesSet = this
                .WhenAnyValue(
                    evc => evc.HasResources,
                    evc => evc.CombineResources,
                    evc => evc.ResourceSelector.TargetResourcesString,
                    (hasResources, combineResources, _) =>
                        !hasResources
                        || combineResources
                        || ResourceSelector.SelectedResourceIds.Count <= 1)
                .ToProperty(this, evc => evc.HasSingleTrackingSeriesSet);

            m_BuildEarnedValueChartPlotModelSub = Observable.Merge(
                    this.WhenAnyValue(
                        evc => evc.m_CoreViewModel.TrackingSeriesSet,
                        evc => evc.m_CoreViewModel.DisplaySettingsViewModel.ShowDates,
                        evc => evc.m_CoreViewModel.DisplaySettingsViewModel.UseClassicDates,
                        evc => evc.m_CoreViewModel.DisplaySettingsViewModel.NonWorkingDayMode,
                        evc => evc.ShowToday,
                        evc => evc.ShowMilestones,
                        evc => evc.m_CoreViewModel.ProjectStart,
                        evc => evc.m_CoreViewModel.Today,
                        evc => evc.m_CoreViewModel.DisplaySettingsViewModel.EarnedValueShowProjections,
                        evc => evc.m_CoreViewModel.BaseTheme,
                        (_, _, _, _, _, _, _, _, _, _) => Unit.Default),
                    // The resource filter and its display modes are view-side state:
                    // changing them only replots the chart, it never recompiles.
                    this.WhenAnyValue(
                        evc => evc.ResourceSelector.TargetResourcesString,
                        evc => evc.CombineResources,
                        evc => evc.ScaleToOwnPlan,
                        (_, _, _) => Unit.Default))
                .MuteWhile(this.WhenAnyValue(evc => evc.m_CoreViewModel.IsBulkUpdating)) // Conflate redundant notifications while a project scenario is loaded/reset.
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(async _ => await BuildEarnedValueChartPlotModelAsync());

            Id = Resource.ProjectPlan.Titles.Title_EarnedValueChartView;
            Title = Resource.ProjectPlan.Titles.Title_EarnedValueChartView;
        }

        #endregion

        #region Properties

        private AvaPlot m_EarnedValueChartPlotModel;
        public AvaPlot EarnedValueChartPlotModel
        {
            get
            {
                return m_EarnedValueChartPlotModel;
            }
            private set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_EarnedValueChartPlotModel, value);
                }
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

        private static EarnedValueSeriesGroup ToSeriesGroup(
            string? titleSuffix,
            ColorFormatModel? colorFormat,
            TrackingSeriesSetModel trackingSeriesSet)
        {
            return new EarnedValueSeriesGroup
            {
                TitleSuffix = titleSuffix,
                ColorFormat = colorFormat,
                Plan = trackingSeriesSet.Plan,
                PlanProjection = trackingSeriesSet.PlanProjection,
                Progress = trackingSeriesSet.Progress,
                ProgressProjection = trackingSeriesSet.ProgressProjection,
                Effort = trackingSeriesSet.Effort,
                EffortProjection = trackingSeriesSet.EffortProjection,
            };
        }

        private static List<TrackingPointModel> RescalePoints(
            IEnumerable<TrackingPointModel> pointSeries,
            double totalWorkingTime)
        {
            // The raw values are working-time numerators, so the percentages
            // can be recomputed against whichever denominator is displayed.
            return [.. pointSeries.Select(p => new TrackingPointModel
            {
                Time = p.Time,
                ActivityId = p.ActivityId,
                ActivityName = p.ActivityName,
                Value = p.Value,
                ValuePercentage = totalWorkingTime == 0 ? 0.0 : 100.0 * p.Value / totalWorkingTime,
            })];
        }

        private (EarnedValueSeriesGroup primary, List<EarnedValueSeriesGroup> seriesGroups) GatherEarnedValueSeriesGroups()
        {
            TrackingSeriesSetModel trackingSeriesSet = m_CoreViewModel.TrackingSeriesSet;
            List<int> selectedResourceIds = [.. ResourceSelector.SelectedResourceIds];

            // With no resources selected the chart shows the whole-project
            // aggregate, exactly as it always has.
            if (!HasResources
                || selectedResourceIds.Count == 0)
            {
                EarnedValueSeriesGroup aggregate = ToSeriesGroup(null, null, trackingSeriesSet);
                return (aggregate, [aggregate]);
            }

            double wholeProjectTotal = trackingSeriesSet.TotalWorkingTime;

            if (CombineResources)
            {
                TrackingSeriesSetModel combined = m_ResourceSchedulingService.CombineResourceTrackingSeries(
                    trackingSeriesSet,
                    selectedResourceIds);

                // The combined set records its percentages against its own
                // total, so only the whole-project scale needs recomputing.
                EarnedValueSeriesGroup seriesGroup = ScaleToOwnPlan
                    ? ToSeriesGroup(null, null, combined)
                    : new EarnedValueSeriesGroup
                    {
                        Plan = RescalePoints(combined.Plan, wholeProjectTotal),
                        PlanProjection = RescalePoints(combined.PlanProjection, wholeProjectTotal),
                        Progress = RescalePoints(combined.Progress, wholeProjectTotal),
                        ProgressProjection = RescalePoints(combined.ProgressProjection, wholeProjectTotal),
                        Effort = RescalePoints(combined.Effort, wholeProjectTotal),
                        EffortProjection = RescalePoints(combined.EffortProjection, wholeProjectTotal),
                    };

                return (seriesGroup, [seriesGroup]);
            }

            // One series group per selected resource, in display order.
            HashSet<int> selectedResourceLookup = [.. selectedResourceIds];
            List<EarnedValueSeriesGroup> seriesGroups = [];

            foreach (ResourceTrackingSeriesModel resourceSeries in trackingSeriesSet.ByResource)
            {
                if (!selectedResourceLookup.Contains(resourceSeries.ResourceId))
                {
                    continue;
                }

                double totalWorkingTime = ScaleToOwnPlan ? resourceSeries.TotalWorkingTime : wholeProjectTotal;

                seriesGroups.Add(new EarnedValueSeriesGroup
                {
                    TitleSuffix = string.IsNullOrWhiteSpace(resourceSeries.ResourceName)
                        ? resourceSeries.ResourceId.ToString(CultureInfo.InvariantCulture)
                        : resourceSeries.ResourceName,
                    ColorFormat = resourceSeries.ColorFormat,
                    Plan = RescalePoints(resourceSeries.Plan, totalWorkingTime),
                    PlanProjection = RescalePoints(resourceSeries.PlanProjection, totalWorkingTime),
                    Progress = RescalePoints(resourceSeries.Progress, totalWorkingTime),
                    ProgressProjection = RescalePoints(resourceSeries.ProgressProjection, totalWorkingTime),
                    Effort = RescalePoints(resourceSeries.Effort, totalWorkingTime),
                    EffortProjection = RescalePoints(resourceSeries.EffortProjection, totalWorkingTime),
                });
            }

            // With exactly one resource on display, the annotations (the
            // milestones, the projected finish and the empty-chart check)
            // anchor to its drawn lines, just as combine mode anchors to the
            // combined selection. With multiple resources they fall back to
            // the whole-project aggregate (milestones and projections are
            // suppressed then anyway).
            EarnedValueSeriesGroup primary = seriesGroups.Count == 1
                ? seriesGroups[0]
                : ToSeriesGroup(null, null, trackingSeriesSet);
            return (primary, seriesGroups);
        }

        private static AvaPlot BuildEarnedValueChartPlotModelInternal(
            IDateTimeCalculator dateTimeCalculator,
            EarnedValueSeriesGroup primary,
            IList<EarnedValueSeriesGroup> seriesGroups,
            bool showToday,
            bool showMilestones,
            bool showDates,
            DateTimeOffset projectStart,
            DateTimeOffset today,
            IGraphCompilation<int, int, int, IDependentActivity> graphCompilation,
            bool showProjections,
            BaseTheme baseTheme)
        {
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            ArgumentNullException.ThrowIfNull(primary);
            ArgumentNullException.ThrowIfNull(seriesGroups);

            // Milestones and projections are only drawn against a single set
            // of plan, progress and effort lines; with multiple uncombined
            // resources on display they are suppressed.
            bool hasSingleTrackingSeriesSet = seriesGroups.Count <= 1;
            showProjections = showProjections && hasSingleTrackingSeriesSet;
            showMilestones = showMilestones && hasSingleTrackingSeriesSet;

            var plotModel = new AvaPlot();
            plotModel.Plot.HideGrid();

            if (primary.Plan.Count == 0)
            {
                return plotModel.SetBaseTheme(baseTheme);
            }

            const double defaultMaxPercentage = 100.0;

            int chartEnd = seriesGroups
                .SelectMany(x => x.Plan.Concat(x.Progress).Concat(x.Effort))
                .Select(x => x.Time).DefaultIfEmpty().Max();

            double maxPercentage = seriesGroups
                .SelectMany(x => x.Plan.Concat(x.Progress).Concat(x.Effort))
                .Select(x => x.ValuePercentage).DefaultIfEmpty(defaultMaxPercentage).Max();

            if (showProjections)
            {
                chartEnd = Math.Max(chartEnd, seriesGroups
                    .SelectMany(x => x.PlanProjection.Concat(x.ProgressProjection).Concat(x.EffortProjection))
                    .Select(x => x.Time).DefaultIfEmpty().Max());

                maxPercentage = Math.Max(maxPercentage, seriesGroups
                    .SelectMany(x => x.PlanProjection.Concat(x.ProgressProjection).Concat(x.EffortProjection))
                    .Select(x => x.ValuePercentage).DefaultIfEmpty(defaultMaxPercentage).Max());
            }

            BuildEarnedValueChartXAxis(plotModel, dateTimeCalculator, chartEnd, showDates, projectStart);
            BuildEarnedValueChartYAxis(plotModel, maxPercentage);

            plotModel.Plot.Legend.OutlineWidth = 1;
            plotModel.Plot.Legend.BackgroundColor = Colors.Transparent;
            plotModel.Plot.Legend.ShadowColor = Colors.Transparent;
            plotModel.Plot.Legend.ShadowOffset = new(0, 0);

            plotModel.Plot.ShowLegend(Edge.Right);

            if (showProjections)
            {
                HorizontalLine line = plotModel.Plot.Add.HorizontalLine(
                    defaultMaxPercentage,
                    width: 1,
                    pattern: LinePattern.Dashed);

                line.LabelText = Resource.ProjectPlan.Labels.Label_ProjectCompletion;
                line.LabelBackgroundColor = Colors.Transparent;
                line.LabelFontSize = PlotHelper.FontSize;
                line.LabelBold = false;
                line.LabelRotation = 0;
                line.LabelOffsetX = 100;
                line.LabelOffsetY = 25;
            }

            const float mainStrokeThickness = 2;
            const float projectionStrokeThickness = 1;

            foreach (EarnedValueSeriesGroup seriesGroup in seriesGroups)
            {
                // Groups without a colour of their own (the whole project, or
                // the combined selection) use the classic per-measure colours;
                // groups representing individual resources use the resource
                // colour and are distinguished by line pattern instead.
                bool useResourceStyling = seriesGroup.ColorFormat is not null;

                Color planColor = useResourceStyling
                    ? ColorHelper.ColorFormatToScottPlotColor(seriesGroup.ColorFormat!)
                    : Colors.Blue;
                Color progressColor = useResourceStyling ? planColor : Colors.Green;
                Color effortColor = useResourceStyling ? planColor : Colors.Red;

                string suffix = string.IsNullOrWhiteSpace(seriesGroup.TitleSuffix)
                    ? string.Empty
                    : $@" - {seriesGroup.TitleSuffix}";

                AddScatterPlot(
                    title: $@"{Resource.ProjectPlan.Labels.Label_Plan}{suffix}",
                    stroke: mainStrokeThickness,
                    color: planColor.WithAlpha(ColorHelper.AnnotationAFull),
                    showDates,
                    projectStart,
                    dateTimeCalculator,
                    plotModel,
                    seriesGroup.Plan);

                AddScatterPlot(
                    title: $@"{Resource.ProjectPlan.Labels.Label_Progress}{suffix}",
                    stroke: mainStrokeThickness,
                    color: progressColor.WithAlpha(ColorHelper.AnnotationAFull),
                    showDates,
                    projectStart,
                    dateTimeCalculator,
                    plotModel,
                    seriesGroup.Progress,
                    pattern: useResourceStyling ? LinePattern.Dashed : null);

                AddScatterPlot(
                    title: $@"{Resource.ProjectPlan.Labels.Label_Effort}{suffix}",
                    stroke: mainStrokeThickness,
                    color: effortColor.WithAlpha(ColorHelper.AnnotationAFull),
                    showDates,
                    projectStart,
                    dateTimeCalculator,
                    plotModel,
                    seriesGroup.Effort,
                    pattern: useResourceStyling ? LinePattern.Dotted : null);

                if (showProjections)
                {
                    // Keep the legend compact: projections for individual
                    // resources are drawn but not listed.
                    AddScatterPlot(
                        title: useResourceStyling ? string.Empty : Resource.ProjectPlan.Labels.Label_PlanProjection,
                        stroke: projectionStrokeThickness,
                        color: planColor.WithAlpha(ColorHelper.AnnotationAMedium),
                        showDates,
                        projectStart,
                        dateTimeCalculator,
                        plotModel,
                        seriesGroup.PlanProjection);

                    AddScatterPlot(
                        title: useResourceStyling ? string.Empty : Resource.ProjectPlan.Labels.Label_ProgressProjection,
                        stroke: projectionStrokeThickness,
                        color: progressColor.WithAlpha(ColorHelper.AnnotationAMedium),
                        showDates,
                        projectStart,
                        dateTimeCalculator,
                        plotModel,
                        seriesGroup.ProgressProjection,
                        pattern: useResourceStyling ? LinePattern.Dashed : null);

                    AddScatterPlot(
                        title: useResourceStyling ? string.Empty : Resource.ProjectPlan.Labels.Label_EffortProjection,
                        stroke: projectionStrokeThickness,
                        color: effortColor.WithAlpha(ColorHelper.AnnotationAMedium),
                        showDates,
                        projectStart,
                        dateTimeCalculator,
                        plotModel,
                        seriesGroup.EffortProjection,
                        pattern: useResourceStyling ? LinePattern.Dotted : null);
                }
            }

            if (showProjections)
            {
                // Find projected completion time.
                AddProjectedFinish(
                    dateTimeCalculator,
                    primary.ProgressProjection,
                    showDates,
                    projectStart,
                    plotModel);
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

            if (showMilestones)
            {
                List<IDependentActivity> milestones = [.. graphCompilation
                    .DependentActivities
                    .OrderBy(x => x.EarliestStartTime)
                    .Where(x => x.Duration == 0)];

                var milestoneParameters = new List<(string label, int startTime, double peakPercentage)>();

                foreach (IDependentActivity milestone in milestones)
                {
                    // Here we need to find the highest peak along the plan
                    // line where the milestone needs to be positioned on the
                    // Y axis.
                    string id = milestone.Id.ToString(CultureInfo.InvariantCulture);
                    string label = string.IsNullOrWhiteSpace(milestone.Name) ? id : $"{milestone.Name} ({id})";
                    int startTime = milestone.EarliestStartTime.GetValueOrDefault();

                    double peakPercentage = primary.Plan
                        .Where(x => x.Time == startTime)
                        .DefaultIfEmpty()
                        .Max(x => x?.ValuePercentage ?? 0);

                    milestoneParameters.Add((label, startTime, peakPercentage));
                }

                var milestoneArrows = new List<AnnotatedArrow>();

                foreach (var (label, startTime, peakPercentage) in milestoneParameters)
                {
                    double milestoneTimeX = ChartHelper.CalculateChartStartTimeXValue(
                        startTime,
                        showDates,
                        projectStart,
                        dateTimeCalculator);

                    AnnotatedArrow milestoneArrow = MilestoneAnnotation(
                        milestoneTimeX,
                        c_ArrowHeadHeight,
                        peakPercentage,
                        label,
                        Colors.Yellow);

                    milestoneArrows.Add(milestoneArrow);
                }

                plotModel.Plot.PlottableList.AddRange(milestoneArrows);
            }

            // Style the plot so the bars start on the left edge.
            plotModel.Plot.Axes.Margins(left: 0, right: 0, bottom: 0, top: 0);

            plotModel.Plot.Axes.AutoScale();

            return plotModel.SetBaseTheme(baseTheme);
        }

        private static void AddProjectedFinish(
            IDateTimeCalculator dateTimeCalculator,
            IList<TrackingPointModel> progressProjection,
            bool showDates,
            DateTimeOffset projectStart,
            AvaPlot plotModel)
        {
            var projectFinishDisplay = new StringBuilder(Resource.ProjectPlan.Labels.Label_ProjectedFinish);
            projectFinishDisplay.Append(' ');

            int projectedFinishTime = progressProjection.Select(x => x.Time).DefaultIfEmpty().Max();

            if (showDates)
            {
                DateTimeOffset startAndFinish = dateTimeCalculator.AddDays(projectStart, projectedFinishTime);
                string projectFinish = dateTimeCalculator
                    .DisplayFinishDate(startAndFinish, startAndFinish, 1)
                    .ToString(DateTimeCalculator.DateFormat);

                projectFinishDisplay.Append(projectFinish);
            }
            else
            {
                projectFinishDisplay.Append(projectedFinishTime);
            }

            Annotation annotation = plotModel.Plot.Add.Annotation(projectFinishDisplay.ToString(), Alignment.LowerRight);
            annotation.LabelBackgroundColor = Colors.Transparent;
            annotation.LabelBorderColor = Colors.Transparent;
            annotation.LabelShadowColor = Colors.Transparent;
        }

        private static AnnotatedArrow MilestoneAnnotation(
            double start,
            float startDelta,
            double peakPercentage,
            string label,
            Color color)
        {
            double Y = peakPercentage;
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

        private static IXAxis BuildEarnedValueChartXAxis(
            AvaPlot plotModel,
            IDateTimeCalculator dateTimeCalculator,
            int chartEnd,
            bool showDates,
            DateTimeOffset projectStart)
        {
            ArgumentNullException.ThrowIfNull(plotModel);
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);

            IXAxis xAxis = plotModel.Plot.Axes.Bottom;

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
                    // Setup the plot to display X axis tick labels using date time format.
                    xAxis = plotModel.Plot.Axes.DateTimeTicksBottom();
                }

                xAxis.Min = minValue;
                xAxis.Max = maxValue;
                xAxis.Label.Text = Resource.ProjectPlan.Labels.Label_TimeAxisTitle;
                xAxis.Label.FontSize = PlotHelper.FontSize;
                xAxis.Label.Bold = false;
            }

            return xAxis;
        }

        private static IYAxis BuildEarnedValueChartYAxis(
            AvaPlot plotModel,
            double maximum)
        {
            ArgumentNullException.ThrowIfNull(plotModel);
            IYAxis yAxis = plotModel.Plot.Axes.Left;

            yAxis.Min = 0.0;
            yAxis.Max = maximum;
            yAxis.Label.Text = Resource.ProjectPlan.Labels.Label_PercentageAxisTitle;
            yAxis.Label.FontSize = PlotHelper.FontSize;
            yAxis.Label.Bold = false;
            return yAxis;
        }

        private static void AddScatterPlot(
            string title,
            float stroke,
            Color color,
            bool showDates,
            DateTimeOffset projectStart,
            IDateTimeCalculator dateTimeCalculator,
            AvaPlot plotModel,
            IList<TrackingPointModel> pointSeries,
            LinePattern? pattern = null)
        {
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            ArgumentNullException.ThrowIfNull(plotModel);
            ArgumentNullException.ThrowIfNull(pointSeries);

            var dataX = new List<double>();
            var dataY = new List<double>();

            if (pointSeries.Any())
            {
                foreach (TrackingPointModel planPoint in pointSeries)
                {
                    dataX.Add(
                        ChartHelper.CalculateChartStartTimeXValue(
                            planPoint.Time,
                            showDates,
                            projectStart,
                            dateTimeCalculator));
                    dataY.Add(planPoint.ValuePercentage);
                }
                Scatter scatter = plotModel.Plot.Add.Scatter(dataX, dataY);
                scatter.LegendText = title;
                scatter.LineWidth = stroke;
                scatter.Color = color;
                scatter.MarkerSize = 0;

                if (pattern is not null)
                {
                    scatter.LinePattern = pattern.Value;
                }
            }
        }

        private void ResetEarnedValueChart()
        {
            EarnedValueChartPlotModel.Plot.Axes.AutoScale();
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
                lock (m_Lock) m_CoreViewModel.DisplaySettingsViewModel.EarnedValueShowProjections = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_ShowToday;
        public bool ShowToday
        {
            get => m_ShowToday.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.DisplaySettingsViewModel.EarnedValueShowToday = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_ShowMilestones;
        public bool ShowMilestones
        {
            get => m_ShowMilestones.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.DisplaySettingsViewModel.EarnedValueShowMilestones = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_CombineResources;
        public bool CombineResources
        {
            get => m_CombineResources.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.DisplaySettingsViewModel.EarnedValueCombineResources = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_ScaleToOwnPlan;
        public bool ScaleToOwnPlan
        {
            get => m_ScaleToOwnPlan.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.DisplaySettingsViewModel.EarnedValueScaleToOwnPlan = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_HasResources;
        public bool HasResources => m_HasResources.Value;

        // Whether the chart displays a single set of plan, progress and
        // effort lines: the whole-project aggregate, the combined selection,
        // or a single selected resource.
        private readonly ObservableAsPropertyHelper<bool> m_HasSingleTrackingSeriesSet;
        public bool HasSingleTrackingSeriesSet => m_HasSingleTrackingSeriesSet.Value;

        public IResourceSelectorViewModel ResourceSelector { get; }

        public ICommand ResetEarnedValueChartCommand { get; }

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
                    await m_ScottPlotImageExporter.SavePlotImageAsync(EarnedValueChartPlotModel.Plot, filename, width, height);
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
            CascadeDiagnostics.RecordBuild($@"{nameof(EarnedValueChartManagerViewModel)}.{nameof(BuildEarnedValueChartPlotModel)}");
            AvaPlot? plotModel = null;

            lock (m_Lock)
            {
                if (!HasCompilationErrors)
                {
                    (EarnedValueSeriesGroup primary, List<EarnedValueSeriesGroup> seriesGroups) = GatherEarnedValueSeriesGroups();

                    plotModel = BuildEarnedValueChartPlotModelInternal(
                        m_DateTimeCalculator,
                        primary,
                        seriesGroups,
                        ShowToday,
                        ShowMilestones,
                        m_CoreViewModel.DisplaySettingsViewModel.ShowDates,
                        m_CoreViewModel.ProjectStart,
                        m_CoreViewModel.Today,
                        m_CoreViewModel.GraphCompilation,
                        m_CoreViewModel.DisplaySettingsViewModel.EarnedValueShowProjections,
                        m_CoreViewModel.BaseTheme);
                }
            }

            plotModel ??= new AvaPlot();
            plotModel.ClearContextMenu();
            AvaPlot outgoing = EarnedValueChartPlotModel;
            EarnedValueChartPlotModel = plotModel;
            m_PlotRetirer.Retire(outgoing);
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

            return await m_ScottPlotImageExporter.RenderPlotImageAsync(EarnedValueChartPlotModel.Plot, width, height);
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
            m_ResourceSelector.KillSubscriptions();
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
                KillSubscriptions();
                m_ResourceSelector.Dispose();
                m_PlotRetirer.Dispose();
                m_EarnedValueChartPlotModel.Plot.Dispose();
                m_IsBusy?.Dispose();
                m_HasStaleOutputs?.Dispose();
                m_HasCompilationErrors?.Dispose();
                m_ShowProjections?.Dispose();
                m_ShowToday?.Dispose();
                m_ShowMilestones?.Dispose();
                m_CombineResources?.Dispose();
                m_ScaleToOwnPlan?.Dispose();
                m_HasResources?.Dispose();
                m_HasSingleTrackingSeriesSet?.Dispose();
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

        #region Private Types

        /// <summary>
        /// A set of tracking point series drawn as one legend group: either the
        /// whole project, the combined selection, or an individual resource
        /// (which carries its own colour and title suffix).
        /// </summary>
        private sealed record EarnedValueSeriesGroup
        {
            public string? TitleSuffix { get; init; }

            public ColorFormatModel? ColorFormat { get; init; }

            public IList<TrackingPointModel> Plan { get; init; } = [];
            public IList<TrackingPointModel> PlanProjection { get; init; } = [];

            public IList<TrackingPointModel> Progress { get; init; } = [];
            public IList<TrackingPointModel> ProgressProjection { get; init; } = [];

            public IList<TrackingPointModel> Effort { get; init; } = [];
            public IList<TrackingPointModel> EffortProjection { get; init; } = [];
        }

        #endregion
    }
}
