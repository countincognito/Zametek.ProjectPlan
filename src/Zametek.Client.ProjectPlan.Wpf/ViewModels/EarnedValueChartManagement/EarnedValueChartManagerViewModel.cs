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
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Zametek.Maths.Graphs;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public class EarnedValueChartManagerViewModel
        : PropertyChangedPubSubViewModel, IEarnedValueChartManagerViewModel
    {
        #region Fields

        private readonly object m_Lock;

        private IList<EarnedValuePoint> m_EarnedValueChartPointSet;
        private PlotModel m_EarnedValueChartPlotModel;
        private int m_EarnedValueChartOutputWidth;
        private int m_EarnedValueChartOutputHeight;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IFileDialogService m_FileDialogService;
        private readonly IProjectSettingService m_ProjectSettingService;
        private readonly IDateTimeCalculator m_DateTimeCalculator;
        private readonly IEventAggregator m_EventService;

        private readonly InteractionRequest<Notification> m_NotificationInteractionRequest;

        private SubscriptionToken m_GraphCompilationUpdatedSubscriptionToken;

        #endregion

        #region Ctors

        public EarnedValueChartManagerViewModel(
            ICoreViewModel coreViewModel,
            IFileDialogService fileDialogService,
            IProjectSettingService projectSettingService,
            IDateTimeCalculator dateTimeCalculator,
            IEventAggregator eventService)
            : base(eventService)
        {
            m_Lock = new object();
            m_CoreViewModel = coreViewModel ?? throw new ArgumentNullException(nameof(coreViewModel));
            m_FileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
            m_ProjectSettingService = projectSettingService ?? throw new ArgumentNullException(nameof(projectSettingService));
            m_DateTimeCalculator = dateTimeCalculator ?? throw new ArgumentNullException(nameof(dateTimeCalculator));
            m_EventService = eventService ?? throw new ArgumentNullException(nameof(eventService));

            m_NotificationInteractionRequest = new InteractionRequest<Notification>();

            m_EarnedValueChartPointSet = new List<EarnedValuePoint>();
            EarnedValueChartPlotModel = null;
            EarnedValueChartOutputWidth = 1000;
            EarnedValueChartOutputHeight = 500;

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

        private GraphCompilation<int, IDependentActivity<int>> GraphCompilation => m_CoreViewModel.GraphCompilation;

        #endregion

        #region Commands

        private DelegateCommandBase InternalCopyEarnedValueChartToClipboardCommand
        {
            get;
            set;
        }

        private void CopyEarnedValueChartToClipboard()
        {
            lock (m_Lock)
            {
                if (CanCopyEarnedValueChartToClipboard())
                {
                    var pngExporter = new OxyPlot.Wpf.PngExporter
                    {
                        Width = EarnedValueChartOutputWidth,
                        Height = EarnedValueChartOutputHeight,
                        Background = OxyColors.White
                    };
                    BitmapSource bitmap = pngExporter.ExportToBitmap(EarnedValueChartPlotModel);
                    System.Windows.Clipboard.SetImage(bitmap);
                }
            }
        }

        private bool CanCopyEarnedValueChartToClipboard()
        {
            lock (m_Lock)
            {
                return EarnedValueChartPlotModel != null;
            }
        }

        private DelegateCommandBase InternalExportEarnedValueChartToCsvCommand
        {
            get;
            set;
        }

        private async void ExportEarnedValueChartToCsv()
        {
            await DoExportEarnedValueChartToCsvAsync();
        }

        private bool CanExportEarnedValueChartToCsv()
        {
            lock (m_Lock)
            {
                return m_EarnedValueChartPointSet.Any();
            }
        }

        #endregion

        #region Private Methods

        private void InitializeCommands()
        {
            CopyEarnedValueChartToClipboardCommand =
                InternalCopyEarnedValueChartToClipboardCommand =
                    new DelegateCommand(CopyEarnedValueChartToClipboard, CanCopyEarnedValueChartToClipboard);
            ExportEarnedValueChartToCsvCommand =
                InternalExportEarnedValueChartToCsvCommand =
                    new DelegateCommand(ExportEarnedValueChartToCsv, CanExportEarnedValueChartToCsv);
        }

        private void RaiseCanExecuteChangedAllCommands()
        {
            InternalCopyEarnedValueChartToClipboardCommand.RaiseCanExecuteChanged();
            InternalExportEarnedValueChartToCsvCommand.RaiseCanExecuteChanged();
        }

        private void SubscribeToEvents()
        {
            m_GraphCompilationUpdatedSubscriptionToken =
                m_EventService.GetEvent<PubSubEvent<GraphCompilationUpdatedPayload>>()
                    .Subscribe(payload =>
                    {
                        IsBusy = true;
                        SetEarnedValueChartPointSet();
                        SetEarnedValueChartPlotModel();
                        IsBusy = false;
                    }, ThreadOption.BackgroundThread);
        }

        private void UnsubscribeFromEvents()
        {
            m_EventService.GetEvent<PubSubEvent<GraphCompilationUpdatedPayload>>()
                .Unsubscribe(m_GraphCompilationUpdatedSubscriptionToken);
        }

        private void SetEarnedValueChartPointSet()
        {
            lock (m_Lock)
            {
                IList<IDependentActivity<int>> dependentActivities = GraphCompilation?.DependentActivities;
                if (dependentActivities != null)
                {
                    IList<IDependentActivity<int>> orderedDependentActivities = dependentActivities
                        .Select(x => (IDependentActivity<int>)x.WorkingCopy())
                        .OrderBy(x => x.EarliestFinishTime.GetValueOrDefault())
                        .ThenBy(x => x.EarliestStartTime.GetValueOrDefault())
                        .ToList();
                    var pointSet = new List<EarnedValuePoint>();
                    if (!HasCompilationErrors
                        && orderedDependentActivities.Any()
                        && orderedDependentActivities.All(x => x.EarliestFinishTime.HasValue))
                    {
                        pointSet.Add(new EarnedValuePoint
                        {
                            Time = 0,
                            ActivityId = string.Empty,
                            ActivityName = string.Empty,
                            EarnedValue = 0,
                            EarnedValuePercentage = 0.0
                        });

                        double totalTime = Convert.ToDouble(orderedDependentActivities.Sum(s => s.Duration));
                        int runningTotal = 0;
                        foreach (IDependentActivity<int> activity in orderedDependentActivities)
                        {
                            runningTotal += activity.Duration;
                            double percentage = (runningTotal / totalTime) * 100.0;
                            int time = activity.EarliestFinishTime.GetValueOrDefault();
                            pointSet.Add(new EarnedValuePoint
                            {
                                Time = time,
                                ActivityId = activity.Id.ToString(),
                                ActivityName = activity.Name,
                                EarnedValue = runningTotal,
                                EarnedValuePercentage = percentage
                            });
                        }
                    }

                    m_EarnedValueChartPointSet.Clear();
                    foreach (EarnedValuePoint point in pointSet)
                    {
                        m_EarnedValueChartPointSet.Add(point);
                    }
                }
            }
        }

        private void SetEarnedValueChartPlotModel()
        {
            lock (m_Lock)
            {
                IList<EarnedValuePoint> pointSet = m_EarnedValueChartPointSet;
                PlotModel plotModel = null;
                if (pointSet != null
                    && pointSet.Any())
                {
                    plotModel = new PlotModel();
                    plotModel.Axes.Add(BuildEarnedValueChartXAxis());
                    plotModel.Axes.Add(BuildEarnedValueChartYAxis());
                    plotModel.LegendPlacement = LegendPlacement.Outside;
                    plotModel.LegendPosition = LegendPosition.RightMiddle;

                    var lineSeries = new LineSeries();
                    m_DateTimeCalculator.UseBusinessDays(UseBusinessDays);

                    foreach (EarnedValuePoint point in pointSet)
                    {
                        lineSeries.Points.Add(
                            new DataPoint(ChartHelper.CalculateChartTimeXValue(point.Time, ShowDates, ProjectStart, m_DateTimeCalculator),
                            point.EarnedValuePercentage));
                    }
                    plotModel.Series.Add(lineSeries);
                }
                EarnedValueChartPlotModel = plotModel;
            }
            RaiseCanExecuteChangedAllCommands();
        }

        private Axis BuildEarnedValueChartXAxis()
        {
            lock (m_Lock)
            {
                IList<IDependentActivity<int>> dependentActivities = GraphCompilation?.DependentActivities;
                Axis axis = null;
                if (dependentActivities != null
                    && dependentActivities.Any())
                {
                    int finishTime = dependentActivities.Max(x => x.EarliestFinishTime.GetValueOrDefault());
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

        private static Axis BuildEarnedValueChartYAxis()
        {
            return new LinearAxis
            {
                Position = AxisPosition.Left,
                Minimum = 0.0,
                Maximum = 100.0,
                Title = Properties.Resources.Label_EarnedValuePercentageAxisTitle
            };
        }

        private Task<DataTable> BuildEarnedValueChartDataTableAsync()
        {
            return Task.Run(() => BuildEarnedValueChartDataTable());
        }

        private DataTable BuildEarnedValueChartDataTable()
        {
            lock (m_Lock)
            {
                var table = new DataTable();
                IList<EarnedValuePoint> pointSet = m_EarnedValueChartPointSet;
                if (pointSet != null
                    && pointSet.Any())
                {
                    table.Columns.Add(new DataColumn(Properties.Resources.Label_TimeAxisTitle));
                    table.Columns.Add(new DataColumn(Properties.Resources.Label_Id));
                    table.Columns.Add(new DataColumn(Properties.Resources.Label_ActivityName));
                    table.Columns.Add(new DataColumn(Properties.Resources.Label_EarnedValueTitle));
                    table.Columns.Add(new DataColumn(Properties.Resources.Label_EarnedValuePercentageAxisTitle));

                    m_DateTimeCalculator.UseBusinessDays(UseBusinessDays);

                    foreach (EarnedValuePoint point in pointSet)
                    {
                        var rowData = new List<object>
                        {
                            ChartHelper.FormatScheduleOutput(point.Time, ShowDates, ProjectStart, m_DateTimeCalculator),
                            point.ActivityId,
                            point.ActivityName,
                            point.EarnedValue,
                            point.EarnedValuePercentage
                        };
                        table.Rows.Add(rowData.ToArray());
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

        #region Public Methods

        public async Task DoExportEarnedValueChartToCsvAsync()
        {
            try
            {
                IsBusy = true;
                string directory = m_ProjectSettingService.PlanDirectory;
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
                        DataTable dataTable = await BuildEarnedValueChartDataTableAsync();
                        await ChartHelper.ExportDataTableToCsvAsync(dataTable, filename);
                        m_ProjectSettingService.SetDirectory(filename);
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

        #region IEarnedValueChartManagerViewModel Members

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

        public PlotModel EarnedValueChartPlotModel
        {
            get
            {
                return m_EarnedValueChartPlotModel;
            }
            private set
            {
                lock (m_Lock)
                {
                    m_EarnedValueChartPlotModel = value;
                }
                RaisePropertyChanged();
            }
        }

        public int EarnedValueChartOutputWidth
        {
            get
            {
                return m_EarnedValueChartOutputWidth;
            }
            set
            {
                m_EarnedValueChartOutputWidth = value;
                RaisePropertyChanged();
            }
        }

        public int EarnedValueChartOutputHeight
        {
            get
            {
                return m_EarnedValueChartOutputHeight;
            }
            set
            {
                m_EarnedValueChartOutputHeight = value;
                RaisePropertyChanged();
            }
        }

        public ICommand CopyEarnedValueChartToClipboardCommand
        {
            get;
            private set;
        }

        public ICommand ExportEarnedValueChartToCsvCommand
        {
            get;
            private set;
        }

        #endregion

        #region Private Types

        public class EarnedValuePoint
        {
            public int Time { get; set; }
            public string ActivityId { get; set; }
            public string ActivityName { get; set; }
            public int EarnedValue { get; set; }
            public double EarnedValuePercentage { get; set; }
        }

        #endregion
    }
}
