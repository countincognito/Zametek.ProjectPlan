using Avalonia;
using ReactiveUI;
using ScottPlot;
using ScottPlot.Avalonia;
using ScottPlot.Plottables;
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
            m_EarnedValueChartPlotModel = new AvaPlot();

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

            m_BuildEarnedValueChartPlotModelSub = this
                .WhenAnyValue(
                    evc => evc.m_CoreViewModel.TrackingSeriesSet,
                    evc => evc.m_CoreViewModel.DisplaySettingsViewModel.ShowDates,
                    evc => evc.m_CoreViewModel.DisplaySettingsViewModel.UseClassicDates,
                    evc => evc.m_CoreViewModel.DisplaySettingsViewModel.UseBusinessDays,
                    evc => evc.ShowToday,
                    evc => evc.ShowMilestones,
                    evc => evc.m_CoreViewModel.ProjectStart,
                    evc => evc.m_CoreViewModel.Today,
                    evc => evc.m_CoreViewModel.DisplaySettingsViewModel.EarnedValueShowProjections,
                    evc => evc.m_CoreViewModel.BaseTheme,
                    (a, b, c, d, e, f, g, h, i, j) => (a, b, c, d, e, f, g, h, i, j))
                .ObserveOn(RxApp.MainThreadScheduler)
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

        private static AvaPlot BuildEarnedValueChartPlotModelInternal(
            IDateTimeCalculator dateTimeCalculator,
            TrackingSeriesSetModel trackingSeriesSet,
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
            ArgumentNullException.ThrowIfNull(trackingSeriesSet);
            var plotModel = new AvaPlot();
            plotModel.Plot.HideGrid();

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

            AddScatterPlot(
                title: Resource.ProjectPlan.Labels.Label_Plan,
                stroke: mainStrokeThickness,
                color: Colors.Blue.WithAlpha(ColorHelper.AnnotationAFull),
                showDates,
                projectStart,
                dateTimeCalculator,
                plotModel,
                trackingSeriesSet.Plan);

            AddScatterPlot(
                title: Resource.ProjectPlan.Labels.Label_Progress,
                stroke: mainStrokeThickness,
                color: Colors.Green.WithAlpha(ColorHelper.AnnotationAFull),
                showDates,
                projectStart,
                dateTimeCalculator,
                plotModel,
                trackingSeriesSet.Progress);

            AddScatterPlot(
                title: Resource.ProjectPlan.Labels.Label_Effort,
                stroke: mainStrokeThickness,
                color: Colors.Red.WithAlpha(ColorHelper.AnnotationAFull),
                showDates,
                projectStart,
                dateTimeCalculator,
                plotModel,
                trackingSeriesSet.Effort);

            if (showProjections)
            {
                AddScatterPlot(
                    title: Resource.ProjectPlan.Labels.Label_PlanProjection,
                    stroke: projectionStrokeThickness,
                    color: Colors.Blue.WithAlpha(ColorHelper.AnnotationAMedium),
                    showDates,
                    projectStart,
                    dateTimeCalculator,
                    plotModel,
                    trackingSeriesSet.PlanProjection);

                AddScatterPlot(
                    title: Resource.ProjectPlan.Labels.Label_ProgressProjection,
                    stroke: projectionStrokeThickness,
                    color: Colors.Green.WithAlpha(ColorHelper.AnnotationAMedium),
                    showDates,
                    projectStart,
                    dateTimeCalculator,
                    plotModel,
                    trackingSeriesSet.ProgressProjection);

                AddScatterPlot(
                    title: Resource.ProjectPlan.Labels.Label_EffortProjection,
                    stroke: projectionStrokeThickness,
                    color: Colors.Red.WithAlpha(ColorHelper.AnnotationAMedium),
                    showDates,
                    projectStart,
                    dateTimeCalculator,
                    plotModel,
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

                    double peakPercentage = trackingSeriesSet.Plan
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
            IList<TrackingPointModel> pointSeries)
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
            }
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
                            EarnedValueChartPlotModel.Plot.Save(
                                filename, width, height, ImageFormats.FromFilename(filename), 100);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImagePngFileExtension}", _ =>
                        {
                            EarnedValueChartPlotModel.Plot.Save(
                                filename, width, height, ImageFormats.FromFilename(filename), 100);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImageBmpFileExtension}", _ =>
                        {
                            EarnedValueChartPlotModel.Plot.Save(
                                filename, width, height, ImageFormats.FromFilename(filename), 100);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImageWebpFileExtension}", _ =>
                        {
                            EarnedValueChartPlotModel.Plot.Save(
                                filename, width, height, ImageFormats.FromFilename(filename), 100);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImageSvgFileExtension}", _ =>
                        {
                            EarnedValueChartPlotModel.Plot.Save(
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

        public void BuildEarnedValueChartPlotModel()
        {
            AvaPlot? plotModel = null;

            lock (m_Lock)
            {
                if (!HasCompilationErrors)
                {
                    plotModel = BuildEarnedValueChartPlotModelInternal(
                        m_DateTimeCalculator,
                        m_CoreViewModel.TrackingSeriesSet,
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

            // Clear existing menu items.
            plotModel.Menu?.Clear();

            // Add menu items with custom actions.
            plotModel.Menu?.Add(Resource.ProjectPlan.Menus.Menu_SaveAs, (plot) =>
            {
                SaveEarnedValueChartImageFileCommand.Execute(null);
            });
            plotModel.Menu?.Add(Resource.ProjectPlan.Menus.Menu_Reset, (plot) =>
            {
                plot.Axes.AutoScale();
            });

            //plotModel.Plot.Axes.AutoScale();
            EarnedValueChartPlotModel = plotModel;
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
                m_ShowMilestones?.Dispose();
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
