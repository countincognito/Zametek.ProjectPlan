using Avalonia;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using ReactiveUI;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class EarnedValueChartManagerViewModel
        : ToolViewModelBase, IEarnedValueChartManagerViewModel, IDisposable
    {
        #region Fields

        private readonly object m_Lock;

        private static readonly IList<IFileFilter> s_ImageFileFilters =
            new List<IFileFilter>
            {
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_ImagePngFileType,
                    Extensions = new List<string>
                    {
                        Resource.ProjectPlan.Filters.Filter_ImagePngFileExtension
                    }
                },
            };

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly ISettingService m_SettingService;
        private readonly IDialogService m_DialogService;
        private readonly IDateTimeCalculator m_DateTimeCalculator;

        private readonly IDisposable? m_BuildEarnedValueChartPlotModelSub;

        #endregion

        #region Ctors

        public EarnedValueChartManagerViewModel(
            ICoreViewModel coreViewModel!!,
            ISettingService settingService!!,
            IDialogService dialogService!!,
            IDateTimeCalculator dateTimeCalculator!!)
        {
            m_Lock = new object();
            m_CoreViewModel = coreViewModel;
            m_SettingService = settingService;
            m_DialogService = dialogService;
            m_DateTimeCalculator = dateTimeCalculator;
            m_EarnedValueChartPlotModel = new PlotModel();

            {
                ReactiveCommand<Unit, Unit> saveEarnedValueChartImageFileCommand = ReactiveCommand.CreateFromTask(SaveEarnedValueChartImageFileAsync);
                SaveEarnedValueChartImageFileCommand = saveEarnedValueChartImageFileCommand;
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

            m_BuildEarnedValueChartPlotModelSub = this
                .WhenAnyValue(
                    rcm => rcm.m_CoreViewModel.TrackingSeriesSet,
                    rcm => rcm.m_CoreViewModel.ShowDates,
                    rcm => rcm.m_CoreViewModel.ProjectStartDateTime)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(async result =>
                {
                    EarnedValueChartPlotModel = await BuildEarnedValueChartPlotModelAsync(
                        m_DateTimeCalculator,
                        result.Item1,
                        result.Item2,
                        result.Item3);
                });

            Id = Resource.ProjectPlan.Titles.Title_EarnedValueChartView;
            Title = Resource.ProjectPlan.Titles.Title_EarnedValueChartView;
        }

        #endregion

        #region Properties

        private PlotModel m_EarnedValueChartPlotModel;
        public PlotModel EarnedValueChartPlotModel
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

        private async Task<PlotModel> BuildEarnedValueChartPlotModelAsync(
            IDateTimeCalculator dateTimeCalculator,
            TrackingSeriesSetModel trackingSeriesSet,
            bool showDates,
            DateTime projectStartDateTime)
        {
            try
            {
                lock (m_Lock)
                {
                    return BuildEarnedValueChartPlotModel(
                        dateTimeCalculator,
                        trackingSeriesSet,
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

        private static PlotModel BuildEarnedValueChartPlotModel(
            IDateTimeCalculator dateTimeCalculator!!,
            TrackingSeriesSetModel trackingSeriesSet!!,
            bool showDates,
            DateTime projectStartDateTime)
        {
            var plotModel = new PlotModel();

            int finishTime = trackingSeriesSet.Plan
                .Concat(trackingSeriesSet.Progress)
                .Concat(trackingSeriesSet.Effort)
                .Select(x => x.Time).DefaultIfEmpty().Max();

            double maxPercentage = trackingSeriesSet.Plan
                .Concat(trackingSeriesSet.Progress)
                .Concat(trackingSeriesSet.Effort)
                .Select(x => x.ValuePercentage).DefaultIfEmpty(100.0).Max();

            plotModel.Axes.Add(BuildEarnedValueChartXAxis(dateTimeCalculator, finishTime, showDates, projectStartDateTime));
            plotModel.Axes.Add(BuildEarnedValueChartYAxis(maxPercentage));

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

            void PopulateLineSeries(
                LineSeries lineSeries!!,
                IList<TrackingPointModel> pointSeries!!)
            {
                if (pointSeries.Any())
                {
                    foreach (TrackingPointModel planPoint in pointSeries)
                    {
                        lineSeries.Points.Add(
                            new DataPoint(ChartHelper.CalculateChartTimeXValue(planPoint.Time, showDates, projectStartDateTime, dateTimeCalculator),
                            planPoint.ValuePercentage));
                    }
                    plotModel.Series.Add(lineSeries);
                }
            }

            PopulateLineSeries(
                new LineSeries
                {
                    Title = Resource.ProjectPlan.Labels.Label_Plan,
                    Color = OxyColors.Blue
                },
                trackingSeriesSet.Plan);

            PopulateLineSeries(
                new LineSeries
                {
                    Title = Resource.ProjectPlan.Labels.Label_Progress,
                    Color = OxyColors.Green
                },
                trackingSeriesSet.Progress);

            PopulateLineSeries(
                new LineSeries
                {
                    Title = Resource.ProjectPlan.Labels.Label_Effort,
                    Color = OxyColors.Red
                },
                trackingSeriesSet.Effort);

            return plotModel;
        }

        private static Axis BuildEarnedValueChartXAxis(
            IDateTimeCalculator dateTimeCalculator!!,
            int finishTime,
            bool showDates,
            DateTime projectStartDateTime)
        {
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

        private static Axis BuildEarnedValueChartYAxis(double maximum)
        {
            return new LinearAxis
            {
                Position = AxisPosition.Left,
                Minimum = 0.0,
                Maximum = maximum,
                Title = Resource.ProjectPlan.Labels.Label_PercentageAxisTitle
            };
        }

        private async Task SaveEarnedValueChartImageFileInternalAsync(string? filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    Resource.ProjectPlan.Messages.Message_EmptyFilename);
            }
            else
            {
                using var stream = File.OpenWrite(filename);

                if (ImageBounds is Rect bounds)
                {
                    OxyPlot.Avalonia.PngExporter.Export(
                        EarnedValueChartPlotModel,
                        stream,
                        Convert.ToInt32(bounds.Width),
                        Convert.ToInt32(bounds.Height),
                        OxyColors.White);
                }
            }
        }

        private async Task SaveEarnedValueChartImageFileAsync()
        {
            try
            {
                string projectTitle = m_SettingService.ProjectTitle;
                string directory = m_SettingService.ProjectDirectory;
                string? filename = await m_DialogService.ShowSaveFileDialogAsync(projectTitle, directory, s_ImageFileFilters);

                if (!string.IsNullOrWhiteSpace(filename))
                {
                    await SaveEarnedValueChartImageFileInternalAsync(filename);
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

        #region IEarnedValueChartManagerViewModel Members

        private readonly ObservableAsPropertyHelper<bool> m_IsBusy;
        public bool IsBusy => m_IsBusy.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasStaleOutputs;
        public bool HasStaleOutputs => m_HasStaleOutputs.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasCompilationErrors;
        public bool HasCompilationErrors => m_HasCompilationErrors.Value;

        public ICommand SaveEarnedValueChartImageFileCommand { get; }

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
                m_BuildEarnedValueChartPlotModelSub?.Dispose();
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
