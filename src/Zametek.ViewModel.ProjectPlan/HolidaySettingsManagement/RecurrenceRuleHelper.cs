using System.Globalization;
using System.Text;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public static class RecurrenceRuleHelper
    {
        public static string ToPhrase(RecurrenceRuleModel model)
        {
            var sb = new StringBuilder();
            string frequency = ToFrequencyUnit(model.Frequency, model.Interval);
            sb.Append(frequency);

            // Handle Positional Days (BYSETPOS + BYDAY) - e.g., "the 1st Monday"
            if (model.BySetPos.Count > 0
                && model.ByDay.Count > 0)
            {
                string posText = JoinWithAnd(model.BySetPos.Select(FormatOrdinal));
                string dayText = JoinWithAnd(model.ByDay.Select(ToDayName));

                sb.Append($" on the {posText} {dayText}");
            }
            else if (model.ByDay.Count > 0)
            {
                var dayText = JoinWithAnd(model.ByDay.Select(ToDayName));
                sb.Append(model.Frequency is RecurrenceFrequency.Monthly or RecurrenceFrequency.Yearly
                    ? $" on {dayText}"
                    : $" on {dayText}");
            }
            // Handle Monthly/Yearly Days (BYMONTHDAY) - e.g., "the 15th day"
            else if (model.ByMonthDay.Count > 0)
            {
                List<string> days = model.ByMonthDay.Select(d => FormatOrdinal(d)).ToList();
                string daySuffix = days.Count > 1 ? "days" : "day";
                sb.Append($" on the {JoinWithAnd(days)} {daySuffix}");
            }

            // Handle Months (BYMONTH)
            string byMonthText = model.ByMonth.Count > 0
                ? " in " + JoinWithAnd(model.ByMonth.Select(ToMonthName))
                : string.Empty;

            sb.Append(byMonthText);











            if (model.WeekStart.HasValue)
            {

                sb.Append(" with week starting ").Append(ToDayName(model.WeekStart.Value));
            }

            if (model.Count.HasValue)
            {

                sb.Append($" for {model.Count.Value} occurrences");
            }
            else if (model.Until.HasValue)
            {

                sb.Append(" until ").Append(model.Until.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }





        public static string ToDayName(RecurrenceDay day) => day switch
        {
            RecurrenceDay.MO => Resource.ProjectPlan.Holidays.Holiday_WeekdayMonday,
            RecurrenceDay.TU => Resource.ProjectPlan.Holidays.Holiday_WeekdayTuesday,
            RecurrenceDay.WE => Resource.ProjectPlan.Holidays.Holiday_WeekdayWednesday,
            RecurrenceDay.TH => Resource.ProjectPlan.Holidays.Holiday_WeekdayThursday,
            RecurrenceDay.FR => Resource.ProjectPlan.Holidays.Holiday_WeekdayFriday,
            RecurrenceDay.SA => Resource.ProjectPlan.Holidays.Holiday_WeekdaySaturday,
            RecurrenceDay.SU => Resource.ProjectPlan.Holidays.Holiday_WeekdaySunday,
            _ => throw new ArgumentOutOfRangeException(nameof(day), day, null)
        };

        public static string ToMonthName(int month) =>
            CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month);

        private static string ToFrequencyUnit(
            RecurrenceFrequency recurrenceFrequency,
            int interval)
        {
            var sb = new StringBuilder();

            if (interval > 1)
            {
                string frequency = recurrenceFrequency switch
                {
                    RecurrenceFrequency.Secondly => Resource.ProjectPlan.Holidays.Holiday_Seconds,
                    RecurrenceFrequency.Minutely => Resource.ProjectPlan.Holidays.Holiday_Minutes,
                    RecurrenceFrequency.Hourly => Resource.ProjectPlan.Holidays.Holiday_Hours,
                    RecurrenceFrequency.Daily => Resource.ProjectPlan.Holidays.Holiday_Days,
                    RecurrenceFrequency.Weekly => Resource.ProjectPlan.Holidays.Holiday_Weeks,
                    RecurrenceFrequency.Monthly => Resource.ProjectPlan.Holidays.Holiday_Months,
                    RecurrenceFrequency.Yearly => Resource.ProjectPlan.Holidays.Holiday_Years,
                    _ => throw new ArgumentOutOfRangeException(nameof(recurrenceFrequency), recurrenceFrequency, null)
                };
                sb.Append(string.Format(Resource.ProjectPlan.Holidays.Holiday_FrequencyMultiple, interval, frequency));
            }
            else
            {
                string frequency = recurrenceFrequency switch
                {
                    RecurrenceFrequency.Secondly => Resource.ProjectPlan.Holidays.Holiday_Second,
                    RecurrenceFrequency.Minutely => Resource.ProjectPlan.Holidays.Holiday_Minute,
                    RecurrenceFrequency.Hourly => Resource.ProjectPlan.Holidays.Holiday_Hour,
                    RecurrenceFrequency.Daily => Resource.ProjectPlan.Holidays.Holiday_Day,
                    RecurrenceFrequency.Weekly => Resource.ProjectPlan.Holidays.Holiday_Week,
                    RecurrenceFrequency.Monthly => Resource.ProjectPlan.Holidays.Holiday_Month,
                    RecurrenceFrequency.Yearly => Resource.ProjectPlan.Holidays.Holiday_Year,
                    _ => throw new ArgumentOutOfRangeException(nameof(recurrenceFrequency), recurrenceFrequency, null)
                };
                sb.Append(string.Format(Resource.ProjectPlan.Holidays.Holiday_FrequencySingle, frequency));
            }

            return sb.ToString();
        }

        private static string FormatOrdinal(int number)
        {
            if (number <= 0
                && number != -1)
            {
                throw new ArgumentOutOfRangeException(nameof(number), number, Resource.ProjectPlan.Messages.Message_MustBeMinusOneOrGreaterThanZero);
            }
            return number switch
            {
                -1 => Resource.ProjectPlan.Holidays.Holiday_FormatOrdinalMinus1,
                1 or 21 or 31 => string.Format(Resource.ProjectPlan.Holidays.Holiday_FormatOrdinal1, number),
                2 or 22 => string.Format(Resource.ProjectPlan.Holidays.Holiday_FormatOrdinal2, number),
                3 or 23 => string.Format(Resource.ProjectPlan.Holidays.Holiday_FormatOrdinal3, number),
                _ => string.Format(Resource.ProjectPlan.Holidays.Holiday_FormatOrdinalOther, number)
            };
        }

        private static string JoinWithAnd(IEnumerable<string> items)
        {
            List<string> list = [.. items];

            return list.Count switch
            {
                0 => string.Empty,
                1 => list[0],
                2 => string.Format(Resource.ProjectPlan.Holidays.Holiday_JoinWithAnd, list[0], list[1]),
                _ => string.Format(Resource.ProjectPlan.Holidays.Holiday_JoinWithAnd, string.Join(", ", list.Take(list.Count - 1)), list[^1])
            };
        }
    }
}