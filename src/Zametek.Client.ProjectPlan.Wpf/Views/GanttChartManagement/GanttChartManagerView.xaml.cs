using System;
using System.Collections.Generic;
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
                    GenerateGanttChart(null, null);
                }, ThreadOption.UIThread);
        }

        private void UnsubscribeFromEvents()
        {
            m_EventService.GetEvent<PubSubEvent<GanttChartDataUpdatedPayload>>()
                .Unsubscribe(m_GanttChartDataUpdatedSubscriptionToken);
        }





        private void GenerateGanttChart(DateTime? minimumDateTime, DateTime? maximumDateTime)
        {
            GanttChartAreaCtrl.ClearGantt();
            IList<ManagedActivityViewModel> arrangedActivities = ViewModel.ArrangedActivities;

            if (arrangedActivities != null)
            {
                DateTime minDate = minimumDateTime ?? ViewModel.ProjectStart;
                DateTime? maybeMaxDate = maximumDateTime ?? minDate.AddDays(90);

                if (maybeMaxDate.HasValue)
                {
                    DateTime maxDate = maybeMaxDate.Value;

                    GanttChartAreaCtrl.Initialize(minDate, maxDate);

                    // Create timelines and define how they should be presented
                    GanttChartAreaCtrl.CreateTimeLine(new PeriodYearSplitter(minDate, maxDate), FormatYear);
                    GanttChartAreaCtrl.CreateTimeLine(new PeriodMonthSplitter(minDate, maxDate), FormatMonth);
                    var gridLineTimeLine = GanttChartAreaCtrl.CreateTimeLine(new PeriodDaySplitter(minDate, maxDate), FormatDay);
                    GanttChartAreaCtrl.CreateTimeLine(new PeriodDaySplitter(minDate, maxDate), FormatDayName);

                    // Set the timeline to attach gridlines to

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
        }










        private Brush DetermineBackground(TimeLineItem timeLineItem)
        {
            if (timeLineItem.End.Date.DayOfWeek == DayOfWeek.Saturday || timeLineItem.End.Date.DayOfWeek == DayOfWeek.Sunday)
                return new SolidColorBrush(Colors.LightBlue);
            return new SolidColorBrush(Colors.Transparent);
        }

        private string FormatYear(Period period)
        {
            return period.Start.Year.ToString();
        }

        private string FormatMonth(Period period)
        {
            return period.Start.Month.ToString();
        }

        private string FormatDay(Period period)
        {
            return period.Start.Day.ToString();
        }

        private string FormatDayName(Period period)
        {
            string returns = System.Globalization.CultureInfo.InvariantCulture.DateTimeFormat.DayNames[(int)period.Start.DayOfWeek];
            return returns.Substring(0, 2);
        }















        private void ButtonMonthBack_OnClick(object sender, RoutedEventArgs e)
        {
            DateTime minDate = GanttChartAreaCtrl.GanttData.MinDate.AddMonths(-1);
            DateTime maxDate = GanttChartAreaCtrl.GanttData.MaxDate.AddMonths(-1);
            GenerateGanttChart(minDate, maxDate);
        }

        private void ButtonDayBack_OnClick(object sender, RoutedEventArgs e)
        {
            DateTime minDate = GanttChartAreaCtrl.GanttData.MinDate.AddDays(-1);
            DateTime maxDate = GanttChartAreaCtrl.GanttData.MaxDate.AddDays(-1);
            GenerateGanttChart(minDate, maxDate);
        }

        private void ButtonMonthForth_OnClick(object sender, RoutedEventArgs e)
        {
            DateTime minDate = GanttChartAreaCtrl.GanttData.MinDate.AddMonths(1);
            DateTime maxDate = GanttChartAreaCtrl.GanttData.MaxDate.AddMonths(1);
            GenerateGanttChart(minDate, maxDate);
        }

        private void ButtonDayForth_OnClick(object sender, RoutedEventArgs e)
        {
            DateTime minDate = GanttChartAreaCtrl.GanttData.MinDate.AddDays(1);
            DateTime maxDate = GanttChartAreaCtrl.GanttData.MaxDate.AddDays(1);
            GenerateGanttChart(minDate, maxDate);
        }

        private void DatePicker_OnSelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            //DateTime minDate = DatePicker.DisplayDate;
            //DateTime maxDate = minDate.AddDays(DaysToShow);
            //GenerateGanttChart(minDate, maxDate);
        }

        private void Refresh_OnClick(object sender, RoutedEventArgs e)
        {
            //DaysToShow = Convert.ToInt32(DaysSelect.Text);

            //DateTime minDate = GanttChartAreaCtrl.GanttData.MinDate;
            //DateTime maxDate = minDate.AddDays(DaysToShow);
            //GenerateGanttChart(minDate, maxDate);
        }

        private void DaysSelect_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            //DaysToShow = Convert.ToInt32(DaysSelect.Text);
        }

        private void DaysSelect_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            //if (Regex.IsMatch(e.Text, @"^[0-9]"))
            //    e.Handled = false;
            //else
            //{
            //    e.Handled = true;
            //    MessageBox.Show("Mistake");
            //}
        }

        private void ButtonFirst_OnClick(object sender, RoutedEventArgs e)
        {
            //DateTime minDate = data.ListOfChartTimeSpans.Min(c => c.from).Date;
            //DateTime maxDate = minDate.AddMonths(2);
            //GenerateGanttChart(minDate, maxDate);
        }

        private void ButtonLast_OnClick(object sender, RoutedEventArgs e)
        {
            //DateTime maxDate = data.ListOfChartTimeSpans.Max(c => c.to).Date;
            //DateTime minDate = maxDate.AddMonths(-2);
            //GenerateGanttChart(minDate, maxDate);
        }




















        //private void SearchTextBox_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        //{
        //    Search = DaysSelect.Text;
        //}

        //private void SearchButton_OnClick(object sender, RoutedEventArgs e)
        //{
        //    ganttControl.ClearGantt();
        //    if (SearchTextBox.Text != "")
        //        foreach (ChartTimeSpan charttimespan in data.ListOfChartTimeSpans)
        //            charttimespan.selected = charttimespan.name.Contains(SearchTextBox.Text);
        //    else if (SearchTextBox.Text == "")
        //        foreach (ChartTimeSpan charttimespan in data.ListOfChartTimeSpans)
        //            charttimespan.selected = false;

        //    gantt.CreateData(this, data, ganttControl.GanttData.MinDate, ganttControl.GanttData.MaxDate);
        //}
















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
