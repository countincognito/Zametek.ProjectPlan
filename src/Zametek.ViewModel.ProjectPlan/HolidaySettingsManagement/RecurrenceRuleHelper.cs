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











            //var pattern = RecurrencePatternHelper.ToPattern(model);
            var freq = model.Frequency switch
            {
                RecurrenceFrequency.Secondly => "every second",
                RecurrenceFrequency.Minutely => "every minute",
                RecurrenceFrequency.Hourly => "every hour",
                RecurrenceFrequency.Daily => "every day",
                RecurrenceFrequency.Weekly => "every week",
                RecurrenceFrequency.Monthly => "every month",
                RecurrenceFrequency.Yearly => "every year",
                _ => throw new InvalidOperationException("Frequency must be set.")
            };

            if (model.Interval > 1)
                freq = $"every {model.Interval} {FrequencyUnit(model.Frequency)}s";









            sb.Append(freq);




            // Handle Positional Days (BYSETPOS + BYDAY) - e.g., "the 1st Monday"
            if (model.BySetPos.Count > 0 && model.ByDay.Count > 0)
            {
                string posText = JoinWithAnd(model.BySetPos.Select(FormatOrdinal));
                string dayText = JoinWithAnd(model.ByDay.Select(DayName));

                sb.Append($" on the {posText} {dayText}");
            }
            else if(model.ByDay.Count > 0)
            {
                var dayText = JoinWithAnd(model.ByDay.Select(DayName));
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
                ? " in " + JoinWithAnd(model.ByMonth.Select(MonthName))
                : string.Empty;

            sb.Append(byMonthText);











            if (model.WeekStart is not null)
                sb.Append(" with week starting ").Append(DayName(model.WeekStart.Value));

            if (model.Count is not null)
            {

                sb.Append($" for {model.Count.Value} occurrences");
            }
            else if (model.Until is not null)
            {

                sb.Append(" until ").Append(model.Until.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }

        static string FrequencyUnit(RecurrenceFrequency frequency) => frequency switch
        {
            RecurrenceFrequency.Secondly => "second",
            RecurrenceFrequency.Minutely => "minute",
            RecurrenceFrequency.Hourly => "hour",
            RecurrenceFrequency.Daily => "day",
            RecurrenceFrequency.Weekly => "week",
            RecurrenceFrequency.Monthly => "month",
            RecurrenceFrequency.Yearly => "year",
            _ => "occurrence"
        };

        static string DayName(RecurrenceDay day) => day switch
        {
            RecurrenceDay.MO => "Monday",
            RecurrenceDay.TU => "Tuesday",
            RecurrenceDay.WE => "Wednesday",
            RecurrenceDay.TH => "Thursday",
            RecurrenceDay.FR => "Friday",
            RecurrenceDay.SA => "Saturday",
            RecurrenceDay.SU => "Sunday",
            _ => ""
        };

        static string MonthName(int month) =>
            CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month);







        private static string FormatOrdinal(int number)
        {
            if (number <= 0 && number != -1) return number.ToString();
            return number switch
            {
                -1 => "last",
                1 or 21 or 31 => $"{number}st",
                2 or 22 => $"{number}nd",
                3 or 23 => $"{number}rd",
                _ => $"{number}th"
            };
        }




        static string JoinWithAnd(IEnumerable<string> items)
        {
            var list = items.ToList();
            return list.Count switch
            {
                0 => string.Empty,
                1 => list[0],
                2 => $"{list[0]} and {list[1]}",
                _ => $"{string.Join(", ", list.Take(list.Count - 1))} and {list[^1]}"
            };
        }
    }
}