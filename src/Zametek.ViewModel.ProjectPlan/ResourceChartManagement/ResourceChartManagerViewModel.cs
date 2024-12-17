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
                    rcm => rcm.m_CoreViewModel.ProjectStartDateTime,
                    rcm => rcm.m_CoreViewModel.BaseTheme)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(async _ => await BuildResourceChartPlotModelAsync());

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

        private static PlotModel BuildResourceChartPlotModelInternal(
            IDateTimeCalculator dateTimeCalculator,
            ResourceSeriesSetModel resourceSeriesSet,
            bool showDates,
            DateTime projectStartDateTime,
            BaseTheme baseTheme)
        {
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            ArgumentNullException.ThrowIfNull(resourceSeriesSet);
            var plotModel = new PlotModel();

            if (resourceSeriesSet.Combined.Count == 0)
            {
                return plotModel.SetBaseTheme(baseTheme);
            }

            IEnumerable<ResourceSeriesModel> combinedResourceSeries = resourceSeriesSet.Combined.OrderBy(x => x.DisplayOrder);
            int finishTime = resourceSeriesSet.ResourceSchedules.Select(x => x.FinishTime).DefaultIfEmpty().Max();

            plotModel.Axes.Add(BuildResourceChartXAxis(dateTimeCalculator, finishTime, showDates, projectStartDateTime));
            plotModel.Axes.Add(BuildResourceChartYAxis());

            var legend = new Legend()
            {
                LegendBorder = OxyColors.Black,
                LegendBackground = OxyColors.Transparent,
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
                IList<int> total = [];

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

                        if (series.ResourceSchedule.ActivityAllocation.Count != 0)
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

            if (plotModel is IPlotModel plotModelInterface)
            {
                plotModelInterface.Update(true);
            }

            return plotModel.SetBaseTheme(baseTheme);
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

        private static LinearAxis BuildResourceChartYAxis()
        {
            return new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = Resource.ProjectPlan.Labels.Label_ResourcesAxisTitle,
                MinorStep = 1
            };
        }

        private async Task SaveResourceChartImageFileAsync()
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
                            using var stream = File.OpenWrite(filename);
                            OxyPlot.SkiaSharp.JpegExporter.Export(
                                ResourceChartPlotModel,
                                stream,
                                width,
                                height,
                                100);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImagePngFileExtension}", _ =>
                        {
                            using var stream = File.OpenWrite(filename);
                            OxyPlot.SkiaSharp.PngExporter.Export(
                                ResourceChartPlotModel,
                                stream,
                                width,
                                height);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_PdfFileExtension}", _ =>
                        {
                            using var stream = File.OpenWrite(filename);
                            OxyPlot.SkiaSharp.PdfExporter.Export(
                                ResourceChartPlotModel,
                                stream,
                                width,
                                height);
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

        public void BuildResourceChartPlotModel()
        {
            PlotModel? plotModel = null;

            lock (m_Lock)
            {
                plotModel = BuildResourceChartPlotModelInternal(
                    m_DateTimeCalculator,
                    m_CoreViewModel.ResourceSeriesSet,
                    m_CoreViewModel.ShowDates,
                    m_CoreViewModel.ProjectStartDateTime,
                    m_CoreViewModel.BaseTheme);
            }

            ResourceChartPlotModel = plotModel ?? new PlotModel();
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
