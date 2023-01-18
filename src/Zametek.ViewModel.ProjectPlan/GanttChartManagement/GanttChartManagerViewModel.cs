using Avalonia;
using Avalonia.Media;
using OxyPlot;
using OxyPlot.Avalonia;
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
                    Extensions = new List<string>
                    {
                        Resource.ProjectPlan.Filters.Filter_ImageJpegFileExtension
                    }
                },
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_ImagePngFileType,
                    Extensions = new List<string>
                    {
                        Resource.ProjectPlan.Filters.Filter_ImagePngFileExtension
                    }
                },
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_PdfFileType,
                    Extensions = new List<string>
                    {
                        Resource.ProjectPlan.Filters.Filter_PdfFileExtension
                    }
                }
            };

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly ISettingService m_SettingService;
        private readonly IDialogService m_DialogService;
        private readonly IDateTimeCalculator m_DateTimeCalculator;

        private readonly IDisposable? m_BuildGanttChartPlotModelSub;

        #endregion

        #region Ctors

        public GanttChartManagerViewModel(
            ICoreViewModel coreViewModel,//!!,
            ISettingService settingService,//!!,
            IDialogService dialogService,//!!,
            IDateTimeCalculator dateTimeCalculator)//!!)
        {
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
                        result.Item5);
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
            IDateTimeCalculator dateTimeCalculator,//!!,
            IGraphCompilation<int, int, IDependentActivity<int, int>> graphCompilation,//!!,
            ResourceSeriesSetModel resourceSeriesSet,//!!,
            ArrowGraphSettingsModel arrowGraphSettings,//!!
            bool showDates,
            DateTime projectStartDateTime)
        {
            var plotModel = new PlotModel();

            IEnumerable<ResourceSeriesModel> scheduledResourceSeries = resourceSeriesSet.Scheduled.OrderBy(x => x.DisplayOrder);

            int finishTime = resourceSeriesSet.ResourceSchedules.Select(x => x.FinishTime).DefaultIfEmpty().Max();

            plotModel.Axes.Add(BuildResourceChartXAxis(dateTimeCalculator, finishTime, showDates, projectStartDateTime));


            IDictionary<int, IDependentActivity<int, int>> activityLookup = graphCompilation.DependentActivities.ToDictionary(x => x.Id);






            var legend = new OxyPlot.Legends.Legend
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
            plotModel.IsLegendVisible = false;





            var colorFormatLookup = new SlackColorFormatLookup(arrowGraphSettings.ActivitySeverities);




            var series = new IntervalBarSeries
            {
                Title = "IntervalBarSeries 1",
                LabelFormatString = @"",
                TrackerFormatString = "{1}: {2}\n{3}: {4}",
            };
            var labels = new List<string>();





            foreach (ResourceSeriesModel resourceSeries in scheduledResourceSeries)
            {
                if (resourceSeries != null)
                {
                    ResourceScheduleModel resourceSchedule = resourceSeries.ResourceSchedule;

                    if (resourceSchedule != null)
                    {
                        //GanttRowGroup rowGroup = GanttChartAreaCtrl.CreateGanttRowGroup(resourceSeries.Title);

                        foreach (ScheduledActivityModel scheduledActivity in resourceSchedule.ScheduledActivities.OrderByDescending(x => x.StartTime))
                        {
                            if (activityLookup.TryGetValue(scheduledActivity.Id, out IDependentActivity<int, int>? activity))
                            {
                                Color slackColor = colorFormatLookup.FindSlackColor(activity.TotalSlack);// ?? Colors.DodgerBlue;


                                string label = string.IsNullOrWhiteSpace(scheduledActivity.Name) ? scheduledActivity.Id.ToString(CultureInfo.InvariantCulture) : scheduledActivity.Name;
                                //GanttRow row = GanttChartAreaCtrl.CreateGanttRow(rowGroup, name);

                                if (activity.EarliestStartTime.HasValue
                                    && activity.EarliestFinishTime.HasValue)
                                {

                                    var backgroundColor = OxyColor.FromArgb(
                                          slackColor.A,
                                          slackColor.R,
                                          slackColor.G,
                                          slackColor.B);

                                    var item = new IntervalBarItem
                                    {
                                        Title = label,
                                        Start = ChartHelper.CalculateChartTimeXValue(scheduledActivity.StartTime, showDates, projectStartDateTime, dateTimeCalculator),
                                        End = ChartHelper.CalculateChartTimeXValue(scheduledActivity.FinishTime, showDates, projectStartDateTime, dateTimeCalculator),
                                        Color = backgroundColor,
                                    };

                                    series.Items.Add(item);
                                    labels.Add(label);
                                }
                            }
                        }
                    }
                }
            }




            plotModel.Annotations.Add(new OxyPlot.Annotations.RectangleAnnotation { MinimumX = 20, MaximumX = 70, MinimumY = 10, MaximumY = 40, TextRotation = 10, Text = "RectangleAnnotation", ToolTip = "This is a tooltip for the RectangleAnnotation", Fill = OxyColor.FromAColor(10, OxyColors.Blue), Stroke = OxyColors.Black, StrokeThickness = 2 });



            plotModel.Axes.Add(BuildResourceChartYAxis(labels));

            plotModel.Series.Add(series);
























            //if (combinedResourceSeries.Any())
            //{
            //    IList<int> total = new List<int>();

            //    foreach (ResourceSeriesModel series in combinedResourceSeries)
            //    {
            //        if (series != null)
            //        {
            //            var color = OxyColor.FromArgb(
            //                series.ColorFormat.A,
            //                series.ColorFormat.R,
            //                series.ColorFormat.G,
            //                series.ColorFormat.B);

            //            var areaSeries = new AreaSeries
            //            {
            //                //Smooth = false,
            //                StrokeThickness = 0.0,
            //                Title = series.Title,
            //                Fill = color,
            //                Color = color
            //            };

            //            if (series.ResourceSchedule.ActivityAllocation.Any())
            //            {
            //                // Mark the start of the plot.
            //                areaSeries.Points.Add(new DataPoint(0.0, 0.0));
            //                areaSeries.Points2.Add(new DataPoint(0.0, 0.0));

            //                for (int i = 0; i < series.ResourceSchedule.ActivityAllocation.Count; i++)
            //                {
            //                    bool j = series.ResourceSchedule.ActivityAllocation[i];
            //                    if (i >= total.Count)
            //                    {
            //                        total.Add(0);
            //                    }
            //                    int dayNumber = i + 1;
            //                    areaSeries.Points.Add(
            //                        new DataPoint(ChartHelper.CalculateChartTimeXValue(dayNumber, showDates, projectStartDateTime, dateTimeCalculator),
            //                        total[i]));
            //                    total[i] += j ? 1 : 0;
            //                    areaSeries.Points2.Add(
            //                        new DataPoint(ChartHelper.CalculateChartTimeXValue(dayNumber, showDates, projectStartDateTime, dateTimeCalculator),
            //                        total[i]));
            //                }
            //            }

            //            plotModel.Series.Add(areaSeries);
            //        }
            //    }
            //}














            return plotModel;
        }

        private static OxyPlot.Axes.Axis BuildResourceChartXAxis(
            IDateTimeCalculator dateTimeCalculator,//!!,
            int finishTime,
            bool showDates,
            DateTime projectStartDateTime)
        {
            if (finishTime != default)
            {
                double minValue = ChartHelper.CalculateChartTimeXValue(-1, showDates, projectStartDateTime, dateTimeCalculator);
                double maxValue = ChartHelper.CalculateChartTimeXValue(finishTime + 1, showDates, projectStartDateTime, dateTimeCalculator);

                if (showDates)
                {
                    return new OxyPlot.Axes.DateTimeAxis
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

                return new OxyPlot.Axes.LinearAxis
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
            return new OxyPlot.Axes.LinearAxis
            {
                Position = AxisPosition.Bottom,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                MaximumPadding = 0.1,
                MinimumPadding = 0.1,
                Title = Resource.ProjectPlan.Labels.Label_TimeAxisTitle
            };
        }

        private static OxyPlot.Axes.Axis BuildResourceChartYAxis(IEnumerable<string> labels)//!!)
        {
            var categoryAxis = new OxyPlot.Axes.CategoryAxis
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
                    //byte[]? data = null;

                    fileExtension.ValueSwitchOn()
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImageJpegFileExtension}", _ =>
                        {
                            using var stream = File.OpenWrite(filename);
                            OxyPlot.SkiaSharp.JpegExporter.Export(
                                GanttChartPlotModel,
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
                                GanttChartPlotModel,
                                stream,
                                Convert.ToInt32(bounds.Width),
                                Convert.ToInt32(bounds.Height),
                                OxyColors.White);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_PdfFileExtension}", _ =>
                        {
                            using var stream = File.OpenWrite(filename);
                            OxyPlot.SkiaSharp.PdfExporter.Export(
                                GanttChartPlotModel,
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
