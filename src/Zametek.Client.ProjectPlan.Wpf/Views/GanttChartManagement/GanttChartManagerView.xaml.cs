using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using nGantt.GanttChart;
using nGantt.PeriodSplitter;
using Prism;
using Prism.Events;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public partial class GanttChartManagerView
        : IActiveAware
    {
        #region Fields

        private bool m_IsActive;
        private readonly IEventAggregator m_EventService;
        private SubscriptionToken m_GanttChartDataUpdatedSubscriptionToken;

        #endregion

        #region Ctors

        public GanttChartManagerView(
            IGanttChartManagerViewModel viewModel,
            IEventAggregator eventService)
        {
            m_EventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
            InitializeComponent();
            ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            SubscribeToEvents();
        }

        #endregion

        #region Properties

        public IGanttChartManagerViewModel ViewModel
        {
            get
            {
                return DataContext as IGanttChartManagerViewModel;
            }
            set
            {
                DataContext = value;
            }
        }

        #endregion

        #region Private Methods

        private void SubscribeToEvents()
        {
            m_GanttChartDataUpdatedSubscriptionToken =
                m_EventService.GetEvent<PubSubEvent<GanttChartDataUpdatedPayload>>()
                .Subscribe(payload =>
                {
                    GenerateGanttChart();
                }, ThreadOption.UIThread);
        }

        private void UnsubscribeFromEvents()
        {
            m_EventService.GetEvent<PubSubEvent<GanttChartDataUpdatedPayload>>()
                .Unsubscribe(m_GanttChartDataUpdatedSubscriptionToken);
        }

        private void DatePicker_OnSelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void GenerateGanttChart()
        {
            GanttChartAreaCtrl.ClearGantt();
            IList<ManagedActivityViewModel> arrangedActivities = ViewModel.ArrangedActivities;

            if (arrangedActivities != null)
            {
                DateTime minDate = DatePicker.SelectedDate ?? ViewModel.ProjectStart;
                DateTime maxDate = minDate.AddDays(DaysSelect.Value.GetValueOrDefault());
                GanttChartAreaCtrl.Initialize(minDate, maxDate);

                // Create timelines and define how they should be presented
                GanttChartAreaCtrl.CreateTimeLine(new PeriodYearSplitter(minDate, maxDate), FormatYear);
                GanttChartAreaCtrl.CreateTimeLine(new PeriodMonthSplitter(minDate, maxDate), FormatMonth);
                var gridLineTimeLine = GanttChartAreaCtrl.CreateTimeLine(new PeriodDaySplitter(minDate, maxDate), FormatDay);
                GanttChartAreaCtrl.CreateTimeLine(new PeriodDaySplitter(minDate, maxDate), FormatDayName);

                // Set the timeline to attach gridlines to
                GanttChartAreaCtrl.SetGridLinesTimeline(gridLineTimeLine, DetermineBackground);

                foreach (ManagedActivityViewModel managedActivityViewModel in arrangedActivities)
                {
                    HeaderedGanttRowGroup rowgroupprojectphases = GanttChartAreaCtrl.CreateGanttRowGroup("Example-Heading");
                    GanttRow row = GanttChartAreaCtrl.CreateGanttRow(rowgroupprojectphases, managedActivityViewModel.Name);





                    if (managedActivityViewModel.EarliestStartDateTime.HasValue
                        && managedActivityViewModel.EarliestFinishDateTime.HasValue)
                    {
                        GanttChartAreaCtrl.AddGanttTask(row, new GanttTask
                        {
                            Start = managedActivityViewModel.EarliestStartDateTime.Value,
                            End = managedActivityViewModel.EarliestFinishDateTime.Value,
                            Name = $"{managedActivityViewModel.Name}",
                            Color = Colors.OrangeRed, //sortedchartTimeSpan.selected ? Colors.OrangeRed : Colors.DodgerBlue,
                            Radius = 5,//(sortedchartTimeSpan.to - sortedchartTimeSpan.from).TotalDays < 3 ? 0 : 5
                        });
                    }




                }
            }
        }

        private Brush DetermineBackground(TimeLineItem timeLineItem)
        {
            if (timeLineItem.End.Date.DayOfWeek == DayOfWeek.Saturday || timeLineItem.End.Date.DayOfWeek == DayOfWeek.Sunday)
                return new SolidColorBrush(Colors.LightBlue);
            return new SolidColorBrush(Colors.Transparent);
        }

        private string FormatYear(Period period)
        {
            return period.Start.ToString("yyyy", DateTimeFormatInfo.InvariantInfo);
        }

        private string FormatMonth(Period period)
        {
            return period.Start.ToString("MMM", DateTimeFormatInfo.InvariantInfo);
        }

        private string FormatDay(Period period)
        {
            return period.Start.ToString("dd", DateTimeFormatInfo.InvariantInfo);
        }

        private string FormatDayName(Period period)
        {
            return period.Start.ToString("ddd", DateTimeFormatInfo.InvariantInfo);
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
