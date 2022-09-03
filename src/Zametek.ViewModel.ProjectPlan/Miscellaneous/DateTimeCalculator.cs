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
            Mode = DateTimeCalculatorMode.AllDays;
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

        #endregion

        #region IDateTimeCalculator Members

        private DateTimeCalculatorMode m_Mode;
        public DateTimeCalculatorMode Mode
        {
            get => m_Mode;
            set
            {
                lock (m_Lock)
                {
                    DateTimeCalculatorMode mode = value;

                    switch (mode)
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
                                nameof(Mode),
                                @$"{Resource.ProjectPlan.Messages.Message_UnknownDateTimeCalculatorMode} {m_Mode}");
                    }

                    this.RaiseAndSetIfChanged(ref m_Mode, mode);
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

        #endregion
    }
}
