using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using Prism;
using Prism.Commands;
using Prism.Events;
using Prism.Interactivity.InteractionRequest;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Event.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ResourceChartManagerViewModel
        : PropertyChangedPubSubViewModel, IResourceChartManagerViewModel, IActiveAware
    {
        #region Fields

        private readonly object m_Lock;

        private bool m_ExportResourceChartAsCosts;
        private PlotModel m_ResourceChartPlotModel;
        private int m_ResourceChartOutputWidth;
        private int m_ResourceChartOutputHeight;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IFileDialogService m_FileDialogService;
        private readonly ISettingService m_SettingService;
        private readonly IDateTimeCalculator m_DateTimeCalculator;
        private readonly IEventAggregator m_EventService;

        private readonly InteractionRequest<Notification> m_NotificationInteractionRequest;

        private SubscriptionToken m_GraphCompilationUpdatedSubscriptionToken;

        private bool m_IsActive;

        #endregion

        #region Ctors

        public ResourceChartManagerViewModel(
            ICoreViewModel coreViewModel,
            IFileDialogService fileDialogService,
            ISettingService settingService,
            IDateTimeCalculator dateTimeCalculator,
            IEventAggregator eventService)
            : base(eventService)
        {
            m_Lock = new object();
            m_CoreViewModel = coreViewModel ?? throw new ArgumentNullException(nameof(coreViewModel));
            m_FileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
            m_SettingService = settingService ?? throw new ArgumentNullException(nameof(settingService));
            m_DateTimeCalculator = dateTimeCalculator ?? throw new ArgumentNullException(nameof(dateTimeCalculator));
            m_EventService = eventService ?? throw new ArgumentNullException(nameof(eventService));

            m_NotificationInteractionRequest = new InteractionRequest<Notification>();

            ResourceChartPlotModel = null;
            ResourceChartOutputWidth = 1000;
            ResourceChartOutputHeight = 500;

            InitializeCommands();
            SubscribeToEvents();

            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.IsBusy), nameof(IsBusy), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.HasStaleOutputs), nameof(HasStaleOutputs), ThreadOption.BackgroundThread);
        }

        #endregion

        #region Properties

        private DateTime ProjectStart => m_CoreViewModel.ProjectStart;

        private bool ShowDates => m_CoreViewModel.ShowDates;

        private bool UseBusinessDays => m_CoreViewModel.UseBusinessDays;

        private bool HasCompilationErrors => m_CoreViewModel.HasCompilationErrors;

        private IGraphCompilation<int, int, IDependentActivity<int, int>> GraphCompilation => m_CoreViewModel.GraphCompilation;

        private ResourceSeriesSetModel ResourceSeriesSet => m_CoreViewModel.ResourceSeriesSet;

        #endregion

        #region Commands

        public DelegateCommandBase InternalCopyResourceChartToClipboardCommand
        {
            get;
            private set;
        }

        private void CopyResourceChartToClipboard()
        {
            lock (m_Lock)
            {
                if (CanCopyResourceChartToClipboard())
                {
                    var pngExporter = new OxyPlot.Wpf.PngExporter
                    {
                        Width = ResourceChartOutputWidth,
                        Height = ResourceChartOutputHeight,
                        Background = OxyColors.White
                    };
                    BitmapSource bitmap = pngExporter.ExportToBitmap(ResourceChartPlotModel);
                    System.Windows.Clipboard.SetImage(bitmap);
                }
            }
        }

        private bool CanCopyResourceChartToClipboard()
        {
            lock (m_Lock)
            {
                return ResourceChartPlotModel != null;
            }
        }

        public DelegateCommandBase InternalExportResourceChartToCsvCommand
        {
            get;
            private set;
        }

        private async void ExportResourceChartToCsv()
        {
            await DoExportResourceChartToCsvAsync().ConfigureAwait(true);
        }

        private bool CanExportResourceChartToCsv()
        {
            lock (m_Lock)
            {
                return ResourceSeriesSet != null;
            }
        }

        #endregion

        #region Public Methods

        public async Task DoExportResourceChartToCsvAsync()
        {
            try
            {
                IsBusy = true;
                string directory = m_SettingService.PlanDirectory;

                var filter = new FileDialogFileTypeFilter(
                    Resource.ProjectPlan.Filters.SaveCsvFileType,
                    Resource.ProjectPlan.Filters.SaveCsvFileExtension
                    );

                bool result = m_FileDialogService.ShowSaveDialog(directory, filter);

                if (result)
                {
                    string filename = m_FileDialogService.Filename;
                    if (string.IsNullOrWhiteSpace(filename))
                    {
                        DispatchNotification(
                            Resource.ProjectPlan.Resources.Title_Error,
                            Resource.ProjectPlan.Resources.Message_EmptyFilename);
                    }
                    else
                    {
                        DataTable dataTable = await BuildResourceChartDataTableAsync().ConfigureAwait(true);
                        await ChartHelper.ExportDataTableToCsvAsync(dataTable, filename).ConfigureAwait(true);
                        m_SettingService.SetDirectory(filename);
                    }
                }
            }
            catch (Exception ex)
            {
                DispatchNotification(
                    Resource.ProjectPlan.Resources.Title_Error,
                    ex.Message);
            }
            finally
            {
                IsBusy = false;
                RaiseCanExecuteChangedAllCommands();
            }
        }

        #endregion

        #region Private Methods

        private void InitializeCommands()
        {
            CopyResourceChartToClipboardCommand =
                InternalCopyResourceChartToClipboardCommand =
                    new DelegateCommand(CopyResourceChartToClipboard, CanCopyResourceChartToClipboard);
            ExportResourceChartToCsvCommand =
                InternalExportResourceChartToCsvCommand =
                    new DelegateCommand(ExportResourceChartToCsv, CanExportResourceChartToCsv);
        }

        private void RaiseCanExecuteChangedAllCommands()
        {
            InternalCopyResourceChartToClipboardCommand.RaiseCanExecuteChanged();
            InternalExportResourceChartToCsvCommand.RaiseCanExecuteChanged();
        }

        private void SubscribeToEvents()
        {
            m_GraphCompilationUpdatedSubscriptionToken =
                m_EventService.GetEvent<PubSubEvent<GraphCompilationUpdatedPayload>>()
                    .Subscribe(payload =>
                    {
                        IsBusy = true;
                        CalculateResourceChartPlotModel();
                        IsBusy = false;
                    }, ThreadOption.BackgroundThread);
        }

        private void UnsubscribeFromEvents()
        {
            m_EventService.GetEvent<PubSubEvent<GraphCompilationUpdatedPayload>>()
                .Unsubscribe(m_GraphCompilationUpdatedSubscriptionToken);
        }

        private void CalculateResourceChartPlotModel()
        {
            lock (m_Lock)
            {
                ResourceSeriesSetModel resourceSeriesSet = ResourceSeriesSet;
                PlotModel plotModel = null;

                if (resourceSeriesSet != null)
                {
                    IEnumerable<ResourceSeriesModel> combinedResourceSeries = resourceSeriesSet.Combined.OrderBy(x => x.DisplayOrder);

                    if (combinedResourceSeries.Any())
                    {
                        plotModel = new PlotModel();
                        plotModel.Axes.Add(BuildResourceChartXAxis());
                        plotModel.Axes.Add(BuildResourceChartYAxis());
                        plotModel.LegendPlacement = LegendPlacement.Outside;
                        plotModel.LegendPosition = LegendPosition.RightMiddle;

                        var total = new List<int>();
                        m_DateTimeCalculator.UseBusinessDays(UseBusinessDays);

                        foreach (ResourceSeriesModel series in combinedResourceSeries)
                        {
                            if (series != null)
                            {
                                var areaSeries = new AreaSeries
                                {
                                    //Smooth = false,
                                    StrokeThickness = 0.0,
                                    Title = series.Title,
                                    Color = OxyColor.FromArgb(
                                        series.ColorFormat.A,
                                        series.ColorFormat.R,
                                        series.ColorFormat.G,
                                        series.ColorFormat.B)
                                };

                                if (series.Values.Any())
                                {
                                    // Mark the start of the plot.
                                    areaSeries.Points.Add(new DataPoint(0.0, 0.0));
                                    areaSeries.Points2.Add(new DataPoint(0.0, 0.0));

                                    for (int i = 0; i < series.Values.Count; i++)
                                    {
                                        int j = series.Values[i];
                                        if (i >= total.Count)
                                        {
                                            total.Add(0);
                                        }
                                        int dayNumber = i + 1;
                                        areaSeries.Points.Add(
                                            new DataPoint(ChartHelper.CalculateChartTimeXValue(dayNumber, ShowDates, ProjectStart, m_DateTimeCalculator),
                                            total[i]));
                                        total[i] += j;
                                        areaSeries.Points2.Add(
                                            new DataPoint(ChartHelper.CalculateChartTimeXValue(dayNumber, ShowDates, ProjectStart, m_DateTimeCalculator),
                                            total[i]));
                                    }
                                }    

                                plotModel.Series.Add(areaSeries);
                            }
                        }
                    }
                }
                ResourceChartPlotModel = plotModel;
            }
            RaiseCanExecuteChangedAllCommands();
        }

        private Axis BuildResourceChartXAxis()
        {
            lock (m_Lock)
            {
                IEnumerable<IResourceSchedule<int, int>> resourceSchedules = GraphCompilation?.ResourceSchedules;
                Axis axis = null;
                if (resourceSchedules != null
                    && resourceSchedules.Any())
                {
                    int finishTime = resourceSchedules.Max(x => x.FinishTime);
                    m_DateTimeCalculator.UseBusinessDays(UseBusinessDays);
                    double minValue = ChartHelper.CalculateChartTimeXValue(0, ShowDates, ProjectStart, m_DateTimeCalculator);
                    double maxValue = ChartHelper.CalculateChartTimeXValue(finishTime, ShowDates, ProjectStart, m_DateTimeCalculator);

                    if (ShowDates)
                    {
                        axis = new DateTimeAxis
                        {
                            Position = AxisPosition.Bottom,
                            Minimum = minValue,
                            Maximum = maxValue,
                            Title = Resource.ProjectPlan.Resources.Label_TimeAxisTitle,
                            StringFormat = "d"
                        };
                    }
                    else
                    {
                        axis = new LinearAxis
                        {
                            Position = AxisPosition.Bottom,
                            Minimum = minValue,
                            Maximum = maxValue,
                            Title = Resource.ProjectPlan.Resources.Label_TimeAxisTitle
                        };
                    }
                }
                else
                {
                    axis = new LinearAxis();
                }
                return axis;
            }
        }

        private static Axis BuildResourceChartYAxis()
        {
            return new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = Resource.ProjectPlan.Resources.Label_ResourcesAxisTitle
            };
        }

        private Task<DataTable> BuildResourceChartDataTableAsync()
        {
            return Task.Run(() => BuildResourceChartDataTable());
        }

        private DataTable BuildResourceChartDataTable()
        {
            lock (m_Lock)
            {
                var table = new DataTable();
                ResourceSeriesSetModel resourceSeriesSet = ResourceSeriesSet;

                if (resourceSeriesSet != null)
                {
                    IEnumerable<ResourceSeriesModel> combinedResourceSeries = resourceSeriesSet.Combined.OrderBy(x => x.DisplayOrder);

                    if (combinedResourceSeries.Any())
                    {
                        table.Columns.Add(new DataColumn(Resource.ProjectPlan.Resources.Label_TimeAxisTitle));

                        // Create the column titles.
                        foreach (ResourceSeriesModel resourceSeries in combinedResourceSeries)
                        {
                            var column = new DataColumn(resourceSeries.Title, typeof(int));
                            table.Columns.Add(column);
                        }

                        m_DateTimeCalculator.UseBusinessDays(UseBusinessDays);

                        // Pivot the series values.
                        int valueCount = combinedResourceSeries.Max(x => x.Values.Count);
                        for (int timeIndex = 0; timeIndex < valueCount; timeIndex++)
                        {
                            var rowData = new List<object>
                                {
                                    ChartHelper.FormatScheduleOutput(timeIndex, ShowDates, ProjectStart, m_DateTimeCalculator)
                                };
                            rowData.AddRange(combinedResourceSeries.Select(x => x.Values[timeIndex] * (ExportResourceChartAsCosts ? x.UnitCost : 1)).Cast<object>());
                            table.Rows.Add(rowData.ToArray());
                        }
                    }
                }

                return table;
            }
        }

        private void DispatchNotification(string title, object content)
        {
            m_NotificationInteractionRequest.Raise(
                new Notification
                {
                    Title = title,
                    Content = content
                });
        }

        #endregion

        #region IResourceChartManagerViewModel Members

        public string Title => Resource.ProjectPlan.Resources.Label_ResourceChartsViewTitle;

        public IInteractionRequest NotificationInteractionRequest => m_NotificationInteractionRequest;

        public bool IsBusy
        {
            get
            {
                return m_CoreViewModel.IsBusy;
            }
            private set
            {
                lock (m_Lock)
                {
                    m_CoreViewModel.IsBusy = value;
                }
                RaisePropertyChanged();
            }
        }

        public bool HasStaleOutputs => m_CoreViewModel.HasStaleOutputs;

        public bool ExportResourceChartAsCosts
        {
            get
            {
                return m_ExportResourceChartAsCosts;
            }
            set
            {
                lock (m_Lock)
                {
                    m_ExportResourceChartAsCosts = value;
                }
                RaisePropertyChanged();
            }
        }

        public PlotModel ResourceChartPlotModel
        {
            get
            {
                return m_ResourceChartPlotModel;
            }
            private set
            {
                lock (m_Lock)
                {
                    m_ResourceChartPlotModel = value;
                }
                RaisePropertyChanged();
            }
        }

        public int ResourceChartOutputWidth
        {
            get
            {
                return m_ResourceChartOutputWidth;
            }
            set
            {
                m_ResourceChartOutputWidth = value;
                RaisePropertyChanged();
            }
        }

        public int ResourceChartOutputHeight
        {
            get
            {
                return m_ResourceChartOutputHeight;
            }
            set
            {
                m_ResourceChartOutputHeight = value;
                RaisePropertyChanged();
            }
        }

        public ICommand CopyResourceChartToClipboardCommand
        {
            get;
            private set;
        }

        public ICommand ExportResourceChartToCsvCommand
        {
            get;
            private set;
        }

        #endregion

        #region IActiveAware Members

        public event EventHandler IsActiveChanged;

        public bool IsActive
        {
            get
            {
                return m_IsActive;
            }
            set
            {
                if (m_IsActive != value)
                {
                    m_IsActive = value;
                    IsActiveChanged?.Invoke(this, new EventArgs());
                }
            }
        }

        #endregion
    }
}
