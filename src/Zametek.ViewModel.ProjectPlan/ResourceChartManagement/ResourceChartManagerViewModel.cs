using Avalonia;
using ReactiveUI;
using ScottPlot;
using ScottPlot.Avalonia;
using ScottPlot.DataSources;
using ScottPlot.Plottables;
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

        private readonly IDisposable? m_BuildResourceChartPlotModelSub;

        private const float c_ScatterLineWidth = 5.0f;

        private const float c_VerticalLineWidth = 2.0f;

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
            m_ResourceChartPlotModel = new AvaPlot();

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
                .WhenAnyValue(rcm => rcm.m_CoreViewModel.DisplaySettingsViewModel.ResourceChartAllocationMode)
                .ToProperty(this, rcm => rcm.AllocationMode);

            m_ScheduleMode = this
                .WhenAnyValue(rcm => rcm.m_CoreViewModel.DisplaySettingsViewModel.ResourceChartScheduleMode)
                .ToProperty(this, rcm => rcm.ScheduleMode);

            m_DisplayStyle = this
                .WhenAnyValue(rcm => rcm.m_CoreViewModel.DisplaySettingsViewModel.ResourceChartDisplayStyle)
                .ToProperty(this, rcm => rcm.DisplayStyle);

            m_ShowToday = this
                .WhenAnyValue(rcm => rcm.m_CoreViewModel.DisplaySettingsViewModel.ResourceChartShowToday)
                .ToProperty(this, rcm => rcm.ShowToday);

            m_BuildResourceChartPlotModelSub = this
                .WhenAnyValue(
                    rcm => rcm.m_CoreViewModel.ResourceSeriesSet,
                    rcm => rcm.m_CoreViewModel.DisplaySettingsViewModel.ShowDates,
                    rcm => rcm.m_CoreViewModel.DisplaySettingsViewModel.UseClassicDates,
                    rcm => rcm.m_CoreViewModel.DisplaySettingsViewModel.UseBusinessDays,
                    rcm => rcm.m_CoreViewModel.ProjectStart,
                    rcm => rcm.m_CoreViewModel.Today,
                    rcm => rcm.AllocationMode,
                    rcm => rcm.ScheduleMode,
                    rcm => rcm.DisplayStyle,
                    rcm => rcm.ShowToday,
                    rcm => rcm.m_CoreViewModel.BaseTheme,
                    (a, b, c, d, e, f, g, h, i, j, k) => (a, b, c, d, e, f, g, h, i, j, k)) // Do this as a workaround because WhenAnyValue cannot handle this many individual inputs.
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(async _ => await BuildResourceChartPlotModelAsync());

            Id = Resource.ProjectPlan.Titles.Title_ResourceChartView;
            Title = Resource.ProjectPlan.Titles.Title_ResourceChartView;
        }

        #endregion

        #region Properties

        private AvaPlot m_ResourceChartPlotModel;
        public AvaPlot ResourceChartPlotModel
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

        private static AvaPlot BuildResourceChartPlotModelInternal(
            IDateTimeCalculator dateTimeCalculator,
            ResourceSeriesSetModel resourceSeriesSet,
            bool showDates,
            DateTimeOffset projectStart,
            DateTimeOffset today,
            AllocationMode allocationMode,
            ScheduleMode scheduleMode,
            DisplayStyle displayStyle,
            bool showToday,
            BaseTheme baseTheme)
        {
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            ArgumentNullException.ThrowIfNull(resourceSeriesSet);
            var plotModel = new AvaPlot();
            plotModel.Plot.HideGrid();

            // Select the type of allocation to be displayed.

            Func<ResourceScheduleModel, List<bool>>? allocationFunction = null;

            allocationFunction = allocationMode switch
            {
                AllocationMode.Activity => (ResourceScheduleModel model) => model.ActivityAllocation,
                AllocationMode.Cost => (ResourceScheduleModel model) => model.CostAllocation,
                AllocationMode.Billing => (ResourceScheduleModel model) => model.BillingAllocation,
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

            BuildResourceChartXAxis(plotModel, dateTimeCalculator, finishTime, showDates, projectStart);

            plotModel.Plot.Legend.OutlineWidth = 1;
            plotModel.Plot.Legend.BackgroundColor = Colors.Transparent;
            plotModel.Plot.Legend.ShadowColor = Colors.Transparent;
            plotModel.Plot.Legend.ShadowOffset = new(0, 0);

            plotModel.Plot.ShowLegend(Edge.Right);

            var scatters = new List<Scatter>();
            var labels = new List<string>();

            if (resourceSeries.Any())
            {
                IList<int> total1 = [];
                IList<int> total2 = [];

                foreach (ResourceSeriesModel series in resourceSeries)
                {
                    if (series != null)
                    {
                        var color = new Color(
                            series.ColorFormat.R,
                            series.ColorFormat.G,
                            series.ColorFormat.B,
                            series.ColorFormat.A);

                        IList<double> xs = [];
                        IList<double> ys = [];

                        switch (displayStyle)
                        {
                            case DisplayStyle.Slanted:
                                {
                                    if (allocationFunction(series.ResourceSchedule).Count != 0)
                                    {
                                        // Mark the start of the plot.
                                        xs.Add(ChartHelper.CalculateChartStartTimeXValue(0, showDates, projectStart, dateTimeCalculator));
                                        ys.Add(0.0);

                                        for (int i = 0; i < allocationFunction(series.ResourceSchedule).Count; i++)
                                        {
                                            bool allocationExists = allocationFunction(series.ResourceSchedule)[i];

                                            if (i >= total1.Count)
                                            {
                                                total1.Add(0);
                                            }

                                            int dayNumber = i + 1;

                                            xs.Add(ChartHelper.CalculateChartStartTimeXValue(dayNumber, showDates, projectStart, dateTimeCalculator));
                                            total1[i] += allocationExists ? 1 : 0;
                                            ys.Add(total1[i]);
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

                                            xs.Add(ChartHelper.CalculateChartStartTimeXValue(dayNumber, showDates, projectStart, dateTimeCalculator));
                                            total1[dayNumber] += allocationExists ? 1 : 0;
                                            ys.Add(total1[dayNumber]);

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

                                            xs.Add(ChartHelper.CalculateChartStartTimeXValue(dayNumber, showDates, projectStart, dateTimeCalculator));
                                            total2[dayNumber] += allocationExists ? 1 : 0;
                                            ys.Add(total2[dayNumber]);
                                        }
                                    }
                                }
                                break;
                            default:
                                throw new ArgumentOutOfRangeException(nameof(displayStyle), @$"{Resource.ProjectPlan.Messages.Message_UnknownDisplayStyle} {displayStyle}");
                        }

                        ScatterSourceDoubleArray source = new([.. xs], [.. ys]);
                        Scatter scatter = new(source)
                        {
                            LegendText = series.Title,
                            LineColor = color,
                            LineWidth = c_ScatterLineWidth,
                            MarkerFillColor = color,
                            MarkerLineColor = color,
                            MarkerSize = 0,
                            FillY = true,
                            FillYColor = color,
                        };

                        scatters.Add(scatter);
                        labels.Add(series.Title);
                    }
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
            }

            scatters.Reverse();
            plotModel.Plot.PlottableList.AddRange(scatters);

            // Style the plot so the bars start on the left edge.
            plotModel.Plot.Axes.Margins(left: 0, right: 0, bottom: 0, top: 0);

            IYAxis yAxis = BuildResourceChartYAxis(plotModel);

            yAxis.SetTicks(
                [.. Enumerable.Range(0, labels.Count).Select(Convert.ToDouble)],
                [.. Enumerable.Range(0, labels.Count).Select(x => Convert.ToString(x))]);

            plotModel.Plot.Axes.AutoScale();

            return plotModel.SetBaseTheme(baseTheme);
        }

        private static IXAxis BuildResourceChartXAxis(
            AvaPlot plotModel,
            IDateTimeCalculator dateTimeCalculator,
            int finishTime,
            bool showDates,
            DateTimeOffset projectStart)
        {
            ArgumentNullException.ThrowIfNull(plotModel);
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);

            IXAxis xAxis = plotModel.Plot.Axes.Bottom;

            if (finishTime != default)
            {
                double minValue = ChartHelper.CalculateChartStartTimeXValue(0, showDates, projectStart, dateTimeCalculator);
                double maxValue = ChartHelper.CalculateChartFinishTimeXValue(finishTime, showDates, projectStart, dateTimeCalculator);

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

        private static IYAxis BuildResourceChartYAxis(AvaPlot plotModel)
        {
            ArgumentNullException.ThrowIfNull(plotModel);
            IYAxis yAxis = plotModel.Plot.Axes.Left;
            yAxis.Min = 0.0;
            yAxis.Label.Text = Resource.ProjectPlan.Labels.Label_ResourcesAxisTitle;
            yAxis.Label.FontSize = PlotHelper.FontSize;
            yAxis.Label.Bold = false;
            return yAxis;
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
                lock (m_Lock) m_CoreViewModel.DisplaySettingsViewModel.ResourceChartAllocationMode = value;
            }
        }

        private readonly ObservableAsPropertyHelper<ScheduleMode> m_ScheduleMode;
        public ScheduleMode ScheduleMode
        {
            get => m_ScheduleMode.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.DisplaySettingsViewModel.ResourceChartScheduleMode = value;
            }
        }

        private readonly ObservableAsPropertyHelper<DisplayStyle> m_DisplayStyle;
        public DisplayStyle DisplayStyle
        {
            get => m_DisplayStyle.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.DisplaySettingsViewModel.ResourceChartDisplayStyle = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_ShowToday;
        public bool ShowToday
        {
            get => m_ShowToday.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.DisplaySettingsViewModel.ResourceChartShowToday = value;
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
                            ResourceChartPlotModel.Plot.Save(
                                filename, width, height, ImageFormats.FromFilename(filename), 100);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImagePngFileExtension}", _ =>
                        {
                            ResourceChartPlotModel.Plot.Save(
                                filename, width, height, ImageFormats.FromFilename(filename), 100);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImageBmpFileExtension}", _ =>
                        {
                            ResourceChartPlotModel.Plot.Save(
                                filename, width, height, ImageFormats.FromFilename(filename), 100);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImageWebpFileExtension}", _ =>
                        {
                            ResourceChartPlotModel.Plot.Save(
                                filename, width, height, ImageFormats.FromFilename(filename), 100);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImageSvgFileExtension}", _ =>
                        {
                            ResourceChartPlotModel.Plot.Save(
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

        public void BuildResourceChartPlotModel()
        {
            AvaPlot? plotModel = null;

            lock (m_Lock)
            {
                if (!HasCompilationErrors)
                {
                    plotModel = BuildResourceChartPlotModelInternal(
                        m_DateTimeCalculator,
                        m_CoreViewModel.ResourceSeriesSet,
                        m_CoreViewModel.DisplaySettingsViewModel.ShowDates,
                        m_CoreViewModel.ProjectStart,
                        m_CoreViewModel.Today,
                        AllocationMode,
                        ScheduleMode,
                        DisplayStyle,
                        ShowToday,
                        m_CoreViewModel.BaseTheme);
                }
            }

            plotModel ??= new AvaPlot();

            // Clear existing menu items.
            plotModel.Menu?.Clear();

            // Add menu items with custom actions.
            plotModel.Menu?.Add(Resource.ProjectPlan.Menus.Menu_SaveAs, (plot) =>
            {
                SaveResourceChartImageFileCommand.Execute(null);
            });
            plotModel.Menu?.Add(Resource.ProjectPlan.Menus.Menu_Reset, (plot) =>
            {
                plot.Axes.AutoScale();
            });

            //plotModel.Plot.Axes.AutoScale();
            ResourceChartPlotModel = plotModel;
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
