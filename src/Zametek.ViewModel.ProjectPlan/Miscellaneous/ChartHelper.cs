using OxyPlot.Axes;
using System.Globalization;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public static class ChartHelper
    {
        public static string FormatScheduleOutput(
            int days,
            bool showDates,
            DateTimeOffset projectStart,
            IDateTimeCalculator dateTimeCalculator)
        {
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            if (showDates)
            {
                return dateTimeCalculator
                    .AddDays(projectStart, days)
                    .ToString(DateTimeCalculator.DateFormat);
            }
            return days.ToString(CultureInfo.InvariantCulture);
        }

        public static double CalculateChartTimeXValue(
            int days,
            bool showDates,
            DateTimeOffset projectStart,
            IDateTimeCalculator dateTimeCalculator)
        {
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            double output = days;
            if (showDates)
            {
                output = Axis.ToDouble(dateTimeCalculator.AddDays(projectStart, days).Date);
            }
            return output;
        }
    }
}
