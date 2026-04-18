using System.Globalization;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public static class RecurrencePatternHelper
    {
        public static RecurrenceRuleModel Parse(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                throw new ArgumentException("Pattern cannot be null or empty.", nameof(pattern));

            pattern = pattern.Trim();
            if (pattern.StartsWith("RRULE:", StringComparison.OrdinalIgnoreCase))
                pattern = pattern["RRULE:".Length..];

            var tokens = pattern.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var token in tokens)
            {
                var idx = token.IndexOf('=');
                if (idx <= 0 || idx == token.Length - 1)
                    throw new FormatException($"Invalid RRULE part: '{token}'.");

                var key = token[..idx].Trim();
                var value = token[(idx + 1)..].Trim();

                if (map.ContainsKey(key))
                    throw new FormatException($"Duplicate RRULE part: '{key}'.");

                map[key] = value;
            }

            if (!map.TryGetValue("FREQ", out var freqValue))
                throw new FormatException("RRULE must contain FREQ.");

            var model = new RecurrenceRuleModel
            {
                Frequency = ParseFrequency(freqValue),
                Interval = map.TryGetValue("INTERVAL", out var intervalValue) ? ParsePositiveInt(intervalValue, "INTERVAL") : 1,
                Count = map.TryGetValue("COUNT", out var countValue) ? ParsePositiveInt(countValue, "COUNT") : null,
                Until = map.TryGetValue("UNTIL", out var untilValue) ? ParseUntil(untilValue) : null,
                ByDay = map.TryGetValue("BYDAY", out var byDayValue) ? ParseDays(byDayValue) : [],
                ByMonthDay = map.TryGetValue("BYMONTHDAY", out var byMonthDayValue) ? ParseIntList(byMonthDayValue, "BYMONTHDAY") : [],
                ByMonth = map.TryGetValue("BYMONTH", out var byMonthValue) ? ParseIntList(byMonthValue, "BYMONTH") : [],
                BySetPos = map.TryGetValue("BYSETPOS", out var bySetPosValue) ? ParseIntList(bySetPosValue, "BYSETPOS") : [],
                WeekStart = map.TryGetValue("WKST", out var wkstValue) ? ParseDay(wkstValue) : null
            };

            ValidateModel(model);
            return model;
        }

        public static string ToPattern(RecurrenceRuleModel model)
        {
            ValidateModel(model);

            var parts = new List<string>
        {
            $"FREQ={ToFreqToken(model.Frequency)}"
        };

            if (model.Interval != 1)
                parts.Add($"INTERVAL={model.Interval}");

            if (model.Count is not null)
                parts.Add($"COUNT={model.Count.Value}");
            else if (model.Until is not null)
                parts.Add($"UNTIL={FormatUntil(model.Until.Value)}");

            if (model.ByMonth.Count > 0)
                parts.Add($"BYMONTH={string.Join(",", model.ByMonth)}");

            if (model.ByMonthDay.Count > 0)
                parts.Add($"BYMONTHDAY={string.Join(",", model.ByMonthDay)}");

            if (model.WeekStart is not null)
                parts.Add($"WKST={model.WeekStart.Value}");

            if (model.ByDay.Count > 0)
                parts.Add($"BYDAY={string.Join(",", model.ByDay.Select(d => d.ToString()))}");

            if (model.BySetPos.Count > 0)
                parts.Add($"BYSETPOS={string.Join(",", model.BySetPos)}");

            return "RRULE:" + string.Join(";", parts);
        }

        static RecurrenceFrequency ParseFrequency(string value) => value.ToUpperInvariant() switch
        {
            "SECONDLY" => RecurrenceFrequency.Secondly,
            "MINUTELY" => RecurrenceFrequency.Minutely,
            "HOURLY" => RecurrenceFrequency.Hourly,
            "DAILY" => RecurrenceFrequency.Daily,
            "WEEKLY" => RecurrenceFrequency.Weekly,
            "MONTHLY" => RecurrenceFrequency.Monthly,
            "YEARLY" => RecurrenceFrequency.Yearly,
            _ => throw new FormatException($"Invalid FREQ value: '{value}'.")
        };

        static string ToFreqToken(RecurrenceFrequency frequency) => frequency switch
        {
            RecurrenceFrequency.Secondly => "SECONDLY",
            RecurrenceFrequency.Minutely => "MINUTELY",
            RecurrenceFrequency.Hourly => "HOURLY",
            RecurrenceFrequency.Daily => "DAILY",
            RecurrenceFrequency.Weekly => "WEEKLY",
            RecurrenceFrequency.Monthly => "MONTHLY",
            RecurrenceFrequency.Yearly => "YEARLY",
            _ => throw new InvalidOperationException("FREQ cannot be None.")
        };

        static int ParsePositiveInt(string value, string name)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) || result <= 0)
                throw new FormatException($"Invalid {name} value: '{value}'.");
            return result;
        }

        static DateTime ParseUntil(string value)
        {
            var formats = new[]
            {
            "yyyyMMdd'T'HHmmss'Z'",
            "yyyyMMdd'T'HHmmss",
            "yyyyMMdd"
        };

            if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);

            throw new FormatException($"Invalid UNTIL value: '{value}'.");
        }

        static string FormatUntil(DateTime value)
        {
            var utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
            return utc.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        }

        static List<RecurrenceDay> ParseDays(string value) =>
            value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .Select(ParseDay)
                 .ToList();

        static RecurrenceDay ParseDay(string value) => value.ToUpperInvariant() switch
        {
            "MO" => RecurrenceDay.MO,
            "TU" => RecurrenceDay.TU,
            "WE" => RecurrenceDay.WE,
            "TH" => RecurrenceDay.TH,
            "FR" => RecurrenceDay.FR,
            "SA" => RecurrenceDay.SA,
            "SU" => RecurrenceDay.SU,
            _ => throw new FormatException($"Invalid weekday value: '{value}'.")
        };

        static List<int> ParseIntList(string value, string name) =>
            value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .Select(v =>
                 {
                     if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                         throw new FormatException($"Invalid {name} value: '{v}'.");
                     return n;
                 })
                 .ToList();

        static void ValidateModel(RecurrenceRuleModel model)
        {
            //if (model.Frequency == RecurrenceFrequency.None)
            //    throw new InvalidOperationException("Frequency must be set.");

            if (model.Interval <= 0)
                throw new InvalidOperationException("Interval must be greater than zero.");

            if (model.Count is not null && model.Count <= 0)
                throw new InvalidOperationException("Count must be greater than zero.");

            if (model.Count is not null && model.Until is not null)
                throw new InvalidOperationException("COUNT and UNTIL cannot both be set.");
        }
    }
}