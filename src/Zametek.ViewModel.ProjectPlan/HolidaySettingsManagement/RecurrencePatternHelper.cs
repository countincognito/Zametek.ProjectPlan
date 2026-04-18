using System.Globalization;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public static class RecurrencePatternHelper
    {
        private const string c_FrequencyToken = "FREQ";
        private const string c_IntervalToken = "INTERVAL";
        private const string c_CountToken = "COUNT";
        private const string c_UntilToken = "UNTIL";
        private const string c_ByDayToken = "BYDAY";
        private const string c_ByMonthDayToken = "BYMONTHDAY";
        private const string c_ByMonthToken = "BYMONTH";
        private const string c_BySetPosToken = "BYSETPOS";
        private const string c_WeekStartToken = "WKST";

        private const string c_FrequencySecondlyToken = "SECONDLY";
        private const string c_FrequencyMinutelyToken = "MINUTELY";
        private const string c_FrequencyHourlyToken = "HOURLY";
        private const string c_FrequencyDailyToken = "DAILY";
        private const string c_FrequencyWeeklyToken = "WEEKLY";
        private const string c_FrequencyMonthlyToken = "MONTHLY";
        private const string c_FrequencyYearlyToken = "YEARLY";

        private const string c_DayMondayToken = "MO";
        private const string c_DayTuesdayToken = "TU";
        private const string c_DayWednesdayToken = "WE";
        private const string c_DayThursdayToken = "TH";
        private const string c_DayFridayToken = "FR";
        private const string c_DaySaturdayToken = "SA";
        private const string c_DaySundayToken = "SU";

        public static RecurrenceRuleModel ToRule(string pattern)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

            pattern = pattern.Trim();
            string[] tokens = pattern.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string token in tokens)
            {
                int idx = token.IndexOf('=');

                if (idx <= 0
                    || idx == token.Length - 1)
                {
                    throw new FormatException(string.Format(Resource.ProjectPlan.Messages.Message_InvalidRRulePart, token));
                }

                string key = token[..idx].Trim();
                string value = token[(idx + 1)..].Trim();

                if (map.ContainsKey(key))
                {
                    throw new FormatException(string.Format(Resource.ProjectPlan.Messages.Message_DuplicateRRulePart, key));
                }

                map[key] = value;
            }

            if (!map.TryGetValue(c_FrequencyToken, out var freqValue))
            {
                throw new FormatException(string.Format(Resource.ProjectPlan.Messages.Message_RRuleMustContain, c_FrequencyToken));
            }

            var model = new RecurrenceRuleModel
            {
                Frequency = ParseFrequency(freqValue),
                Interval = map.TryGetValue(c_IntervalToken, out var intervalValue) ? ParsePositiveInt(intervalValue, c_IntervalToken) : 1,
                Count = map.TryGetValue(c_CountToken, out var countValue) ? ParsePositiveInt(countValue, c_CountToken) : null,
                Until = map.TryGetValue(c_UntilToken, out var untilValue) ? ParseUntil(untilValue) : null,
                ByDay = map.TryGetValue(c_ByDayToken, out var byDayValue) ? ParseDays(byDayValue) : [],
                ByMonthDay = map.TryGetValue(c_ByMonthDayToken, out var byMonthDayValue) ? ParseIntList(byMonthDayValue, c_ByMonthDayToken) : [],
                ByMonth = map.TryGetValue(c_ByMonthToken, out var byMonthValue) ? ParseIntList(byMonthValue, c_ByMonthToken) : [],
                BySetPos = map.TryGetValue(c_BySetPosToken, out var bySetPosValue) ? ParseIntList(bySetPosValue, c_BySetPosToken) : [],
                WeekStart = map.TryGetValue(c_WeekStartToken, out var wkstValue) ? ParseDay(wkstValue) : null
            };

            ValidateModel(model);
            return model;
        }

        public static string ToPattern(RecurrenceRuleModel model)
        {
            ValidateModel(model);

            var parts = new List<string>
            {
                $@"{c_FrequencyToken}={ToFrequencyToken(model.Frequency)}"
            };

            if (model.Interval != 1)
            {
                parts.Add($@"{c_IntervalToken}={model.Interval}");
            }

            if (model.Count.HasValue)
            {
                parts.Add($@"{c_CountToken}={model.Count.Value}");
            }
            else if (model.Until.HasValue)
            {
                parts.Add($@"{c_UntilToken}={FormatUntil(model.Until.Value)}");
            }

            if (model.ByMonth.Count > 0)
            {
                parts.Add($@"{c_ByMonthToken}={string.Join(',', model.ByMonth)}");
            }

            if (model.ByMonthDay.Count > 0)
            {
                parts.Add($@"{c_ByMonthDayToken}={string.Join(',', model.ByMonthDay)}");
            }

            if (model.WeekStart.HasValue)
            {
                parts.Add($@"{c_WeekStartToken}={ToDayToken(model.WeekStart.Value)}");
            }

            if (model.ByDay.Count > 0)
            {
                parts.Add($@"{c_ByDayToken}={string.Join(',', model.ByDay.Select(ToDayToken))}");
            }

            if (model.BySetPos.Count > 0)
            {
                parts.Add($@"{c_BySetPosToken}={string.Join(',', model.BySetPos)}");
            }

            return string.Join(';', parts);
        }

        public static RecurrenceFrequency ParseFrequency(string value) => value.ToUpperInvariant() switch
        {
            c_FrequencySecondlyToken => RecurrenceFrequency.Secondly,
            c_FrequencyMinutelyToken => RecurrenceFrequency.Minutely,
            c_FrequencyHourlyToken => RecurrenceFrequency.Hourly,
            c_FrequencyDailyToken => RecurrenceFrequency.Daily,
            c_FrequencyWeeklyToken => RecurrenceFrequency.Weekly,
            c_FrequencyMonthlyToken => RecurrenceFrequency.Monthly,
            c_FrequencyYearlyToken => RecurrenceFrequency.Yearly,
            _ => throw new FormatException(string.Format(Resource.ProjectPlan.Messages.Message_InvalidFrequencyValue, value))
        };

        public static string ToFrequencyToken(RecurrenceFrequency frequency) => frequency switch
        {
            RecurrenceFrequency.Secondly => c_FrequencySecondlyToken,
            RecurrenceFrequency.Minutely => c_FrequencyMinutelyToken,
            RecurrenceFrequency.Hourly => c_FrequencyHourlyToken,
            RecurrenceFrequency.Daily => c_FrequencyDailyToken,
            RecurrenceFrequency.Weekly => c_FrequencyWeeklyToken,
            RecurrenceFrequency.Monthly => c_FrequencyMonthlyToken,
            RecurrenceFrequency.Yearly => c_FrequencyYearlyToken,
            _ => throw new InvalidOperationException(string.Format(Resource.ProjectPlan.Messages.Message_InvalidFrequencyValue, frequency))
        };

        public static RecurrenceDay ParseDay(string value) => value.ToUpperInvariant() switch
        {
            c_DayMondayToken => RecurrenceDay.MO,
            c_DayTuesdayToken => RecurrenceDay.TU,
            c_DayWednesdayToken => RecurrenceDay.WE,
            c_DayThursdayToken => RecurrenceDay.TH,
            c_DayFridayToken => RecurrenceDay.FR,
            c_DaySaturdayToken => RecurrenceDay.SA,
            c_DaySundayToken => RecurrenceDay.SU,
            _ => throw new FormatException(string.Format(Resource.ProjectPlan.Messages.Message_InvalidWeekdayValue, value))
        };

        public static string ToDayToken(RecurrenceDay day) => day switch
        {
            RecurrenceDay.MO => c_DayMondayToken,
            RecurrenceDay.TU => c_DayTuesdayToken,
            RecurrenceDay.WE => c_DayWednesdayToken,
            RecurrenceDay.TH => c_DayThursdayToken,
            RecurrenceDay.FR => c_DayFridayToken,
            RecurrenceDay.SA => c_DaySaturdayToken,
            RecurrenceDay.SU => c_DaySundayToken,
            _ => throw new FormatException(string.Format(Resource.ProjectPlan.Messages.Message_InvalidWeekdayValue, day))
        };

        private static int ParsePositiveInt(string value, string name)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) || result <= 0)
            {
                throw new FormatException(string.Format(Resource.ProjectPlan.Messages.Message_InvalidInputValue, name, value));
            }
            return result;
        }

        private static DateTime ParseUntil(string value)
        {
            string[] formats =
            [
                @"yyyyMMdd'T'HHmmss'Z'",
                @"yyyyMMdd'T'HHmmss",
                @"yyyyMMdd"
            ];

            if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
            {
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            }
            throw new FormatException(string.Format(Resource.ProjectPlan.Messages.Message_InvalidInputValue, c_UntilToken, value));
        }

        private static string FormatUntil(DateTime value)
        {
            var utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
            return utc.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        }

        private static List<RecurrenceDay> ParseDays(string value) =>
            [.. value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(ParseDay)];

        private static List<int> ParseIntList(string value, string name) =>
            [.. value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x =>
                 {
                     if (!int.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out var output))
                     {
                         throw new FormatException(string.Format(Resource.ProjectPlan.Messages.Message_InvalidInputValue, name, x));
                     }
                     return output;
                 })];

        private static void ValidateModel(RecurrenceRuleModel model)
        {
            if (model.Interval <= 0)
            {

                throw new InvalidOperationException(string.Format(Resource.ProjectPlan.Messages.Message_MustBeGreaterThanZero, c_IntervalToken, model.Interval));
            }

            if (model.Count.HasValue
                && model.Count <= 0)
            {
                throw new InvalidOperationException(string.Format(Resource.ProjectPlan.Messages.Message_MustBeGreaterThanZero, c_CountToken, model.Count));
            }

            if (model.Count.HasValue
                && model.Until.HasValue)
            {

                throw new InvalidOperationException(string.Format(Resource.ProjectPlan.Messages.Message_CannotBothBeSet, c_CountToken, c_UntilToken));
            }
        }
    }
}