using System.Globalization;
using System.Text;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public static class RecurrenceRuleHelper
    {
        private static readonly HashSet<RecurrenceDay> s_EveryWeekDay = [.. Enum.GetValues<RecurrenceDay>().Cast<RecurrenceDay>()];
        private static readonly HashSet<int> s_EveryMonthDay = [.. Enumerable.Range(1, 31)];

        public static bool IsRecurrenceRuleEveryDay(RecurrenceRuleModel model)
        {

            if (model.Until is null
                && model.Count is null)
            {
                if (model.Frequency == RecurrenceFrequency.Daily
                    && model.Interval <= 1)
                {
                    return true;
                }

                //bool isEveryWeekDay = !model.ByDay.Except(s_EveryWeekDay).Any();
                bool isEveryWeekDay = !s_EveryWeekDay.Except(model.ByDay).Any();

                if (model.Frequency == RecurrenceFrequency.Weekly
                    && isEveryWeekDay)
                {
                    return true;
                }

                //bool isEveryMonthDay = !model.ByMonthDay.Except(s_EveryMonthDay).Any();
                bool isEveryMonthDay = !s_EveryMonthDay.Except(model.ByMonthDay).Any();

                if (model.Frequency == RecurrenceFrequency.Monthly
                    && isEveryMonthDay)
                {
                    return true;
                }
            }

            return false;
        }

        public static string ToPhrase(RecurrenceRuleModel model)
        {
            if (model.Frequency == RecurrenceFrequency.None)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            string frequency = ToFrequencyUnit(model.Frequency, model.Interval);
            sb.Append(frequency);

            // Handle Positional Days (BYSETPOS + BYDAY) - e.g., "the 1st Monday"
            if (model.BySetPos.Count > 0
                && model.ByDay.Count > 0)
            {
                string posText = JoinWithAnd(model.BySetPos.Select(FormatOrdinal));
                string dayText = JoinWithAnd(model.ByDay.Select(ToDayName));

                sb.Append(
                    string.Format(
                        Resource.ProjectPlan.Holidays.Holiday_BySetPosAndByDay,
                        posText,
                        dayText));
            }
            else if (model.ByDay.Count > 0)
            {
                string dayText = JoinWithAnd(model.ByDay.Select(ToDayName));

                sb.Append(
                    string.Format(
                        Resource.ProjectPlan.Holidays.Holiday_ByDay,
                        dayText));
            }
            // Handle Monthly/Yearly Days (BYMONTHDAY) - e.g., "the 15th day"
            else if (model.ByMonthDay.Count > 0)
            {
                List<string> days = [.. model.ByMonthDay.Select(FormatOrdinal)];

                string daySuffix = days.Count > 1
                    ? Resource.ProjectPlan.Holidays.Holiday_Days
                    : Resource.ProjectPlan.Holidays.Holiday_Day;

                sb.Append(
                    string.Format(
                        Resource.ProjectPlan.Holidays.Holiday_ByMonthDay,
                        JoinWithAnd(days),
                        daySuffix));
            }

            // Handle Months (BYMONTH)
            string byMonthText = string.Empty;

            if (model.ByMonth.Count > 0)
            {
                byMonthText = string.Format(
                    Resource.ProjectPlan.Holidays.Holiday_ByMonth,
                    JoinWithAnd(model.ByMonth.Select(ToMonthName)));
            }

            sb.Append(byMonthText);

            // Handle Week Start (WKST)
            if (model.WeekStart.HasValue)
            {
                sb.Append(
                    string.Format(
                        Resource.ProjectPlan.Holidays.Holiday_WeekStart,
                        ToDayName(model.WeekStart.Value)));
            }

            // Handle End Condition (COUNT or UNTIL)
            if (model.Count.HasValue)
            {
                sb.Append(
                    string.Format(
                        Resource.ProjectPlan.Holidays.Holiday_Count,
                        model.Count.Value));
            }
            else if (model.Until.HasValue)
            {
                sb.Append(
                    string.Format(
                        Resource.ProjectPlan.Holidays.Holiday_Until,
                        model.Until.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
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