using Avalonia;
using OxyPlot;
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

            m_BuildResourceChartPlotModelSub = this
                .WhenAnyValue(
                    rcm => rcm.m_CoreViewModel.ResourceSeriesSet,
                    rcm => rcm.m_CoreViewModel.ShowDates,
                    rcm => rcm.m_CoreViewModel.ProjectStartDateTime)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(async result =>
                {
                    ResourceChartPlotModel = await BuildResourceChartPlotModelAsync(
                        m_DateTimeCalculator,
                        result.Item1,
                        result.Item2,
                        result.Item3);
                });

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

        private async Task<PlotModel> BuildResourceChartPlotModelAsync(
            IDateTimeCalculator dateTimeCalculator,
            ResourceSeriesSetModel resourceSeriesSet,
            bool showDates,
            DateTime projectStartDateTime)
        {
            try
            {
                lock (m_Lock)
                {
                    return BuildResourceChartPlotModel(
                        dateTimeCalculator,
                        resourceSeriesSet,
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

        private static PlotModel BuildResourceChartPlotModel(
            IDateTimeCalculator dateTimeCalculator,
            ResourceSeriesSetModel resourceSeriesSet,
            bool showDates,
            DateTime projectStartDateTime)
        {
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            ArgumentNullException.ThrowIfNull(resourceSeriesSet);
            var plotModel = new PlotModel();
            IEnumerable<ResourceSeriesModel> combinedResourceSeries = resourceSeriesSet.Combined.OrderBy(x => x.DisplayOrder);
            int finishTime = resourceSeriesSet.ResourceSchedules.Select(x => x.FinishTime).DefaultIfEmpty().Max();

            plotModel.Axes.Add(BuildResourceChartXAxis(dateTimeCalculator, finishTime, showDates, projectStartDateTime));
            plotModel.Axes.Add(BuildResourceChartYAxis());

            var legend = new Legend()
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

            if (combinedResourceSeries.Any())
            {
                IList<int> total = new List<int>();

                foreach (ResourceSeriesModel series in combinedResourceSeries)
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
                            //Smooth = false,
                            StrokeThickness = 0.0,
                            Title = series.Title,
                            Fill = color,
                            Color = color
                        };

                        if (series.ResourceSchedule.ActivityAllocation.Any())
                        {
                            // Mark the start of the plot.
                            areaSeries.Points.Add(new DataPoint(0.0, 0.0));
                            areaSeries.Points2.Add(new DataPoint(0.0, 0.0));

                            for (int i = 0; i < series.ResourceSchedule.ActivityAllocation.Count; i++)
                            {
                                bool j = series.ResourceSchedule.ActivityAllocation[i];
                                if (i >= total.Count)
                                {
                                    total.Add(0);
                                }
                                int dayNumber = i + 1;
                                areaSeries.Points.Add(
                                    new DataPoint(ChartHelper.CalculateChartTimeXValue(dayNumber, showDates, projectStartDateTime, dateTimeCalculator),
                                    total[i]));
                                total[i] += j ? 1 : 0;
                                areaSeries.Points2.Add(
                                    new DataPoint(ChartHelper.CalculateChartTimeXValue(dayNumber, showDates, projectStartDateTime, dateTimeCalculator),
                                    total[i]));
                            }
                        }

                        plotModel.Series.Add(areaSeries);
                    }
                }
            }

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

        private static Axis BuildResourceChartYAxis()
        {
            return new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = Resource.ProjectPlan.Labels.Label_ResourcesAxisTitle
            };
        }

        private async Task SaveResourceChartImageFileInternalAsync(string? filename)
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
                                ResourceChartPlotModel,
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
                                ResourceChartPlotModel,
                                stream,
                                Convert.ToInt32(bounds.Width),
                                Convert.ToInt32(bounds.Height),
                                OxyColors.White);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_PdfFileExtension}", _ =>
                        {
                            using var stream = File.OpenWrite(filename);
                            OxyPlot.SkiaSharp.PdfExporter.Export(
                                ResourceChartPlotModel,
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

        private async Task SaveResourceChartImageFileAsync()
        {
            try
            {
                string projectTitle = m_SettingService.ProjectTitle;
                string directory = m_SettingService.ProjectDirectory;
                string? filename = await m_DialogService.ShowSaveFileDialogAsync(projectTitle, directory, s_ExportFileFilters);

                if (!string.IsNullOrWhiteSpace(filename))
                {
                    await SaveResourceChartImageFileInternalAsync(filename);
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

        #region IResourceChartManagerViewModel Members

        private readonly ObservableAsPropertyHelper<bool> m_IsBusy;
        public bool IsBusy => m_IsBusy.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasStaleOutputs;
        public bool HasStaleOutputs => m_HasStaleOutputs.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasCompilationErrors;
        public bool HasCompilationErrors => m_HasCompilationErrors.Value;

        public ICommand SaveResourceChartImageFileCommand { get; }

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
                m_BuildResourceChartPlotModelSub?.Dispose();
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
