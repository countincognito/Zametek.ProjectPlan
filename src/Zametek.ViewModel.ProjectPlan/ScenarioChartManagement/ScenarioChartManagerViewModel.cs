using Avalonia;
using Avalonia.Threading;
using DynamicData;
using java.awt;
using ReactiveUI;
using ScottPlot;
using ScottPlot.Avalonia;
using ScottPlot.DataSources;
using ScottPlot.Plottables;
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

        private const float c_ScatterLineWidth = 5.0f;

        private const float c_VerticalLineWidth = 2.0f;

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

            //m_AllocationMode = this
            //    .WhenAnyValue(rcm => rcm.m_CoreViewModel.DisplaySettingsViewModel.ResourceChartAllocationMode)
            //    .ToProperty(this, rcm => rcm.AllocationMode);

            //m_ScheduleMode = this
            //    .WhenAnyValue(rcm => rcm.m_CoreViewModel.DisplaySettingsViewModel.ResourceChartScheduleMode)
            //    .ToProperty(this, rcm => rcm.ScheduleMode);

            //m_DisplayStyle = this
            //    .WhenAnyValue(rcm => rcm.m_CoreViewModel.DisplaySettingsViewModel.ResourceChartDisplayStyle)
            //    .ToProperty(this, rcm => rcm.DisplayStyle);

            //m_ShowToday = this
            //    .WhenAnyValue(rcm => rcm.m_CoreViewModel.DisplaySettingsViewModel.ResourceChartShowToday)
            //    .ToProperty(this, rcm => rcm.ShowToday);

            //m_ShowMilestones = this
            //    .WhenAnyValue(rcm => rcm.m_CoreViewModel.DisplaySettingsViewModel.ResourceChartShowMilestones)
            //    .ToProperty(this, rcm => rcm.ShowMilestones);

            m_BuildScenarioChartPlotModelSub = this
                .WhenAnyValue(
                    rcm => rcm.m_ProjectScenarioManagerViewModel.TrackedMetricsSet,
                    rcm => rcm.m_CoreViewModel.DisplaySettingsViewModel.ShowDates,
                    rcm => rcm.m_CoreViewModel.DisplaySettingsViewModel.UseClassicDates,
                    rcm => rcm.m_CoreViewModel.DisplaySettingsViewModel.UseBusinessDays,
                    rcm => rcm.m_CoreViewModel.ProjectStart,
                    rcm => rcm.m_CoreViewModel.BaseTheme)
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
            IDateTimeCalculator dateTimeCalculator,
            bool showDates,
            DateTimeOffset projectStart,
            TrackedMetrics xMetric,
            TrackedMetrics yMetric,
            BaseTheme baseTheme)
        {
            ArgumentNullException.ThrowIfNull(trackedMetricsSet);
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            var plotModel = new AvaPlot();
            plotModel.Plot.HideGrid();

            // Select the metric for the X axis.
            Func<MetricsModel, double>? xMetricFunction = null;

            xMetricFunction = xMetric switch
            {
                TrackedMetrics.RisksCriticality => (MetricsModel model) => model.Risks.Criticality.GetValueOrDefault(),
                TrackedMetrics.RisksFibonacci => throw new NotImplementedException(),
                TrackedMetrics.RisksActivity => (MetricsModel model) => model.Risks.Activity.GetValueOrDefault(),
                TrackedMetrics.RisksActivityStdDevCorrection => throw new NotImplementedException(),
                TrackedMetrics.RisksGeometricCriticality => throw new NotImplementedException(),
                TrackedMetrics.RisksGeometricFibonacci => throw new NotImplementedException(),
                TrackedMetrics.RisksGeometricActivity => throw new NotImplementedException(),
                TrackedMetrics.CostsDirect => throw new NotImplementedException(),
                TrackedMetrics.CostsIndirect => throw new NotImplementedException(),
                TrackedMetrics.CostsOther => throw new NotImplementedException(),
                TrackedMetrics.CostsTotal => throw new NotImplementedException(),
                TrackedMetrics.BillingsDirect => throw new NotImplementedException(),
                TrackedMetrics.BillingsIndirect => throw new NotImplementedException(),
                TrackedMetrics.BillingsOther => throw new NotImplementedException(),
                TrackedMetrics.BillingsTotal => throw new NotImplementedException(),
                TrackedMetrics.MarginsDirect => throw new NotImplementedException(),
                TrackedMetrics.MarginsIndirect => throw new NotImplementedException(),
                TrackedMetrics.MarginsOther => throw new NotImplementedException(),
                TrackedMetrics.MarginsTotal => throw new NotImplementedException(),
                TrackedMetrics.MarginsDirectAbsolute => throw new NotImplementedException(),
                TrackedMetrics.MarginsIndirectAbsolute => throw new NotImplementedException(),
                TrackedMetrics.MarginsOtherAbsolute => throw new NotImplementedException(),
                TrackedMetrics.MarginsTotalAbsolute => throw new NotImplementedException(),
                TrackedMetrics.EffortsDirect => throw new NotImplementedException(),
                TrackedMetrics.EffortsIndirect => throw new NotImplementedException(),
                TrackedMetrics.EffortsOther => throw new NotImplementedException(),
                TrackedMetrics.EffortsTotal => throw new NotImplementedException(),
                TrackedMetrics.EffortsActivity => throw new NotImplementedException(),
                TrackedMetrics.EffortsEfficiency => throw new NotImplementedException(),
                TrackedMetrics.NetworkCyclomaticComplexity => throw new NotImplementedException(),
                TrackedMetrics.NetworkDuration => (MetricsModel model) => model.Network.Duration.GetValueOrDefault(),
                TrackedMetrics.NetworkDurationManMonths => throw new NotImplementedException(),
                _ => throw new ArgumentOutOfRangeException(nameof(xMetric), @$"{Resource.ProjectPlan.Messages.Message_UnknownTrackedMetric} {xMetric}"),
            };

            // Select the metric for the Y axis.
            Func<MetricsModel, double>? yMetricFunction = null;

            yMetricFunction = (MetricsModel model) => model.Risks.Criticality.GetValueOrDefault();








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
                dataX.Add(xMetricFunction(trackedMetrics.Metrics));
                dataY.Add(yMetricFunction(trackedMetrics.Metrics));
            }


            Scatter scatter = plotModel.Plot.Add.Scatter(dataX, dataY);
            //scatter.LegendText = title;
            scatter.LineWidth = 0;
            scatter.MarkerSize = 10;
            //scatter.LineColor = Colors.Transparent;
            //scatter.MarkerSize = 0;






            //// Build the X axis.

            //{
            //    //int chartEnd = trackingSeriesSet.Plan
            //    //    .Concat(trackingSeriesSet.Progress)
            //    //    .Concat(trackingSeriesSet.Effort)
            //    //    .Select(x => x.Time).DefaultIfEmpty().Max();



            //    //IXAxis xAxis = plotModel.Plot.Axes.Bottom;

            //    //double minValue = ChartHelper.CalculateChartStartTimeXValue(
            //    //    chartStart,
            //    //    showDates,
            //    //    projectStart,
            //    //    dateTimeCalculator);
            //    //double maxValue = ChartHelper.CalculateChartFinishTimeXValue(
            //    //    chartEnd,
            //    //    showDates,
            //    //    projectStart,
            //    //    dateTimeCalculator);

            //    //if (showDates)
            //    //{
            //    //    // Setup the plot to display X axis tick labels using date time format.
            //    //    xAxis = plotModel.Plot.Axes.DateTimeTicksBottom();
            //    //}

            //    //xAxis.Min = minValue;
            //    //xAxis.Max = maxValue;
            //    //xAxis.Label.Text = Resource.ProjectPlan.Labels.Label_TimeAxisTitle;
            //    //xAxis.Label.FontSize = PlotHelper.FontSize;
            //    //xAxis.Label.Bold = false;
            //}











            //IEnumerable<ResourceSeriesModel> resourceSeries = scheduleFunction(resourceSeriesSet).OrderBy(x => x.DisplayOrder);
            //int finishTime = resourceSeriesSet.ResourceSchedules.Select(x => x.FinishTime).DefaultIfEmpty().Max();

            //BuildScenarioChartXAxis(plotModel, dateTimeCalculator, finishTime, showDates, projectStart);







            //plotModel.Plot.Legend.OutlineWidth = 1;
            //plotModel.Plot.Legend.BackgroundColor = Colors.Transparent;
            //plotModel.Plot.Legend.ShadowColor = Colors.Transparent;
            //plotModel.Plot.Legend.ShadowOffset = new(0, 0);

            //plotModel.Plot.ShowLegend(Edge.Right);










            //var scatters = new List<Scatter>();
            //var labels = new List<string>();

            //if (resourceSeries.Any())
            //{
            //    IList<int> total1 = [];
            //    IList<int> total2 = [];

            //    foreach (ResourceSeriesModel series in resourceSeries)
            //    {
            //        if (series != null)
            //        {
            //            var color = new Color(
            //                series.ColorFormat.R,
            //                series.ColorFormat.G,
            //                series.ColorFormat.B,
            //                series.ColorFormat.A);

            //            IList<double> xs = [];
            //            IList<double> ys = [];

            //            switch (displayStyle)
            //            {
            //                case DisplayStyle.Slanted:
            //                    {
            //                        if (allocationFunction(series.ResourceSchedule).Count != 0)
            //                        {
            //                            // Mark the start of the plot.
            //                            xs.Add(ChartHelper.CalculateChartStartTimeXValue(0, showDates, projectStart, dateTimeCalculator));
            //                            ys.Add(0.0);

            //                            for (int i = 0; i < allocationFunction(series.ResourceSchedule).Count; i++)
            //                            {
            //                                bool allocationExists = allocationFunction(series.ResourceSchedule)[i];

            //                                if (i >= total1.Count)
            //                                {
            //                                    total1.Add(0);
            //                                }

            //                                int dayNumber = i + 1;

            //                                xs.Add(ChartHelper.CalculateChartStartTimeXValue(dayNumber, showDates, projectStart, dateTimeCalculator));
            //                                total1[i] += allocationExists ? 1 : 0;
            //                                ys.Add(total1[i]);
            //                            }
            //                        }
            //                    }
            //                    break;
            //                case DisplayStyle.Block:
            //                    {
            //                        if (allocationFunction(series.ResourceSchedule).Count != 0)
            //                        {
            //                            for (int i = 0; i < allocationFunction(series.ResourceSchedule).Count; i++)
            //                            {
            //                                bool allocationExists = allocationFunction(series.ResourceSchedule)[i];

            //                                // First point.
            //                                int dayNumber = i;

            //                                if (dayNumber >= total1.Count)
            //                                {
            //                                    total1.Add(0);
            //                                }

            //                                xs.Add(ChartHelper.CalculateChartStartTimeXValue(dayNumber, showDates, projectStart, dateTimeCalculator));
            //                                total1[dayNumber] += allocationExists ? 1 : 0;
            //                                ys.Add(total1[dayNumber]);

            //                                // Second point.
            //                                if (dayNumber >= total2.Count)
            //                                {
            //                                    total2.Add(0);
            //                                }

            //                                dayNumber++;

            //                                if (dayNumber >= total2.Count)
            //                                {
            //                                    total2.Add(0);
            //                                }

            //                                xs.Add(ChartHelper.CalculateChartStartTimeXValue(dayNumber, showDates, projectStart, dateTimeCalculator));
            //                                total2[dayNumber] += allocationExists ? 1 : 0;
            //                                ys.Add(total2[dayNumber]);
            //                            }
            //                        }
            //                    }
            //                    break;
            //                default:
            //                    throw new ArgumentOutOfRangeException(nameof(displayStyle), @$"{Resource.ProjectPlan.Messages.Message_UnknownDisplayStyle} {displayStyle}");
            //            }

            //            ScatterSourceDoubleArray source = new([.. xs], [.. ys]);
            //            Scatter scatter = new(source)
            //            {
            //                LegendText = series.Title,
            //                LineColor = color,
            //                LineWidth = c_ScatterLineWidth,
            //                MarkerFillColor = color,
            //                MarkerLineColor = color,
            //                MarkerSize = 0,
            //                FillY = true,
            //                FillYColor = color,
            //            };

            //            scatters.Add(scatter);
            //            labels.Add(series.Title);
            //        }
            //    }
            //}

            //scatters.Reverse();
            //plotModel.Plot.PlottableList.AddRange(scatters);

            //if (showToday)
            //{
            //    (int? intValue, _) = dateTimeCalculator.CalculateTimeAndDateTime(projectStart, today);

            //    if (intValue is not null)
            //    {
            //        double todayTimeX = ChartHelper.CalculateChartStartTimeXValue(
            //            intValue.GetValueOrDefault(),
            //            showDates,
            //            projectStart,
            //            dateTimeCalculator);

            //        plotModel.Plot.Add.VerticalLine(
            //            todayTimeX,
            //            width: c_VerticalLineWidth,
            //            pattern: LinePattern.Dotted);
            //    }
            //}

            //if (showMilestones)
            //{
            //    List<IDependentActivity> milestones = [.. graphCompilation
            //        .DependentActivities
            //        .OrderBy(x => x.EarliestStartTime)
            //        .Where(x => x.Duration == 0)];

            //    var milestoneLines = new List<AnnotatedVerticalLine>();

            //    foreach (IDependentActivity milestone in milestones)
            //    {
            //        string id = milestone.Id.ToString(CultureInfo.InvariantCulture);
            //        string label = string.IsNullOrWhiteSpace(milestone.Name) ? id : $"{milestone.Name} ({id})";

            //        double milestoneTimeX = ChartHelper.CalculateChartStartTimeXValue(
            //            milestone.EarliestStartTime.GetValueOrDefault(),
            //            showDates,
            //            projectStart,
            //            dateTimeCalculator);

            //        AnnotatedVerticalLine milestoneLine = MilestoneAnnotation(
            //            milestoneTimeX,
            //            c_VerticalLineWidth,
            //            label,
            //            Colors.White);

            //        milestoneLines.Add(milestoneLine);
            //    }

            //    plotModel.Plot.PlottableList.AddRange(milestoneLines);
            //}





            // Style the plot so the bars start on the left edge.
            //plotModel.Plot.Axes.Margins(left: 0, right: 0, bottom: 0, top: 0);





            //IYAxis yAxis = BuildScenarioChartYAxis(plotModel);

            //yAxis.SetTicks(
            //    [.. Enumerable.Range(0, labels.Count).Select(Convert.ToDouble)],
            //    [.. Enumerable.Range(0, labels.Count).Select(x => Convert.ToString(x))]);

            plotModel.Plot.Axes.AutoScale();

            return plotModel.SetBaseTheme(baseTheme);
        }
        //private static AnnotatedVerticalLine MilestoneAnnotation(
        //    double start,
        //    float width,
        //    string label,
        //    Color color)
        //{
        //    return new AnnotatedVerticalLine
        //    {
        //        Annotation = label,
        //        LineWidth = width,
        //        LineColor = color,
        //        LabelBackgroundColor = color,
        //        LinePattern = LinePattern.Dashed,
        //        X = start,
        //    };
        //}















        private static IAxis BuildEarnedValueChartXAxis(
            AvaPlot plotModel,
            IDateTimeCalculator dateTimeCalculator,
            int chartStart,
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
                    chartStart,
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












        //private static IXAxis BuildScenarioChartXAxis(
        //    AvaPlot plotModel,
        //    IDateTimeCalculator dateTimeCalculator,
        //    int finishTime,
        //    bool showDates,
        //    DateTimeOffset projectStart)
        //{
        //    ArgumentNullException.ThrowIfNull(plotModel);
        //    ArgumentNullException.ThrowIfNull(dateTimeCalculator);

        //    IXAxis xAxis = plotModel.Plot.Axes.Bottom;

        //    if (finishTime != default)
        //    {
        //        double minValue = ChartHelper.CalculateChartStartTimeXValue(0, showDates, projectStart, dateTimeCalculator);
        //        double maxValue = ChartHelper.CalculateChartFinishTimeXValue(finishTime, showDates, projectStart, dateTimeCalculator);

        //        if (showDates)
        //        {
        //            // Setup the plot to display X axis tick labels using date time format.
        //            xAxis = plotModel.Plot.Axes.DateTimeTicksBottom();
        //        }

        //        xAxis.Min = minValue;
        //        xAxis.Max = maxValue;
        //        xAxis.Label.Text = Resource.ProjectPlan.Labels.Label_TimeAxisTitle;
        //        xAxis.Label.FontSize = PlotHelper.FontSize;
        //        xAxis.Label.Bold = false;
        //    }

        //    return xAxis;
        //}

        //private static IYAxis BuildScenarioChartYAxis(AvaPlot plotModel)
        //{
        //    ArgumentNullException.ThrowIfNull(plotModel);
        //    IYAxis yAxis = plotModel.Plot.Axes.Left;
        //    yAxis.Min = 0.0;
        //    yAxis.Label.Text = Resource.ProjectPlan.Labels.Label_ScenariosAxisTitle;
        //    yAxis.Label.FontSize = PlotHelper.FontSize;
        //    yAxis.Label.Bold = false;
        //    return yAxis;
        //}






































        private async Task SaveScenarioChartImageFileAsync()
        {
            try
            {
                //string title = m_SettingService.ProjectTitle;
                //title = string.IsNullOrWhiteSpace(title) ? Resource.ProjectPlan.Titles.Title_UntitledProject : title;
                //string scenarioOutputFile = $@"{title}{Resource.ProjectPlan.Suffixes.Suffix_ScenarioChart}";
                //string directory = m_SettingService.ProjectDirectory;
                //string? filename = await m_DialogService.ShowSaveFileDialogAsync(scenarioOutputFile, directory, s_ExportFileFilters);

                //if (!string.IsNullOrWhiteSpace(filename)
                //    && ImageBounds is Rect bounds)
                //{
                //    int boundedWidth = Math.Abs(Convert.ToInt32(bounds.Width));
                //    int boundedHeight = Math.Abs(Convert.ToInt32(bounds.Height));

                //    await SaveScenarioChartImageFileAsync(filename, boundedWidth, boundedHeight);
                //}
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

        //private readonly ObservableAsPropertyHelper<AllocationMode> m_AllocationMode;
        //public AllocationMode AllocationMode
        //{
        //    get => m_AllocationMode.Value;
        //    set
        //    {
        //        lock (m_Lock) m_CoreViewModel.DisplaySettingsViewModel.ResourceChartAllocationMode = value;
        //    }
        //}

        //private readonly ObservableAsPropertyHelper<ScheduleMode> m_ScheduleMode;
        //public ScheduleMode ScheduleMode
        //{
        //    get => m_ScheduleMode.Value;
        //    set
        //    {
        //        lock (m_Lock) m_CoreViewModel.DisplaySettingsViewModel.ResourceChartScheduleMode = value;
        //    }
        //}

        //private readonly ObservableAsPropertyHelper<DisplayStyle> m_DisplayStyle;
        //public DisplayStyle DisplayStyle
        //{
        //    get => m_DisplayStyle.Value;
        //    set
        //    {
        //        lock (m_Lock) m_CoreViewModel.DisplaySettingsViewModel.ResourceChartDisplayStyle = value;
        //    }
        //}

        //private readonly ObservableAsPropertyHelper<bool> m_ShowToday;
        //public bool ShowToday
        //{
        //    get => m_ShowToday.Value;
        //    set
        //    {
        //        lock (m_Lock) m_CoreViewModel.DisplaySettingsViewModel.ResourceChartShowToday = value;
        //    }
        //}

        //private readonly ObservableAsPropertyHelper<bool> m_ShowMilestones;
        //public bool ShowMilestones
        //{
        //    get => m_ShowMilestones.Value;
        //    set
        //    {
        //        lock (m_Lock) m_CoreViewModel.DisplaySettingsViewModel.ResourceChartShowMilestones = value;
        //    }
        //}

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
                        m_DateTimeCalculator,
                        m_CoreViewModel.DisplaySettingsViewModel.ShowDates,
                        m_CoreViewModel.ProjectStart,
                        TrackedMetrics.NetworkDuration,
                        TrackedMetrics.RisksActivity,
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
                //m_AllocationMode?.Dispose();
                //m_ScheduleMode?.Dispose();
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
