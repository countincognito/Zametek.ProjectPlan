using FluentDateTimeOffset;
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

        private readonly object m_Lock;

        private static readonly string s_DateFormat = DateTimeFormatInfo.CurrentInfo.ShortDatePattern;

        private static readonly string s_DateTimeFormat = $@"{s_DateFormat} {DateTimeFormatInfo.CurrentInfo.LongTimePattern}";

        private static readonly string s_DateTimeOffsetFormat = s_DateTimeFormat + (DateTimeFormatInfo.CurrentInfo.LongTimePattern.Contains('z') ? string.Empty : " zzz");

        #endregion

        #region Ctors

        public DateTimeCalculator()
        {
            m_Lock = new object();
            m_AddDaysFunc = AddAllDays;
            m_CountDaysFunc = CountAllDays;

            m_DisplayEarliestStartDateFunc = DisplayDefaultEarliestStartDate;
            m_DisplayLatestStartDateFunc = DisplayDefaultLatestStartDate;
            m_DisplayFinishDateFunc = DisplayDefaultFinishDate;

            m_MaximumLatestFinishDateInFunc = DefaultMaximumLatestFinishDateIn;
            m_MaximumLatestFinishDateOutFunc = DefaultMaximumLatestFinishDateOut;

            CalculatorMode = DateTimeCalculatorMode.AllDays;
            DisplayMode = DateTimeDisplayMode.Default;
        }

        #endregion

        #region Properties

        public static string DateFormat => s_DateFormat;

        public static string DateTimeFormat => s_DateTimeFormat;

        public static string DateTimeOffsetFormat => s_DateTimeOffsetFormat;

        #endregion

        #region Private Methods

        private static DateTimeOffset AddAllDays(
            DateTimeOffset current,
            int days)
        {
            return current.AddDays(days);
        }

        private static DateTimeOffset AddBusinessDays(
            DateTimeOffset current,
            int days)
        {
            return current.AddBusinessDays(days);
        }

        private static int CountAllDays(
            DateTimeOffset current,
            DateTimeOffset toCompareWith)
        {
            if (current.IsAfter(toCompareWith))
            {
                return -CountAllDays(toCompareWith, current);
            }
            return Convert.ToInt32((toCompareWith - current).TotalDays);
        }

        private static int CountBusinessDays(
            DateTimeOffset current,
            DateTimeOffset toCompareWith)
        {
            if (current.IsAfter(toCompareWith))
            {
                return -CountBusinessDays(toCompareWith, current);
            }
            int count = 0;
            while (current.IsBefore(toCompareWith))
            {
                current = current.AddDays(1);
                if (current.DayOfWeek != DayOfWeek.Saturday
                    && current.DayOfWeek != DayOfWeek.Sunday)
                {
                    count++;
                }
            }
            return count;
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

        #endregion

        #region IDateTimeCalculator Members

        private DateTimeCalculatorMode m_CalculatorMode;
        public DateTimeCalculatorMode CalculatorMode
        {
            get => m_CalculatorMode;
            set
            {
                lock (m_Lock)
                {
                    DateTimeCalculatorMode calculatorMode = value;

                    switch (calculatorMode)
                    {
                        case DateTimeCalculatorMode.AllDays:
                            DaysPerWeek = 7;
                            m_AddDaysFunc = AddAllDays;
                            m_CountDaysFunc = CountAllDays;
                            break;
                        case DateTimeCalculatorMode.BusinessDays:
                            DaysPerWeek = 5;
                            m_AddDaysFunc = AddBusinessDays;
                            m_CountDaysFunc = CountBusinessDays;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(
                                nameof(CalculatorMode),
                                @$"{Resource.ProjectPlan.Messages.Message_UnknownDateTimeCalculatorMode} {calculatorMode}");
                    }

                    this.RaiseAndSetIfChanged(ref m_CalculatorMode, calculatorMode);
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

                    switch (displayMode)
                    {
                        case DateTimeDisplayMode.Default:
                            m_DisplayEarliestStartDateFunc = DisplayDefaultEarliestStartDate;
                            m_DisplayLatestStartDateFunc = DisplayDefaultLatestStartDate;
                            m_DisplayFinishDateFunc = DisplayDefaultFinishDate;
                            m_MaximumLatestFinishDateInFunc = DefaultMaximumLatestFinishDateIn;
                            m_MaximumLatestFinishDateOutFunc = DefaultMaximumLatestFinishDateOut;
                            break;
                        case DateTimeDisplayMode.Classic:
                            m_DisplayEarliestStartDateFunc = DisplayClassicEarliestStartDate;
                            m_DisplayLatestStartDateFunc = DisplayClassicLatestStartDate;
                            m_DisplayFinishDateFunc = DisplayClassicFinishDate;
                            m_MaximumLatestFinishDateInFunc = ClassicMaximumLatestFinishDateIn;
                            m_MaximumLatestFinishDateOutFunc = ClassicMaximumLatestFinishDateOut;
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

        private int m_DaysPerWeek;
        public int DaysPerWeek
        {
            get => m_DaysPerWeek;
            private set
            {
                lock (m_Lock) this.RaiseAndSetIfChanged(ref m_DaysPerWeek, value);
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
