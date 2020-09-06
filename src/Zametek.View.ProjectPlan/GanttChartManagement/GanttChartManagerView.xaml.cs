using nGantt.GanttChart;
using nGantt.PeriodSplitter;
using Prism;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Event.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;
using Zametek.Wpf.Core;

namespace Zametek.View.ProjectPlan
{
    [AvalonDockAnchorable(Strategy = AnchorableStrategies.Top, IsHidden = false)]
    public partial class GanttChartManagerView
        : IActiveAware
    {
        #region Fields

        private readonly IDateTimeCalculator m_DateTimeCalculator;
        private readonly IEventAggregator m_EventService;
        private SubscriptionToken m_GanttChartDataUpdatedSubscriptionToken;

        private bool m_IsActive;

        #endregion

        #region Ctors

        public GanttChartManagerView(
            IGanttChartManagerViewModel viewModel,
            IDateTimeCalculator dateTimeCalculator,
            IEventAggregator eventService)
        {
            m_DateTimeCalculator = dateTimeCalculator ?? throw new ArgumentNullException(nameof(dateTimeCalculator));
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

        private void PublishGanttChartSettingsUpdatedPayload()
        {
            m_EventService.GetEvent<PubSubEvent<GanttChartSettingsUpdatedPayload>>()
                .Publish(new GanttChartSettingsUpdatedPayload());
        }

        private void DatePicker_OnSelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            PublishGanttChartSettingsUpdatedPayload();
        }

        private void DaysSelect_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            PublishGanttChartSettingsUpdatedPayload();
        }

        private void GroupByResource_CheckChanged(object sender, RoutedEventArgs e)
        {
            PublishGanttChartSettingsUpdatedPayload();
        }

        private void GenerateGanttChart()
        {
            GanttChartAreaCtrl.ClearGantt();
            GanttChartModel ganttChart = ViewModel.GanttChart;

            if (ganttChart != null)
            {
                IList<DependentActivityModel> dependentActivities = ganttChart.DependentActivities;
                ResourceSeriesSetModel resourceSeriesSet = ganttChart.ResourceSeriesSet;

                m_DateTimeCalculator.UseBusinessDays(ViewModel.UseBusinessDays);

                DateTime projectStart = ViewModel.ProjectStart;

                DateTime minDate = DatePicker.SelectedDate ?? projectStart;
                DateTime maxDate = minDate.AddDays(DaysSelect.Value.GetValueOrDefault());
                GanttChartAreaCtrl.Initialize(minDate, maxDate);

                // Create timelines and define how they should be presented.
                GanttChartAreaCtrl.CreateTimeLine(new PeriodYearSplitter(minDate, maxDate), FormatYear);
                GanttChartAreaCtrl.CreateTimeLine(new PeriodMonthSplitter(minDate, maxDate), FormatMonth);
                TimeLine gridLineTimeLine = GanttChartAreaCtrl.CreateTimeLine(new PeriodDaySplitter(minDate, maxDate), FormatDay);
                //GanttChartAreaCtrl.CreateTimeLine(new PeriodDaySplitter(minDate, maxDate), FormatDayName);

                // Attach gridlines.
                GanttChartAreaCtrl.SetGridLinesTimeline(gridLineTimeLine, DetermineBackground);

                // Prep formatting helpers.
                SlackColorFormatLookup colorFormatLookup = null;
                ArrowGraphSettingsModel arrowGraphSettings = ViewModel.ArrowGraphSettings;

                if (arrowGraphSettings?.ActivitySeverities != null)
                {
                    colorFormatLookup = new SlackColorFormatLookup(arrowGraphSettings.ActivitySeverities);
                }

                if (GroupByResource.IsChecked.GetValueOrDefault())
                {
                    BuildGanttChart(dependentActivities.Select(x => x.Activity), resourceSeriesSet, projectStart, colorFormatLookup);
                }
                else
                {
                    BuildGanttChart(dependentActivities.Select(x => x.Activity), projectStart, colorFormatLookup);
                }
            }
        }

        private void BuildGanttChart(
            IEnumerable<ActivityModel> activities,
            ResourceSeriesSetModel resourceSeriesSet,
            DateTime projectStart,
            SlackColorFormatLookup colorFormatLookup)
        {
            if (activities == null || resourceSeriesSet?.Scheduled == null)
            {
                return;
            }

            IDictionary<int, ActivityModel> activityLookup = activities.ToDictionary(x => x.Id);

            foreach (ResourceSeriesModel resourceSeries in resourceSeriesSet.Scheduled)
            {
                ResourceScheduleModel resourceSchedule = resourceSeries.ResourceSchedule;

                if (resourceSchedule != null)
                {
                    GanttRowGroup rowGroup = GanttChartAreaCtrl.CreateGanttRowGroup(resourceSeries.Title);

                    foreach (ScheduledActivityModel scheduledctivity in resourceSchedule.ScheduledActivities)
                    {
                        if (activityLookup.TryGetValue(scheduledctivity.Id, out ActivityModel activity))
                        {
                            string name = string.IsNullOrWhiteSpace(scheduledctivity.Name) ? scheduledctivity.Id.ToString(CultureInfo.InvariantCulture) : scheduledctivity.Name;
                            GanttRow row = GanttChartAreaCtrl.CreateGanttRow(rowGroup, name);

                            if (activity.EarliestStartTime.HasValue
                                && activity.EarliestFinishTime.HasValue)
                            {
                                CheckRowErrors(activity, row);

                                GanttChartAreaCtrl.AddGanttTask(
                                    row,
                                    CreateGanttTask(projectStart, activity, colorFormatLookup));
                            }
                        }
                    }
                }
            }
        }

        private void BuildGanttChart(
            IEnumerable<ActivityModel> activities,
            DateTime projectStart,
            SlackColorFormatLookup colorFormatLookup)
        {
            if (activities == null)
            {
                return;
            }

            foreach (ActivityModel activity in activities)
            {
                GanttRowGroup rowGroup = GanttChartAreaCtrl.CreateGanttRowGroup();
                string name = string.IsNullOrWhiteSpace(activity.Name) ? activity.Id.ToString(CultureInfo.InvariantCulture) : activity.Name;
                GanttRow row = GanttChartAreaCtrl.CreateGanttRow(rowGroup, name);

                if (activity.EarliestStartTime.HasValue
                    && activity.EarliestFinishTime.HasValue)
                {
                    CheckRowErrors(activity, row);

                    GanttChartAreaCtrl.AddGanttTask(
                        row,
                        CreateGanttTask(projectStart, activity, colorFormatLookup));
                }
            }
        }

        private void CheckRowErrors(ActivityModel activity, GanttRow row)
        {
            if (activity is null
                || row is null)
            {
                return;
            }
            if (activity.FreeSlack.GetValueOrDefault() < 0
                || activity.TotalSlack.GetValueOrDefault() < 0
                || (activity.TotalSlack.GetValueOrDefault() - activity.FreeSlack.GetValueOrDefault()) < 0)
            {
                row.HasErrors = true;
            }
        }

        private GanttTask CreateGanttTask(
            DateTime projectStart,
            ActivityModel activity,
            SlackColorFormatLookup colorFormatLookup)
        {
            Color background = colorFormatLookup?.FindSlackColor(activity.TotalSlack) ?? Colors.DodgerBlue;
            string name = string.IsNullOrWhiteSpace(activity.Name) ? activity.Id.ToString(CultureInfo.InvariantCulture) : activity.Name;

            return new GanttTask
            {
                Start = m_DateTimeCalculator.AddDays(projectStart, activity.EarliestStartTime.Value),
                End = m_DateTimeCalculator.AddDays(projectStart, activity.EarliestFinishTime.Value),
                Name = name,
                BackgroundColor = new SolidColorBrush(background),
                ForegroundColor = new SolidColorBrush(ContrastConvert(background)),
                Radius = activity.Duration < 3 ? 0 : 5,
                TaskProgressVisibility = Visibility.Collapsed,
            };
        }

        /// <summary>
        /// https://stackoverflow.com/questions/6763032/how-to-pick-a-background-color-depending-on-font-color-to-have-proper-contrast
        /// </summary>
        public static Color ContrastConvert(Color color)
        {
            double x = 0.2126 * color.ScR + 0.7152 * color.ScG + 0.0722 * color.ScB;
            return x < 0.5 ? Colors.White : Colors.Black;
        }

        private Brush DetermineBackground(TimeLineItem timeLineItem)
        {
            if (timeLineItem.End.Date.DayOfWeek == DayOfWeek.Saturday || timeLineItem.End.Date.DayOfWeek == DayOfWeek.Sunday)
            {
                return new SolidColorBrush(Colors.LightBlue);
            }
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
            return null;// period.Start.ToString("dd", DateTimeFormatInfo.InvariantInfo);
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
