using System.Globalization;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public static class ChartHelper
    {

        private static readonly DateTime s_TimeOrigin = new(1899, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        public static string FormatStartScheduleOutput(
            int days,
            bool showDates,
            DateTimeOffset projectStart,
            int duration,
            IDateTimeCalculator dateTimeCalculator)
        {
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            if (showDates)
            {
                return StartDateTimeOffset(
                        days,
                        projectStart,
                        duration,
                        dateTimeCalculator)
                    .ToString(DateTimeCalculator.DateFormat);
            }
            return days.ToString(CultureInfo.InvariantCulture);
        }

        public static string FormatFinishScheduleOutput(
            int days,
            bool showDates,
            DateTimeOffset projectStart,
            int duration,
            IDateTimeCalculator dateTimeCalculator)
        {
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            if (showDates)
            {
                return FinishDateTimeOffset(
                        days,
                        projectStart,
                        duration,
                        dateTimeCalculator)
                    .ToString(DateTimeCalculator.DateFormat);
            }
            return days.ToString(CultureInfo.InvariantCulture);
        }

        public static double CalculateChartStartTimeXValue(
            int days,
            bool showDates,
            DateTimeOffset projectStart,
            IDateTimeCalculator dateTimeCalculator)
        {
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            double output = days;
            if (showDates)
            {
                output = ToDouble(
                    dateTimeCalculator.AddDays(
                        projectStart.Date,
                        days)
                    .Date);
            }
            return output;
        }

        public static double CalculateChartFinishTimeXValue(
            int days,
            bool showDates,
            DateTimeOffset projectStart,
            IDateTimeCalculator dateTimeCalculator)
        {
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            double output = days;
            if (showDates)
            {
                output = ToDouble(
                    dateTimeCalculator.AddDays(
                        projectStart.Date,
                        days)
                    .Date);
            }
            return output;
        }

        private static DateTimeOffset StartDateTimeOffset(
            int days,
            DateTimeOffset projectStart,
            int duration,
            IDateTimeCalculator dateTimeCalculator)
        {
            return dateTimeCalculator.DisplayEarliestStartDate(
                projectStart.Date,
                dateTimeCalculator.AddDays(
                    projectStart.Date,
                    days),
                duration);
        }

        private static DateTimeOffset FinishDateTimeOffset(
            int days,
            DateTimeOffset projectStart,
            int duration,
            IDateTimeCalculator dateTimeCalculator)
        {
            DateTimeOffset start = StartDateTimeOffset(
                days,
                projectStart,
                duration,
                dateTimeCalculator);

            return dateTimeCalculator.DisplayFinishDate(
                start,
                dateTimeCalculator.AddDays(
                    projectStart.Date,
                    days),
                duration);
        }

        private static double ToDouble(DateTime value)
        {
            var span = value - s_TimeOrigin;
            return span.TotalDays + 1;
        }
    }
}
