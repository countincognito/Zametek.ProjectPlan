namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// Whether the calendar can read a holiday rule in a time that has a limit. The calendar (DateTimeCalculator) reads each
    /// occurrence of each rule in its range of days into a list, under its lock, and a day is the smallest unit of a plan. A
    /// rule that repeats more often than each day has thousands or millions of occurrences in a day: on 2026-11-23, a count of
    /// 30 days took 5.5 s with FREQ=HOURLY, and it was not done after 2 minutes with FREQ=MINUTELY or FREQ=SECONDLY (heap
    /// 0.9 GB and 1.6 GB). The limit of a compile does not end this work, because it is in the load.
    /// </summary>
    public static class HolidayRuleCheck
    {
        private const string c_RulePrefix = @"RRULE:";
        private const string c_FrequencyToken = @"FREQ";

        private static readonly HashSet<string> s_Frequencies = new(StringComparer.OrdinalIgnoreCase)
        {
            @"DAILY",
            @"WEEKLY",
            @"MONTHLY",
            @"YEARLY",
        };

        private static readonly HashSet<string> s_PartsOfADay = new(StringComparer.OrdinalIgnoreCase)
        {
            @"BYHOUR",
            @"BYMINUTE",
            @"BYSECOND",
        };

        /// <summary>
        /// True when the rule repeats each day or less often, with no part for hours, minutes or seconds. A rule with no
        /// text is not a rule of this kind, and it is true.
        /// </summary>
        public static bool IsAllowed(string? rule)
        {
            if (string.IsNullOrWhiteSpace(rule))
            {
                return true;
            }

            string text = rule.Trim();

            if (text.StartsWith(c_RulePrefix, StringComparison.OrdinalIgnoreCase))
            {
                text = text[c_RulePrefix.Length..];
            }

            foreach (string part in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                int equals = part.IndexOf('=', StringComparison.Ordinal);
                string key = (equals < 0 ? part : part[..equals]).Trim();
                string value = equals < 0 ? string.Empty : part[(equals + 1)..].Trim();

                if (s_PartsOfADay.Contains(key))
                {
                    return false;
                }

                if (string.Equals(key, c_FrequencyToken, StringComparison.OrdinalIgnoreCase)
                    && !s_Frequencies.Contains(value))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
