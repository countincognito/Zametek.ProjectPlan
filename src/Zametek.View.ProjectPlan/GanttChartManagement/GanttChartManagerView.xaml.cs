using nGantt.GanttChart;
using nGantt.PeriodSplitter;
using Prism;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Event.ProjectPlan;
using Zametek.Maths.Graphs;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public partial class GanttChartManagerView
        : IActiveAware
    {
        #region Fields

        private bool m_IsActive;
        private readonly IDateTimeCalculator m_DateTimeCalculator;
        private readonly IEventAggregator m_EventService;
        private SubscriptionToken m_GanttChartDataUpdatedSubscriptionToken;

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
                IList<IDependentActivity<int, int>> dependentActivities = ganttChart.DependentActivities;
                IList<ResourceSeriesModel> resourceSeriesSet = ganttChart.ResourceSeriesSet;
                IList<IResourceSchedule<int, int>> resourceSchedules = ganttChart.ResourceSchedules;

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
                    BuildGanttChart(dependentActivities, resourceSeriesSet, resourceSchedules, projectStart, colorFormatLookup);
                }
                else
                {
                    BuildGanttChart(dependentActivities, projectStart, colorFormatLookup);
                }
            }
        }

        private void BuildGanttChart(
            IList<IDependentActivity<int, int>> dependentActivities,
            IList<ResourceSeriesModel> resourceSeriesSet,
            IList<IResourceSchedule<int, int>> resourceSchedules,
            DateTime projectStart,
            SlackColorFormatLookup colorFormatLookup)
        {
            if (dependentActivities == null || resourceSeriesSet == null || resourceSchedules == null)
            {
                return;
            }

            IDictionary<int, IDependentActivity<int, int>> activityLookup = dependentActivities.ToDictionary(x => x.Id);

            int spareResourceCount = 1;
            for (int resourceIndex = 0; resourceIndex < resourceSchedules.Count; resourceIndex++)
            {
                IResourceSchedule<int, int> resourceSchedule = resourceSchedules[resourceIndex];
                IEnumerable<IScheduledActivity<int>> scheduledActivities = resourceSchedule?.ScheduledActivities;

                if (scheduledActivities == null)
                {
                    continue;
                }

                var stringBuilder = new StringBuilder();
                if (resourceSchedule.Resource != null)
                {
                    if (string.IsNullOrWhiteSpace(resourceSchedule.Resource.Name))
                    {
                        stringBuilder.Append($@"Resource {resourceSchedule.Resource.Id}");
                    }
                    else
                    {
                        stringBuilder.Append(resourceSchedule.Resource.Name);
                    }
                }
                else
                {
                    stringBuilder.Append($@"Resource {spareResourceCount}");
                    spareResourceCount++;
                }

                string resourceName = stringBuilder.ToString();
                GanttRowGroup rowGroup = GanttChartAreaCtrl.CreateGanttRowGroup(resourceName);

                foreach (IScheduledActivity<int> scheduledctivity in resourceSchedule.ScheduledActivities)
                {
                    if (activityLookup.TryGetValue(scheduledctivity.Id, out IDependentActivity<int, int> activity))
                    {
                        GanttRow row = GanttChartAreaCtrl.CreateGanttRow(rowGroup, activity.Name);

                        if (activity.EarliestStartTime.HasValue
                            && activity.EarliestFinishTime.HasValue)
                        {
                            GanttChartAreaCtrl.AddGanttTask(
                                row,
                                CreateGanttTask(projectStart, activity, colorFormatLookup));
                        }
                    }
                }
            }
        }

        private void BuildGanttChart(
            IList<IDependentActivity<int, int>> dependentActivities,
            DateTime projectStart,
            SlackColorFormatLookup colorFormatLookup)
        {
            if (dependentActivities == null)
            {
                return;
            }

            foreach (IDependentActivity<int, int> activity in dependentActivities)
            {
                GanttRowGroup rowGroup = GanttChartAreaCtrl.CreateGanttRowGroup();
                GanttRow row = GanttChartAreaCtrl.CreateGanttRow(rowGroup, activity.Name);

                if (activity.EarliestStartTime.HasValue
                    && activity.EarliestFinishTime.HasValue)
                {
                    GanttChartAreaCtrl.AddGanttTask(
                        row,
                        CreateGanttTask(projectStart, activity, colorFormatLookup));
                }
            }
        }

        private GanttTask CreateGanttTask(
            DateTime projectStart,
            IDependentActivity<int, int> activity,
            SlackColorFormatLookup colorFormatLookup)
        {
            Color background = colorFormatLookup?.FindSlackColor(activity.TotalSlack) ?? Colors.DodgerBlue;

            return new GanttTask
            {
                Start = m_DateTimeCalculator.AddDays(projectStart, activity.EarliestStartTime.Value),
                End = m_DateTimeCalculator.AddDays(projectStart, activity.EarliestFinishTime.Value),
                Name = activity.Name,
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
