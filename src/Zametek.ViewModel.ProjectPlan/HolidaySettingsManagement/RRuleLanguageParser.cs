using net.sf.mpxj.planner.schema;
using System.Globalization;

namespace Zametek.ViewModel.ProjectPlan
{
    public class RRule
    {
        public string Freq { get; set; } = "DAILY";
        public int Interval { get; set; } = 1;
        public int? Count { get; set; }
        public DateTime? Until { get; set; }
        public List<string> ByDay { get; set; } = new();
        public List<int> ByMonthDay { get; set; } = new();
        public List<int> ByMonth { get; set; } = new();
        public List<int> BySetPos { get; set; } = new();
        public string? WeekStart { get; set; }
    }

    public static class RRuleLanguageParser
    {
        private static readonly Dictionary<string, string> FreqMap = new()
        {
            { "SECONDLY", "second" },
            { "MINUTELY", "minute" },
            { "HOURLY", "hour" },
            { "DAILY", "day" },
            { "WEEKLY", "week" },
            { "MONTHLY", "month" },
            { "YEARLY", "year" }
        };

        private static readonly Dictionary<string, string> WeekdayMap = new()
        {
            { "MO", "Monday" },
            { "TU", "Tuesday" },
            { "WE", "Wednesday" },
            { "TH", "Thursday" },
            { "FR", "Friday" },
            { "SA", "Saturday" },
            { "SU", "Sunday" }
        };

        private static readonly Dictionary<int, string> MonthMap = new()
        {
            { 1, "January" },
            { 2, "February" },
            { 3, "March" },
            { 4, "April" },
            { 5, "May" },
            { 6, "June" },
            { 7, "July" },
            { 8, "August" },
            { 9, "September" },
            { 10, "October" },
            { 11, "November" },
            { 12, "December" }
        };

        public static string ToText(string rrule, DateTime? dtStart = null)
        {
            if (string.IsNullOrWhiteSpace(rrule))
                throw new ArgumentException("RRULE string must not be empty.", nameof(rrule));

            var rule = ParseRRule(rrule);
            return BuildText(rule, dtStart);
        }

        public static RRule ParseRRule(string rrule)
        {
            var rule = new RRule();

            var parts = rrule.Split([';'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var kv = part.Split('=');
                if (kv.Length != 2)
                    continue;

                var key = kv[0].Trim().ToUpperInvariant();
                var value = kv[1].Trim();

                switch (key)
                {
                    case "FREQ":
                        rule.Freq = value.ToUpperInvariant();
                        break;

                    case "INTERVAL":
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var interval))
                            rule.Interval = Math.Max(1, interval);
                        break;

                    case "COUNT":
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
                            rule.Count = count;
                        break;

                    case "UNTIL":
                        rule.Until = ParseUntil(value);
                        break;

                    case "BYDAY":
                        rule.ByDay = value
                            .Split(',')
                            .Select(v => v.Trim().ToUpperInvariant())
                            .Where(v => !string.IsNullOrEmpty(v))
                            .ToList();
                        break;

                    case "BYMONTHDAY":
                        rule.ByMonthDay = value
                            .Split(',')
                            .Select(v => int.Parse(v, CultureInfo.InvariantCulture))
                            .ToList();
                        break;

                    case "BYMONTH":
                        rule.ByMonth = value
                            .Split(',')
                            .Select(v => int.Parse(v, CultureInfo.InvariantCulture))
                            .ToList();
                        break;

                    case "BYSETPOS":
                        rule.BySetPos = value
                            .Split(',')
                            .Select(v => int.Parse(v, CultureInfo.InvariantCulture))
                            .ToList();
                        break;

                    case "WKST":
                        rule.WeekStart = value.ToUpperInvariant();
                        break;

                    default:
                        break;
                }
            }

            return rule;
        }

        private static DateTime? ParseUntil(string value)
        {
            if (value.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
            {
                if (DateTime.TryParseExact(
                        value,
                        "yyyyMMdd'T'HHmmss'Z'",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                        out var dtUtc))
                {
                    return dtUtc;
                }
            }

            if (DateTime.TryParseExact(
                    value,
                    "yyyyMMdd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var dtDate))
            {
                return dtDate;
            }

            return null;
        }

        private static string BuildText(RRule rule, DateTime? dtStart)
        {
            var parts = new List<string>();

            var freqBase = FreqMap.TryGetValue(rule.Freq, out var f) ? f : "day";
            string freqPhrase;

            if (rule.Interval == 1)
            {
                freqPhrase = rule.Freq switch
                {
                    "DAILY" => "Every day",
                    "WEEKLY" => "Every week",
                    "MONTHLY" => "Every month",
                    "YEARLY" => "Every year",
                    "HOURLY" => "Every hour",
                    "MINUTELY" => "Every minute",
                    "SECONDLY" => "Every second",
                    _ => $"Every {freqBase}"
                };
            }
            else
            {
                freqPhrase = $"Every {rule.Interval} {freqBase}" +
                             (rule.Interval > 1 ? "s" : string.Empty);
            }

            parts.Add(freqPhrase);

            // WEEKLY: ignore ordinals in BYDAY (1SU -> SU) and list weekdays
            if (rule.Freq == "WEEKLY" && rule.ByDay.Any())
            {
                var dayNames = rule.ByDay
                    .Select(t => ParseByDayEntry(t).Weekday)   // drop Ordinal
                    .Select(code => WeekdayMap.TryGetValue(code, out var name) ? name : code)
                    .ToList();

                if (dayNames.Any())
                {
                    parts.Add("on " + JoinList(dayNames));
                }
            }

            // MONTHLY with BYMONTHDAY
            if (rule.Freq == "MONTHLY" && rule.ByMonthDay.Any())
            {
                var ordinals = rule.ByMonthDay.Select(ToOrdinal).ToList();
                parts.Add("on the " + JoinList(ordinals));
            }

            // MONTHLY with BYDAY and BYSETPOS (e.g., 4th Sunday)
            if (rule.Freq == "MONTHLY" && rule.ByDay.Any() && rule.BySetPos.Any())
            {
                var plainDays = rule.ByDay
                    .Select(ParseByDayEntry)
                    .Select(d => WeekdayMap.TryGetValue(d.Weekday, out var name) ? name : d.Weekday)
                    .ToList();

                var ordinals = rule.BySetPos.Select(ToOrdinal).ToList();

                if (plainDays.Any() && ordinals.Any())
                {
                    parts.Add("on the " + JoinList(ordinals) + " " + JoinList(plainDays));
                }
            }

            // YEARLY with BYMONTH and BYMONTHDAY
            if (rule.Freq == "YEARLY" && rule.ByMonth.Any() && rule.ByMonthDay.Any())
            {
                var months = rule.ByMonth
                    .Select(m => MonthMap.TryGetValue(m, out var name) ? name : $"month {m}")
                    .ToList();

                var days = rule.ByMonthDay.Select(ToOrdinal).ToList();

                // Single month/day pair: "Every year on the 10th of April"
                if (months.Count == 1 && days.Count == 1)
                {
                    parts.Add($"on the {days[0]} of {months[0]}");
                }
                else
                {
                    // Multiple: "on the 1st and 15th of January and July"
                    parts.Add("on the " + JoinList(days) + " of " + JoinList(months));
                }
            }









            // YEARLY with BYDAY, BYMONTH and BYSETPOS (e.g., 4th Sunday of March)
            if (rule.Freq == "YEARLY" && rule.ByDay.Any() && rule.ByMonth.Any() && rule.BySetPos.Any())
            {
                var plainDays = rule.ByDay
                    .Select(ParseByDayEntry)
                    .Select(d => WeekdayMap.TryGetValue(d.Weekday, out var name) ? name : d.Weekday)
                    .ToList();

                var months = rule.ByMonth
                     .Select(m => MonthMap.TryGetValue(m, out var name) ? name : $"month {m}")
                     .ToList();

                var ordinals = rule.BySetPos.Select(ToOrdinal).ToList();





                // Single month/day pair: "Every year on the 2nd Thursday of April"
                if (months.Count == 1 && ordinals.Count == 1)
                {
                    parts.Add($"on the {ordinals[0]} {plainDays[0]} of {months[0]}");
                }
                else
                {
                    // Multiple: "on the 1st and 15th of January and July"
                    parts.Add("on the " + JoinList(ordinals) + " " + JoinList(plainDays) + " of " + JoinList(months));
                }






                //if (plainDays.Any() && ordinals.Any())
                //{
                //    parts.Add("on the " + JoinList(ordinals) + " " + JoinList(plainDays));
                //}
            }















            // Count / Until
            if (rule.Count.HasValue)
            {
                parts.Add($"for {rule.Count.Value} time" + (rule.Count.Value == 1 ? "" : "s"));
            }
            else if (rule.Until.HasValue)
            {
                var untilStr = rule.Until.Value.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
                parts.Add($"until {untilStr}");
            }

            if (dtStart.HasValue && parts.Count == 1)
            {
                parts.Add($"starting {dtStart.Value.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}");
            }

            var text = parts[0];
            if (parts.Count > 1)
            {
                text += " " + string.Join(" ", parts.Skip(1));
            }

            return text;
        }

        private class ByDayEntry
        {
            public int? Ordinal { get; set; }
            public string Weekday { get; set; } = "MO";
        }

        private static ByDayEntry ParseByDayEntry(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return new ByDayEntry { Weekday = "MO" };

            token = token.ToUpperInvariant();

            var weekday = token[^2..];
            var numberPart = token.Length > 2 ? token[..^2] : null;

            int? ordinal = null;
            if (!string.IsNullOrEmpty(numberPart) &&
                int.TryParse(numberPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
            {
                ordinal = n;
            }

            return new ByDayEntry
            {
                Ordinal = ordinal,
                Weekday = weekday
            };
        }

        private static string ToOrdinal(int n)
        {
            var abs = Math.Abs(n);
            string suffix;
            var ones = abs % 10;
            var tens = (abs / 10) % 10;

            if (tens == 1)
            {
                suffix = "th";
            }
            else
            {
                suffix = ones switch
                {
                    1 => "st",
                    2 => "nd",
                    3 => "rd",
                    _ => "th"
                };
            }

            var s = abs + suffix;
            if (n < 0)
            {
                s = s + " from the end";
            }

            return s;
        }

        private static string JoinList(IReadOnlyList<string> items)
        {
            if (items.Count == 0) return string.Empty;
            if (items.Count == 1) return items[0];
            if (items.Count == 2) return $"{items[0]} and {items[1]}";
            return string.Join(", ", items.Take(items.Count - 1)) + " and " + items[^1];
        }
    }
}

















//using System.Globalization;

//namespace Zametek.ViewModel.ProjectPlan
//{
//    public class RRule
//    {
//        public string Freq { get; set; } = "DAILY";
//        public int Interval { get; set; } = 1;
//        public int? Count { get; set; }
//        public DateTime? Until { get; set; }
//        public List<string> ByDay { get; set; } = new();
//        public List<int> ByMonthDay { get; set; } = new();
//        public List<int> ByMonth { get; set; } = new();
//        public List<int> BySetPos { get; set; } = new();
//        public string? WeekStart { get; set; }
//    }

//    public static class RRuleLanguageParser
//    {
//        private static readonly Dictionary<string, string> FreqMap = new()
//        {
//            { "SECONDLY", "second" },
//            { "MINUTELY", "minute" },
//            { "HOURLY", "hour" },
//            { "DAILY", "day" },
//            { "WEEKLY", "week" },
//            { "MONTHLY", "month" },
//            { "YEARLY", "year" }
//        };

//        private static readonly Dictionary<string, string> WeekdayMap = new()
//        {
//            { "MO", "Monday" },
//            { "TU", "Tuesday" },
//            { "WE", "Wednesday" },
//            { "TH", "Thursday" },
//            { "FR", "Friday" },
//            { "SA", "Saturday" },
//            { "SU", "Sunday" }
//        };

//        private static readonly Dictionary<int, string> MonthMap = new()
//        {
//            { 1, "January" }, { 2, "February" }, { 3, "March" }, { 4, "April" },
//            { 5, "May" }, { 6, "June" }, { 7, "July" }, { 8, "August" },
//            { 9, "September" }, { 10, "October" }, { 11, "November" }, { 12, "December" }
//        };

//        public static string ToText(string rrule, DateTime? dtStart = null)
//        {
//            if (string.IsNullOrWhiteSpace(rrule))
//                throw new ArgumentException("RRULE string must not be empty.", nameof(rrule));

//            var rule = ParseRRule(rrule);
//            return BuildText(rule, dtStart);
//        }

//        public static RRule ParseRRule(string rrule)
//        {
//            var rule = new RRule();

//            var parts = rrule.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
//            foreach (var part in parts)
//            {
//                var kv = part.Split('=');
//                if (kv.Length != 2)
//                    continue;

//                var key = kv[0].Trim().ToUpperInvariant();
//                var value = kv[1].Trim();

//                switch (key)
//                {
//                    case "FREQ":
//                        rule.Freq = value.ToUpperInvariant();
//                        break;

//                    case "INTERVAL":
//                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var interval))
//                            rule.Interval = Math.Max(1, interval);
//                        break;

//                    case "COUNT":
//                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
//                            rule.Count = count;
//                        break;

//                    case "UNTIL":
//                        // UNTIL can be DATE or DATE-TIME in UTC like 19980404T070000Z
//                        rule.Until = ParseUntil(value);
//                        break;

//                    case "BYDAY":
//                        rule.ByDay = value
//                            .Split(',')
//                            .Select(v => v.Trim().ToUpperInvariant())
//                            .Where(v => !string.IsNullOrEmpty(v))
//                            .ToList();
//                        break;

//                    case "BYMONTHDAY":
//                        rule.ByMonthDay = value
//                            .Split(',')
//                            .Select(v => int.Parse(v, CultureInfo.InvariantCulture))
//                            .ToList();
//                        break;

//                    case "BYMONTH":
//                        rule.ByMonth = value
//                            .Split(',')
//                            .Select(v => int.Parse(v, CultureInfo.InvariantCulture))
//                            .ToList();
//                        break;

//                    case "BYSETPOS":
//                        rule.BySetPos = value
//                            .Split(',')
//                            .Select(v => int.Parse(v, CultureInfo.InvariantCulture))
//                            .ToList();
//                        break;

//                    case "WKST":
//                        rule.WeekStart = value.ToUpperInvariant();
//                        break;

//                    default:
//                        // ignore unsupported parts for now
//                        break;
//                }
//            }

//            return rule;
//        }

//        private static DateTime? ParseUntil(string value)
//        {
//            // DATE-TIME in UTC with 'Z'
//            if (value.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
//            {
//                if (DateTime.TryParseExact(
//                        value,
//                        "yyyyMMdd'T'HHmmss'Z'",
//                        CultureInfo.InvariantCulture,
//                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
//                        out var dtUtc))
//                {
//                    return dtUtc;
//                }
//            }

//            // DATE only
//            if (DateTime.TryParseExact(
//                    value,
//                    "yyyyMMdd",
//                    CultureInfo.InvariantCulture,
//                    DateTimeStyles.None,
//                    out var dtDate))
//            {
//                return dtDate;
//            }

//            return null;
//        }

//        private static string BuildText(RRule rule, DateTime? dtStart)
//        {
//            var parts = new List<string>();

//            // Base frequency + interval
//            var freqBase = FreqMap.TryGetValue(rule.Freq, out var f) ? f : "day";
//            string freqPhrase;

//            if (rule.Interval == 1)
//            {
//                freqPhrase = rule.Freq switch
//                {
//                    "DAILY" => "Every day",
//                    "WEEKLY" => "Every week",
//                    "MONTHLY" => "Every month",
//                    "YEARLY" => "Every year",
//                    "HOURLY" => "Every hour",
//                    "MINUTELY" => "Every minute",
//                    "SECONDLY" => "Every second",
//                    _ => $"Every {freqBase}"
//                };
//            }
//            else
//            {
//                freqPhrase = $"Every {rule.Interval} {freqBase}" +
//                             (rule.Interval > 1 ? "s" : string.Empty);
//            }

//            parts.Add(freqPhrase);

//            // Weekly BYDAY
//            if (rule.Freq == "WEEKLY" && rule.ByDay.Any())
//            {
//                var dayNames = rule.ByDay
//                    .Select(ParseByDayEntry)
//                    .Where(d => d.Ordinal == null) // only plain days like MO,TU,...
//                    .Select(d => WeekdayMap.TryGetValue(d.Weekday, out var name) ? name : d.Weekday)
//                    .ToList();

//                if (dayNames.Any())
//                {
//                    parts.Add("on " + JoinList(dayNames));
//                }
//            }

//            // Monthly with BYMONTHDAY
//            if (rule.Freq == "MONTHLY" && rule.ByMonthDay.Any())
//            {
//                var ordinals = rule.ByMonthDay.Select(ToOrdinal).ToList();
//                parts.Add("on the " + JoinList(ordinals));
//            }

//            // Monthly with BYDAY and BYSETPOS (e.g., 4th Sunday)
//            if (rule.Freq == "MONTHLY" && rule.ByDay.Any() && rule.BySetPos.Any())
//            {
//                var dayEntries = rule.ByDay.Select(ParseByDayEntry).ToList();
//                var plainDays = dayEntries
//                    .Where(d => d.Ordinal == null)
//                    .Select(d => WeekdayMap.TryGetValue(d.Weekday, out var name) ? name : d.Weekday)
//                    .ToList();

//                var ordinals = rule.BySetPos.Select(ToOrdinal).ToList();

//                if (plainDays.Any() && ordinals.Any())
//                {
//                    // Example: "on the fourth Sunday"
//                    parts.Add("on the " + JoinList(ordinals) + " " + JoinList(plainDays));
//                }
//            }

//            // Yearly with BYMONTH and BYMONTHDAY
//            if (rule.Freq == "YEARLY" && rule.ByMonth.Any() && rule.ByMonthDay.Any())
//            {
//                var months = rule.ByMonth
//                    .Select(m => MonthMap.TryGetValue(m, out var name) ? name : $"month {m}")
//                    .ToList();

//                var days = rule.ByMonthDay.Select(ToOrdinal).ToList();

//                parts.Add("on " + JoinList(days) + " of " + JoinList(months));
//            }

//            // Count / Until
//            if (rule.Count.HasValue)
//            {
//                parts.Add($"for {rule.Count.Value} time" + (rule.Count.Value == 1 ? "" : "s"));
//            }
//            else if (rule.Until.HasValue)
//            {
//                var untilStr = rule.Until.Value.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
//                parts.Add($"until {untilStr}");
//            }

//            // If nothing specific beyond FREQ, optionally use dtStart to add one example
//            if (dtStart.HasValue && parts.Count == 1)
//            {
//                parts.Add($"starting {dtStart.Value.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}");
//            }

//            // Combine base phrase and modifiers
//            var text = parts[0];
//            if (parts.Count > 1)
//            {
//                text += " " + string.Join(" ", parts.Skip(1));
//            }

//            return text;
//        }

//        private class ByDayEntry
//        {
//            public int? Ordinal { get; set; } // e.g., 1 in 1SU
//            public string Weekday { get; set; } = "MO"; // e.g., SU
//        }

//        // Handles entries like "MO", "1SU", "-1FR"
//        private static ByDayEntry ParseByDayEntry(string token)
//        {
//            if (string.IsNullOrWhiteSpace(token))
//                return new ByDayEntry { Weekday = "MO" };

//            token = token.ToUpperInvariant();

//            // split into number part + weekday
//            var weekday = token[^2..]; // last 2 chars
//            var numberPart = token.Length > 2 ? token[..^2] : null;

//            int? ordinal = null;
//            if (!string.IsNullOrEmpty(numberPart) &&
//                int.TryParse(numberPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
//            {
//                ordinal = n;
//            }

//            return new ByDayEntry
//            {
//                Ordinal = ordinal,
//                Weekday = weekday
//            };
//        }

//        private static string ToOrdinal(int n)
//        {
//            var abs = Math.Abs(n);
//            string suffix;
//            var ones = abs % 10;
//            var tens = (abs / 10) % 10;

//            if (tens == 1)
//            {
//                suffix = "th";
//            }
//            else
//            {
//                suffix = ones switch
//                {
//                    1 => "st",
//                    2 => "nd",
//                    3 => "rd",
//                    _ => "th"
//                };
//            }

//            var s = abs + suffix;
//            if (n < 0)
//            {
//                s = s + " from the end";
//            }

//            return s;
//        }

//        private static string JoinList(IReadOnlyList<string> items)
//        {
//            if (items.Count == 0) return string.Empty;
//            if (items.Count == 1) return items[0];
//            if (items.Count == 2) return $"{items[0]} and {items[1]}";
//            return string.Join(", ", items.Take(items.Count - 1)) + " and " + items[^1];
//        }
//    }
//}