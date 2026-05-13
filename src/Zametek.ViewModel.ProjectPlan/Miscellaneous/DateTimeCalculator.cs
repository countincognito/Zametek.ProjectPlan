using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using ReactiveUI;
using System.Globalization;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class DateTimeCalculator
        : ViewModelBase, IDateTimeCalculator
    {
        #region Fields

        private readonly Lock m_Lock;
        private readonly TimeProvider m_TimeProvider;

        private static readonly string s_DateFormat = DateTimeFormatInfo.CurrentInfo.ShortDatePattern;

        private static readonly string s_DateTimeFormat = $@"{s_DateFormat} {DateTimeFormatInfo.CurrentInfo.LongTimePattern}";

        private static readonly string s_DateTimeOffsetFormat = s_DateTimeFormat + (DateTimeFormatInfo.CurrentInfo.LongTimePattern.Contains('z') ? string.Empty : " zzz");

        private static readonly HolidayModel s_WeekendCalendarEvent = new()
        {
            Id = 1,
            RecurrencePattern = "FREQ=WEEKLY;BYDAY=SA,SU",
        };

        private readonly List<HolidayModel> m_CustomCalendarNonWorkingCalendarEvents;

        private readonly HashSet<DateOnly> m_NonWorkingDays;
        private const int c_NonWorkingDaysSearchBuffer = 30;

        #endregion

        #region Ctors

        public DateTimeCalculator(TimeProvider timeProvider)
        {
            m_Lock = new();
            m_TimeProvider = timeProvider;
            m_AddDaysFunc = AddAllDays;
            m_CountDaysFunc = CountAllDays;
            m_CustomCalendarNonWorkingCalendarEvents = [];
            m_NonWorkingDays = [];

            m_DisplayEarliestStartDateFunc = DisplayDefaultEarliestStartDate;
            m_DisplayLatestStartDateFunc = DisplayDefaultLatestStartDate;
            m_DisplayFinishDateFunc = DisplayDefaultFinishDate;

            m_MaximumLatestFinishDateInFunc = DefaultMaximumLatestFinishDateIn;
            m_MaximumLatestFinishDateOutFunc = DefaultMaximumLatestFinishDateOut;

            NonWorkingDayMode = NonWorkingDayMode.None;
            DisplayMode = DateTimeDisplayMode.Default;
        }

        #endregion

        #region Properties

        public static string DateFormat => s_DateFormat;

        public static string DateTimeFormat => s_DateTimeFormat;

        public static string DateTimeOffsetFormat => s_DateTimeOffsetFormat;

        public List<HolidayModel> NonWorkingDayCalendarEvents => [.. m_CustomCalendarNonWorkingCalendarEvents];

        #endregion

        #region Private Methods

        private DateTimeOffset AddAllDays(
            DateTimeOffset current,
            int days)
        {
            lock (m_Lock)
            {
                return AddNonWorkingDays(current, days, []);
            }
        }

        private int CountAllDays(
            DateTimeOffset current,
            DateTimeOffset toCompareWith)
        {
            lock (m_Lock)
            {
                return CountNonWorkingDays(current, toCompareWith, []);
            }
        }

        private DateTimeOffset AddBusinessDays(
            DateTimeOffset current,
            int days)
        {
            lock (m_Lock)
            {
                return AddNonWorkingDays(current, days, [s_WeekendCalendarEvent]);
            }
        }

        private int CountBusinessDays(
            DateTimeOffset current,
            DateTimeOffset toCompareWith)
        {
            lock (m_Lock)
            {
                return CountNonWorkingDays(current, toCompareWith, [s_WeekendCalendarEvent]);
            }
        }

        private DateTimeOffset AddCustomCalendarDays(
            DateTimeOffset current,
            int days)
        {
            lock (m_Lock)
            {
                return AddNonWorkingDays(current, days, m_CustomCalendarNonWorkingCalendarEvents);
            }
        }

        private int CountCustomCalendarDays(
            DateTimeOffset current,
            DateTimeOffset toCompareWith)
        {
            lock (m_Lock)
            {
                return CountNonWorkingDays(current, toCompareWith, m_CustomCalendarNonWorkingCalendarEvents);
            }
        }

        private DateTimeOffset AddNonWorkingDays(
            DateTimeOffset current,
            int days,
            List<HolidayModel> nonWorkingDayCalendarEvents)
        {
            lock (m_Lock)
            {
                AppendNonWorkingDays(current.Date, days + c_NonWorkingDaysSearchBuffer, nonWorkingDayCalendarEvents);

                int sign = Math.Sign(days);
                int unsignedDays = Math.Abs(days);

                int count = 0;
                while (count < unsignedDays)
                {
                    current = current.AddDays(sign);

                    // If we have moved out of the range of already calculated non-working days,
                    // then calculate more non-working days.
                    if (current.IsAfterOrOn(NonWorkingDaysFinish))
                    {
                        AppendNonWorkingDays(
                            current.Date,
                            c_NonWorkingDaysSearchBuffer,
                            nonWorkingDayCalendarEvents);
                    }

                    if (!m_NonWorkingDays.Contains(DateOnly.FromDateTime(current.Date)))
                    {
                        count += 1;
                    }
                }
                return current;
            }
        }

        private int CountNonWorkingDays(
            DateTimeOffset current,
            DateTimeOffset toCompareWith,
            List<HolidayModel> nonWorkingDayCalendarEvents)
        {
            lock (m_Lock)
            {
                if (current.IsAfter(toCompareWith))
                {
                    return -CountNonWorkingDays(toCompareWith, current, nonWorkingDayCalendarEvents);
                }

                AppendNonWorkingDays(
                    current.Date,
                    toCompareWith.Date,
                    nonWorkingDayCalendarEvents: nonWorkingDayCalendarEvents);

                int count = 0;
                while (current.IsBefore(toCompareWith))
                {
                    current = current.AddDays(1);

                    if (!m_NonWorkingDays.Contains(DateOnly.FromDateTime(current.Date)))
                    {
                        count += 1;
                    }
                }
                return count;
            }
        }

        private void ClearNonWorkingDays()
        {
            lock (m_Lock)
            {
                m_NonWorkingDays.Clear();
                NonWorkingDaysStart = ProjectStart;
                NonWorkingDaysFinish = ProjectStart;
            }
        }

        private void AppendNonWorkingDays(
            DateTime startDateTime,
            DateTime finishDateTime,
            List<HolidayModel> nonWorkingDayCalendarEvents)
        {
            lock (m_Lock)
            {
                if (startDateTime.IsAfterOrOn(finishDateTime))
                {
                    (startDateTime, finishDateTime) = (finishDateTime, startDateTime);
                }

                if (startDateTime.IsAfterOrOn(NonWorkingDaysStart.Date)
                    && finishDateTime.IsBeforeOrOn(NonWorkingDaysFinish.Date))
                {
                    return;
                }

                DateTime bufferedStartDateTime = startDateTime.AddDays(-1).Date;
                DateTime bufferedFinishDateTime = finishDateTime.AddDays(1).Date;

                foreach (HolidayModel nonWorkingDayCalendarEvent in nonWorkingDayCalendarEvents)
                {
                    HashSet<DateOnly> newNonWorkingDays = GetNonWorkingDaysFromCalendarEvents(
                        bufferedStartDateTime,
                        bufferedFinishDateTime,
                        ProjectStart,
                        nonWorkingDayCalendarEvent);

                    m_NonWorkingDays.UnionWith(newNonWorkingDays);
                }

                if (startDateTime.IsBeforeOrOn(NonWorkingDaysStart.Date))
                {
                    NonWorkingDaysStart = GetLocal(startDateTime);
                }
                if (finishDateTime.IsAfterOrOn(NonWorkingDaysFinish.Date))
                {
                    NonWorkingDaysFinish = GetLocal(finishDateTime);
                }
            }
        }

        private void AppendNonWorkingDays(
            DateTime startDateTime,
            int days,
            List<HolidayModel> nonWorkingDayCalendarEvents)
        {
            lock (m_Lock)
            {
                DateTime finishDateTime = startDateTime.AddDays(days).Date;

                AppendNonWorkingDays(
                    startDateTime,
                    finishDateTime,
                    nonWorkingDayCalendarEvents);
            }
        }

        private static HashSet<DateOnly> GetNonWorkingDaysFromCalendarEvents(
            DateTime searchStartDateTime,
            DateTime searchFinishDateTime,
            DateTimeOffset projectStart,
            HolidayModel nonWorkingDayCalendarEvent)
        {
            if (searchStartDateTime.IsAfterOrOn(searchFinishDateTime))
            {
                (searchStartDateTime, searchFinishDateTime) = (searchFinishDateTime, searchStartDateTime);
            }

            // CalDateTime only works with DateTimeKind.Unspecified or DateTimeKind.Utc,
            // so we need to convert our input DateTimes to one of those kinds.
            var searchStartCalDateTime = new CalDateTime(DateTime.SpecifyKind(searchStartDateTime, DateTimeKind.Unspecified));
            var searchEndCalDateTime = new CalDateTime(DateTime.SpecifyKind(searchFinishDateTime, DateTimeKind.Unspecified));

            CalDateTime? startDateTime = new(DateTime.SpecifyKind(projectStart.DateTime, DateTimeKind.Unspecified));

            if (searchStartCalDateTime.Date.IsBeforeOrOn(startDateTime.Date))
            {
                startDateTime = searchStartCalDateTime;
            }

            if (nonWorkingDayCalendarEvent.StartDateTime.HasValue)
            {
                startDateTime = new CalDateTime(DateTime.SpecifyKind(nonWorkingDayCalendarEvent.StartDateTime.Value.DateTime, DateTimeKind.Unspecified));
            }

            var nonWorkingDaysEvent = new CalendarEvent
            {
                Start = startDateTime,
                RecurrenceRules = [new RecurrencePattern(nonWorkingDayCalendarEvent.RecurrencePattern)],
            };

            List<Occurrence> occurrences = [.. nonWorkingDaysEvent
                .GetOccurrences(searchStartCalDateTime)
                .TakeWhileBefore(searchEndCalDateTime)];

            HashSet<DateOnly> nonWorkingDays = [.. occurrences.Select(x => x.Period.StartTime.Date)];
            return nonWorkingDays;
        }

        private static DateTimeOffset DisplayDefaultEarliestStartDate(
            DateTimeOffset projectStart,
            DateTimeOffset earliestStart,
            int duration)
        {
            return earliestStart;
        }

        private DateTimeOffset DisplayClassicEarliestStartDate(
            DateTimeOffset projectStart,
            DateTimeOffset earliestStart,
            int duration)
        {
            lock (m_Lock)
            {
                if (duration == 0
                    && CountDays(projectStart, earliestStart) > 0)
                {
                    return AddDays(earliestStart, -1);
                }
                return earliestStart;
            }
        }

        private static DateTimeOffset DisplayDefaultLatestStartDate(
            DateTimeOffset earliestStart,
            DateTimeOffset latestStart,
            int duration)
        {
            return latestStart;
        }

        private DateTimeOffset DisplayClassicLatestStartDate(
            DateTimeOffset earliestStart,
            DateTimeOffset latestStart,
            int duration)
        {
            lock (m_Lock)
            {
                if (duration == 0
                    && CountDays(earliestStart, latestStart) > 0)
                {
                    return AddDays(latestStart, -1);
                }
                return latestStart;
            }
        }

        private static DateTimeOffset DisplayDefaultFinishDate(
            DateTimeOffset start,
            DateTimeOffset finish,
            int duration)
        {
            return finish;
        }

        private DateTimeOffset DisplayClassicFinishDate(
            DateTimeOffset start,
            DateTimeOffset finish,
            int duration)
        {
            lock (m_Lock)
            {
                if (duration > 0
                    || CountDays(start, finish) > 0)
                {
                    return AddDays(finish, -1);
                }
                return finish;
            }
        }

        private static DateTimeOffset DefaultMaximumLatestFinishDateIn(
            DateTimeOffset start,
            DateTimeOffset maxLatestFinish,
            int duration)
        {
            return maxLatestFinish;
        }

        private DateTimeOffset ClassicMaximumLatestFinishDateIn(
            DateTimeOffset start,
            DateTimeOffset maxLatestFinish,
            int duration)
        {
            lock (m_Lock)
            {
                if (duration > 0
                    || CountDays(start, maxLatestFinish) > 0)
                {
                    return AddDays(maxLatestFinish, 1);
                }
                return maxLatestFinish;
            }
        }

        private static DateTimeOffset DefaultMaximumLatestFinishDateOut(
            DateTimeOffset start,
            DateTimeOffset maxLatestFinish,
            int duration)
        {
            return maxLatestFinish;
        }

        private DateTimeOffset ClassicMaximumLatestFinishDateOut(
            DateTimeOffset start,
            DateTimeOffset maxLatestFinish,
            int duration)
        {
            lock (m_Lock)
            {
                if (duration > 0
                    || CountDays(start, maxLatestFinish) > 0)
                {
                    return AddDays(maxLatestFinish, -1);
                }
                return maxLatestFinish;
            }
        }

        private int? CalculateTime(int? input)
        {
            lock (m_Lock)
            {
                int? result = input;
                if (result.HasValue && result < 0)
                {
                    result = 0;
                }
                return result;
            }
        }

        private int? CalculateTime(
            DateTimeOffset projectStart,
            DateTimeOffset? input)
        {
            lock (m_Lock)
            {
                int? result = null;
                if (input.HasValue)
                {
                    result = CountDays(projectStart, input.GetValueOrDefault());
                    result = CalculateTime(result);
                }
                return result;
            }
        }

        private DateTimeOffset? CalculateDateTime(
            DateTimeOffset projectStart,
            DateTimeOffset? input)
        {
            lock (m_Lock)
            {
                DateTimeOffset? result = input;
                if (result.HasValue)
                {
                    if (result < projectStart)
                    {
                        result = GetLocal(projectStart.DateTime);
                    }
                    result = new DateTimeOffset(result.GetValueOrDefault().Date + projectStart.TimeOfDay, projectStart.Offset);
                }
                return result;
            }
        }

        private DateTimeOffset? CalculateDateTime(
            DateTimeOffset projectStart,
            int? input)
        {
            lock (m_Lock)
            {
                DateTimeOffset? result = null;
                if (input.HasValue)
                {
                    result = AddDays(projectStart, input.GetValueOrDefault());
                    result = CalculateDateTime(projectStart, result);
                }
                return result;
            }
        }

        #endregion

        #region IDateTimeCalculator Members

        private NonWorkingDayMode m_NonWorkingDayMode;
        public NonWorkingDayMode NonWorkingDayMode
        {
            get => m_NonWorkingDayMode;
            set
            {
                lock (m_Lock)
                {
                    NonWorkingDayMode nonWorkingDayMode = value;
                    ClearNonWorkingDays();

                    switch (nonWorkingDayMode)
                    {
                        case NonWorkingDayMode.None:
                            {
                                m_AddDaysFunc = AddAllDays;
                                m_CountDaysFunc = CountAllDays;
                            }
                            break;
                        case NonWorkingDayMode.Weekends:
                            {
                                m_AddDaysFunc = AddBusinessDays;
                                m_CountDaysFunc = CountBusinessDays;
                            }
                            break;
                        case NonWorkingDayMode.CustomCalendar:
                            {
                                m_AddDaysFunc = AddCustomCalendarDays;
                                m_CountDaysFunc = CountCustomCalendarDays;
                            }
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(
                                nameof(NonWorkingDayMode),
                                @$"{Resource.ProjectPlan.Messages.Message_UnknownNonWorkingDayMode} {nonWorkingDayMode}");
                    }

                    this.RaiseAndSetIfChanged(ref m_NonWorkingDayMode, nonWorkingDayMode);
                }
            }
        }

        private DateTimeDisplayMode m_DisplayMode;
        public DateTimeDisplayMode DisplayMode
        {
            get => m_DisplayMode;
            set
            {
                lock (m_Lock)
                {
                    DateTimeDisplayMode displayMode = value;
                    ClearNonWorkingDays();

                    switch (displayMode)
                    {
                        case DateTimeDisplayMode.Default:
                            {
                                m_DisplayEarliestStartDateFunc = DisplayDefaultEarliestStartDate;
                                m_DisplayLatestStartDateFunc = DisplayDefaultLatestStartDate;
                                m_DisplayFinishDateFunc = DisplayDefaultFinishDate;
                                m_MaximumLatestFinishDateInFunc = DefaultMaximumLatestFinishDateIn;
                                m_MaximumLatestFinishDateOutFunc = DefaultMaximumLatestFinishDateOut;
                            }
                            break;
                        case DateTimeDisplayMode.Classic:
                            {
                                m_DisplayEarliestStartDateFunc = DisplayClassicEarliestStartDate;
                                m_DisplayLatestStartDateFunc = DisplayClassicLatestStartDate;
                                m_DisplayFinishDateFunc = DisplayClassicFinishDate;
                                m_MaximumLatestFinishDateInFunc = ClassicMaximumLatestFinishDateIn;
                                m_MaximumLatestFinishDateOutFunc = ClassicMaximumLatestFinishDateOut;
                            }
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(
                                nameof(DisplayMode),
                                @$"{Resource.ProjectPlan.Messages.Message_UnknownDateTimeDisplayMode} {displayMode}");
                    }

                    this.RaiseAndSetIfChanged(ref m_DisplayMode, displayMode);
                }
            }
        }

        private DateTimeOffset m_ProjectStart;
        public DateTimeOffset ProjectStart
        {
            get => m_ProjectStart;
            set
            {
                lock (m_Lock)
                {
                    // Convert to local now using TimeProvider as we do not know
                    // if the input is provided as just a datetime from XAML.
                    m_ProjectStart = GetLocal(value.DateTime);
                    ClearNonWorkingDays();
                    this.RaiseAndSetIfChanged(ref m_ProjectStart, value);
                }
            }
        }

        private DateTimeOffset m_NonWorkingDaysStart;
        public DateTimeOffset NonWorkingDaysStart
        {
            get => m_NonWorkingDaysStart;
            private set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_NonWorkingDaysStart, value);
                }
            }
        }
        private DateTimeOffset m_NonWorkingDaysFinish;
        public DateTimeOffset NonWorkingDaysFinish
        {
            get => m_NonWorkingDaysFinish;
            private set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_NonWorkingDaysFinish, value);
                }
            }
        }

        public void SetNonWorkingDayCalendarEvents(List<HolidayModel> nonWorkingDayCalendarEvents)
        {
            lock (m_Lock)
            {
                m_CustomCalendarNonWorkingCalendarEvents.Clear();

                foreach (HolidayModel holiday in nonWorkingDayCalendarEvents)
                {
                    m_CustomCalendarNonWorkingCalendarEvents.Add(holiday);
                }

                ClearNonWorkingDays();
            }
        }

        public DateTimeOffset GetLocalNow()
        {
            return m_TimeProvider.GetLocalNow();
        }

        public DateTimeOffset GetLocal(DateTime dateTime)
        {
            var localDateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Local);
            TimeSpan offset = m_TimeProvider.LocalTimeZone.GetUtcOffset(localDateTime);
            return new(localDateTime, offset);
        }

        public (int?, DateTimeOffset?) CalculateTimeAndDateTime(
            DateTimeOffset projectStart,
            int? input)
        {
            lock (m_Lock)
            {
                // Calculate integer and DateTimeOffset values (double pass).
                int? intValue = CalculateTime(input);
                DateTimeOffset? dateTimeOffsetValue = CalculateDateTime(projectStart, intValue);

                dateTimeOffsetValue = CalculateDateTime(projectStart, dateTimeOffsetValue);
                intValue = CalculateTime(projectStart, dateTimeOffsetValue);

                return (intValue, dateTimeOffsetValue);
            }
        }

        public (int?, DateTimeOffset?) CalculateTimeAndDateTime(
            DateTimeOffset projectStart,
            DateTimeOffset? input)
        {
            lock (m_Lock)
            {
                // Calculate integer and DateTimeOffset values (double pass).
                DateTimeOffset? dateTimeOffsetValue = CalculateDateTime(projectStart, input);
                int? intValue = CalculateTime(projectStart, dateTimeOffsetValue);

                intValue = CalculateTime(intValue);
                dateTimeOffsetValue = CalculateDateTime(projectStart, intValue);

                return (intValue, dateTimeOffsetValue);
            }
        }

        private Func<DateTimeOffset, int, DateTimeOffset> m_AddDaysFunc;

        public DateTimeOffset AddDays(DateTimeOffset startDateTime, int days)
        {
            lock (m_Lock)
            {
                return m_AddDaysFunc(startDateTime, days);
            }
        }

        private Func<DateTimeOffset, DateTimeOffset, int> m_CountDaysFunc;

        public int CountDays(DateTimeOffset current, DateTimeOffset toCompareWith)
        {
            lock (m_Lock)
            {
                return m_CountDaysFunc(current, toCompareWith);
            }
        }

        public bool IsNonWorkingDay(DateTimeOffset date)
        {
            // CountDays from (date - 1 calendar day) to date returns 0 when date is non-working.
            return CountDays(date.AddDays(-1), date) == 0;
        }

        private Func<DateTimeOffset, DateTimeOffset, int, DateTimeOffset> m_DisplayEarliestStartDateFunc;
        public DateTimeOffset DisplayEarliestStartDate(
            DateTimeOffset projectStart,
            DateTimeOffset earliestStart,
            int duration)
        {
            lock (m_Lock)
            {
                return m_DisplayEarliestStartDateFunc(projectStart, earliestStart, duration);
            }
        }

        private Func<DateTimeOffset, DateTimeOffset, int, DateTimeOffset> m_DisplayLatestStartDateFunc;
        public DateTimeOffset DisplayLatestStartDate(
            DateTimeOffset earliestStart,
            DateTimeOffset latestStart,
            int duration)
        {
            lock (m_Lock)
            {
                return m_DisplayLatestStartDateFunc(earliestStart, latestStart, duration);
            }
        }

        private Func<DateTimeOffset, DateTimeOffset, int, DateTimeOffset> m_DisplayFinishDateFunc;
        public DateTimeOffset DisplayFinishDate(
            DateTimeOffset start,
            DateTimeOffset finish,
            int duration)
        {
            lock (m_Lock)
            {
                return m_DisplayFinishDateFunc(start, finish, duration);
            }
        }

        private Func<DateTimeOffset, DateTimeOffset, int, DateTimeOffset> m_MaximumLatestFinishDateInFunc;
        public DateTimeOffset MaximumLatestFinishDateIn(
            DateTimeOffset start,
            DateTimeOffset maxLatestFinish,
            int duration)
        {
            lock (m_Lock)
            {
                return m_MaximumLatestFinishDateInFunc(start, maxLatestFinish, duration);
            }
        }

        private Func<DateTimeOffset, DateTimeOffset, int, DateTimeOffset> m_MaximumLatestFinishDateOutFunc;
        public DateTimeOffset MaximumLatestFinishDateOut(
            DateTimeOffset start,
            DateTimeOffset maxLatestFinish,
            int duration)
        {
            lock (m_Lock)
            {
                return m_MaximumLatestFinishDateOutFunc(start, maxLatestFinish, duration);
            }
        }

        #endregion
    }
}
