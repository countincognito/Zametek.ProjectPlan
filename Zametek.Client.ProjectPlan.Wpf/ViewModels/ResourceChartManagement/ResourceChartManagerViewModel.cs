using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using Prism.Commands;
using Prism.Events;
using Prism.Interactivity.InteractionRequest;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Zametek.Common.Project;
using Zametek.Maths.Graphs;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public class ResourceChartManagerViewModel
        : PropertyChangedPubSubViewModel, IResourceChartManagerViewModel
    {
        #region Fields

        private readonly object m_Lock;
        private bool m_IsBusy;
        private IList<ResourceSeries> m_ResourceChartSeriesSet;
        private bool m_ExportResourceChartAsCosts;
        private PlotModel m_ResourceChartPlotModel;
        private int m_ResourceChartOutputWidth;
        private int m_ResourceChartOutputHeight;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IFileDialogService m_FileDialogService;
        private readonly IAppSettingService m_AppSettingService;
        private readonly IDateTimeCalculator m_DateTimeCalculator;
        private readonly IEventAggregator m_EventService;

        private readonly InteractionRequest<Notification> m_NotificationInteractionRequest;

        private SubscriptionToken m_GraphCompiledPayloadToken;

        #endregion

        #region Ctors

        public ResourceChartManagerViewModel(
            ICoreViewModel coreViewModel,
            IFileDialogService fileDialogService,
            IAppSettingService appSettingService,
            IDateTimeCalculator dateTimeCalculator,
            IEventAggregator eventService)
            : base(eventService)
        {
            m_Lock = new object();
            m_CoreViewModel = coreViewModel ?? throw new ArgumentNullException(nameof(coreViewModel));
            m_FileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
            m_AppSettingService = appSettingService ?? throw new ArgumentNullException(nameof(appSettingService));
            m_DateTimeCalculator = dateTimeCalculator ?? throw new ArgumentNullException(nameof(dateTimeCalculator));
            m_EventService = eventService ?? throw new ArgumentNullException(nameof(eventService));

            m_NotificationInteractionRequest = new InteractionRequest<Notification>();

            m_ResourceChartSeriesSet = new List<ResourceSeries>();
            ResourceChartPlotModel = null;
            ResourceChartOutputWidth = 1000;
            ResourceChartOutputHeight = 500;

            InitializeCommands();
            SubscribeToEvents();

            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.HasStaleOutputs), nameof(HasStaleOutputs), ThreadOption.BackgroundThread);
        }

        #endregion

        #region Properties

        private DateTime ProjectStart => m_CoreViewModel.ProjectStart;

        private bool ShowDates => m_CoreViewModel.ShowDates;

        private bool HasCompilationErrors => m_CoreViewModel.HasCompilationErrors;

        private GraphCompilation<int, IDependentActivity<int>> GraphCompilation => m_CoreViewModel.GraphCompilation;

        private IList<ResourceDto> ResourceDtos => m_CoreViewModel.ResourceDtos;

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
            await DoExportResourceChartToCsvAsync();
        }

        private bool CanExportResourceChartToCsv()
        {
            lock (m_Lock)
            {
                return m_ResourceChartSeriesSet.Any();
            }
        }

        #endregion

        #region Public Methods

        public async Task DoExportResourceChartToCsvAsync()
        {
            try
            {
                IsBusy = true;
                string directory = m_AppSettingService.ProjectPlanFolder;
                if (m_FileDialogService.ShowSaveDialog(
                    directory,
                    Properties.Resources.Filter_SaveCsvFileType,
                    Properties.Resources.Filter_SaveCsvFileExtension) == DialogResult.OK)
                {
                    string filename = m_FileDialogService.Filename;
                    if (string.IsNullOrWhiteSpace(filename))
                    {
                        DispatchNotification(
                            Properties.Resources.Title_Error,
                            Properties.Resources.Message_EmptyFilename);
                    }
                    else
                    {
                        DataTable dataTable = await BuildResourceChartDataTableAsync();
                        await ChartHelper.ExportDataTableToCsvAsync(dataTable, filename);
                        m_AppSettingService.ProjectPlanFolder = Path.GetDirectoryName(filename);
                    }
                }
            }
            catch (Exception ex)
            {
                DispatchNotification(
                    Properties.Resources.Title_Error,
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
            m_GraphCompiledPayloadToken =
                m_EventService.GetEvent<PubSubEvent<GraphCompiledPayload>>()
                    .Subscribe(payload =>
                    {
                        SetResourceChartSeriesSet();
                        SetResourceChartPlotModel();
                        CalculateCosts();
                    }, ThreadOption.BackgroundThread);
        }

        private void UnsubscribeFromEvents()
        {
            m_EventService.GetEvent<PubSubEvent<GraphCompiledPayload>>()
                .Unsubscribe(m_GraphCompiledPayloadToken);
        }

        private void SetResourceChartSeriesSet()
        {
            lock (m_Lock)
            {
                IList<IResourceSchedule<int>> resourceSchedules = GraphCompilation?.ResourceSchedules;
                var seriesSet = new List<ResourceSeries>();
                if (resourceSchedules != null
                    && resourceSchedules.Any())
                {
                    IDictionary<int, ColorFormatDto> colorFormatLookup = ResourceDtos.ToDictionary(x => x.Id, x => x.ColorFormat);
                    var indirectResourceIdsToIgnore = new HashSet<int>();
                    int finishTime = resourceSchedules.Max(x => x.FinishTime);
                    int spareResourceCount = 1;
                    var scheduledSeriesSet = new List<ResourceSeries>();
                    for (int resourceIndex = 0; resourceIndex < resourceSchedules.Count; resourceIndex++)
                    {
                        IResourceSchedule<int> resourceSchedule = resourceSchedules[resourceIndex];
                        var series = new ResourceSeries()
                        {
                            Values = resourceSchedule.ActivityAllocation.Select(x => x ? 1 : 0).ToList()
                        };
                        series.InterActivityAllocationType = InterActivityAllocationType.None;
                        var stringBuilder = new StringBuilder();
                        IResource<int> resource = resourceSchedule.Resource;

                        if (resource != null)
                        {
                            series.InterActivityAllocationType = resource.InterActivityAllocationType;
                            indirectResourceIdsToIgnore.Add(resource.Id);
                            if (string.IsNullOrWhiteSpace(resource.Name))
                            {
                                stringBuilder.Append($@"Resource {resource.Id}");
                            }
                            else
                            {
                                stringBuilder.Append($@"{resource.Name}");
                            }
                        }
                        else
                        {
                            stringBuilder.Append($@"Resource {spareResourceCount}");
                            spareResourceCount++;
                        }

                        series.Title = stringBuilder.ToString();
                        series.ColorFormatDto = resource != null && colorFormatLookup.ContainsKey(resource.Id) ? colorFormatLookup[resource.Id].Copy() : new ColorFormatDto().Randomize();
                        series.UnitCost = resource?.UnitCost ?? 0;
                        series.DisplayOrder = resource?.DisplayOrder ?? 0;
                        scheduledSeriesSet.Add(series);
                    }

                    // Now add the remaining resources that are indirect costs, but
                    // sort them separately and add them to the front of the list.
                    var unscheduledSeriesSet = new List<ResourceSeries>();
                    IEnumerable<ResourceDto> indirectResources =
                        ResourceDtos.Where(x => !indirectResourceIdsToIgnore.Contains(x.Id) && x.InterActivityAllocationType == InterActivityAllocationType.Indirect);

                    foreach (ResourceDto resourceDto in indirectResources)
                    {
                        var series = new ResourceSeries()
                        {
                            InterActivityAllocationType = resourceDto.InterActivityAllocationType,
                            Values = new List<int>(Enumerable.Repeat(1, finishTime))
                        };
                        var stringBuilder = new StringBuilder();
                        if (string.IsNullOrWhiteSpace(resourceDto.Name))
                        {
                            stringBuilder.Append($@"Resource {resourceDto.Id}");
                        }
                        else
                        {
                            stringBuilder.Append($@"{resourceDto.Name}");
                        }

                        series.Title = stringBuilder.ToString();
                        series.ColorFormatDto = resourceDto.ColorFormat != null ? resourceDto.ColorFormat.Copy() : new ColorFormatDto().Randomize();
                        series.UnitCost = resourceDto.UnitCost;
                        series.DisplayOrder = resourceDto.DisplayOrder;
                        unscheduledSeriesSet.Add(series);
                    }

                    seriesSet.AddRange(unscheduledSeriesSet.OrderBy(x => x.DisplayOrder));
                    seriesSet.AddRange(scheduledSeriesSet.OrderBy(x => x.DisplayOrder));
                }

                m_ResourceChartSeriesSet.Clear();
                foreach (ResourceSeries series in seriesSet)
                {
                    m_ResourceChartSeriesSet.Add(series);
                }
            }
            RaiseCanExecuteChangedAllCommands();
        }

        private void SetResourceChartPlotModel()
        {
            lock (m_Lock)
            {
                IList<ResourceSeries> seriesSet = m_ResourceChartSeriesSet;
                PlotModel plotModel = null;
                if (seriesSet != null
                    && seriesSet.Any())
                {
                    plotModel = new PlotModel();
                    plotModel.Axes.Add(BuildResourceChartXAxis());
                    plotModel.Axes.Add(BuildResourceChartYAxis());
                    plotModel.LegendPlacement = LegendPlacement.Outside;
                    plotModel.LegendPosition = LegendPosition.RightMiddle;

                    var total = new List<int>();
                    m_DateTimeCalculator.UseBusinessDays(m_CoreViewModel.UseBusinessDays);

                    foreach (ResourceSeries series in seriesSet)
                    {
                        if (series != null)
                        {
                            var areaSeries = new AreaSeries
                            {
                                Smooth = false,
                                StrokeThickness = 0.0,
                                Title = series.Title,
                                Color = OxyColor.FromArgb(
                                    series.ColorFormatDto.A,
                                    series.ColorFormatDto.R,
                                    series.ColorFormatDto.G,
                                    series.ColorFormatDto.B)
                            };
                            for (int i = 0; i < series.Values.Count; i++)
                            {
                                int j = series.Values[i];
                                if (i >= total.Count)
                                {
                                    total.Add(0);
                                }
                                areaSeries.Points.Add(
                                    new DataPoint(ChartHelper.CalculateChartTimeXValue(i, ShowDates, ProjectStart, m_DateTimeCalculator),
                                    total[i]));
                                total[i] += j;
                                areaSeries.Points2.Add(
                                    new DataPoint(ChartHelper.CalculateChartTimeXValue(i, ShowDates, ProjectStart, m_DateTimeCalculator),
                                    total[i]));
                            }
                            plotModel.Series.Add(areaSeries);
                        }
                    }
                }
                ResourceChartPlotModel = plotModel;
            }
        }

        private Axis BuildResourceChartXAxis()
        {
            lock (m_Lock)
            {
                IList<IResourceSchedule<int>> resourceSchedules = GraphCompilation?.ResourceSchedules;
                Axis axis = null;
                if (resourceSchedules != null
                    && resourceSchedules.Any())
                {
                    int finishTime = resourceSchedules.Max(x => x.FinishTime);
                    m_DateTimeCalculator.UseBusinessDays(m_CoreViewModel.UseBusinessDays);
                    double minValue = ChartHelper.CalculateChartTimeXValue(0, ShowDates, ProjectStart, m_DateTimeCalculator);
                    double maxValue = ChartHelper.CalculateChartTimeXValue(finishTime, ShowDates, ProjectStart, m_DateTimeCalculator);

                    if (ShowDates)
                    {
                        axis = new DateTimeAxis
                        {
                            Position = AxisPosition.Bottom,
                            Minimum = minValue,
                            Maximum = maxValue,
                            Title = Properties.Resources.Label_TimeAxisTitle,
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
                            Title = Properties.Resources.Label_TimeAxisTitle
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
                Title = Properties.Resources.Label_ResourcesAxisTitle
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
                IList<ResourceSeries> seriesSet = m_ResourceChartSeriesSet.OrderBy(x => x.DisplayOrder).ToList();
                if (seriesSet != null
                    && seriesSet.Any())
                {
                    table.Columns.Add(new DataColumn(Properties.Resources.Label_TimeAxisTitle));

                    // Create the column titles.
                    for (int seriesIndex = 0; seriesIndex < seriesSet.Count; seriesIndex++)
                    {
                        var column = new DataColumn(seriesSet[seriesIndex].Title, typeof(int));
                        table.Columns.Add(column);
                    }

                    m_DateTimeCalculator.UseBusinessDays(m_CoreViewModel.UseBusinessDays);

                    // Pivot the series values.
                    int valueCount = seriesSet.Max(x => x.Values.Count);
                    for (int timeIndex = 0; timeIndex < valueCount; timeIndex++)
                    {
                        var rowData = new List<object>
                        {
                            ChartHelper.FormatScheduleOutput(timeIndex, ShowDates, ProjectStart, m_DateTimeCalculator)
                        };
                        rowData.AddRange(seriesSet.Select(x => x.Values[timeIndex] * (ExportResourceChartAsCosts ? x.UnitCost : 1)).Cast<object>());
                        table.Rows.Add(rowData.ToArray());
                    }
                }
                return table;
            }
        }

        private void CalculateCosts()
        {
            lock (m_Lock)
            {
                ClearCostProperties();
                if (HasCompilationErrors)
                {
                    return;
                }
                IList<ResourceSeries> seriesSet = m_ResourceChartSeriesSet;
                if (seriesSet != null
                    && seriesSet.Any())
                {
                    DirectCost = seriesSet
                        .Where(x => x.InterActivityAllocationType == InterActivityAllocationType.Direct)
                        .Sum(x => x.Values.Sum(y => y * x.UnitCost));
                    IndirectCost = seriesSet
                        .Where(x => x.InterActivityAllocationType == InterActivityAllocationType.Indirect)
                        .Sum(x => x.Values.Sum(y => y * x.UnitCost));
                    OtherCost = seriesSet
                        .Where(x => x.InterActivityAllocationType == InterActivityAllocationType.None)
                        .Sum(x => x.Values.Sum(y => y * x.UnitCost));
                    TotalCost = seriesSet
                        .Sum(x => x.Values.Sum(y => y * x.UnitCost));
                }
            }
        }

        private void ClearCostProperties()
        {
            lock (m_Lock)
            {
                DirectCost = null;
                IndirectCost = null;
                OtherCost = null;
                TotalCost = null;
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

        public IInteractionRequest NotificationInteractionRequest => m_NotificationInteractionRequest;

        public bool IsBusy
        {
            get
            {
                return m_IsBusy;
            }
            private set
            {
                m_IsBusy = value;
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

        public double? DirectCost
        {
            get
            {
                return m_CoreViewModel.DirectCost;
            }
            private set
            {
                m_CoreViewModel.DirectCost = value;
                RaisePropertyChanged();
            }
        }

        public double? IndirectCost
        {
            get
            {
                return m_CoreViewModel.IndirectCost;
            }
            private set
            {
                m_CoreViewModel.IndirectCost = value;
                RaisePropertyChanged();
            }
        }

        public double? OtherCost
        {
            get
            {
                return m_CoreViewModel.OtherCost;
            }
            private set
            {
                m_CoreViewModel.OtherCost = value;
                RaisePropertyChanged();
            }
        }

        public double? TotalCost
        {
            get
            {
                return m_CoreViewModel.TotalCost;
            }
            private set
            {
                m_CoreViewModel.TotalCost = value;
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

        #region Private Types

        private class ResourceSeries
        {
            public string Title { get; set; }
            public InterActivityAllocationType InterActivityAllocationType { get; set; }
            public IList<int> Values { get; set; }
            public ColorFormatDto ColorFormatDto { get; set; }
            public double UnitCost { get; set; }
            public int DisplayOrder { get; set; }
        }

        #endregion
    }
}
