using System.Globalization;
using Zametek.Common.ProjectPlan;

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
        /// <summary>
        /// The most holidays that a scenario can have. Each rule costs a read for each range of days that the calendar counts. On
        /// 2026-11-23, a plan of 46 activities over one year with the custom calendar took 0.9 s with 2 holidays, 1.35 s with
        /// 1,000 and 3.2 s with 10,000 yearly holidays (zpp, Release). An upload of 50 MB can hold about 500,000 holidays.
        /// </summary>
        public const int MaximumHolidayCount = 1_000;

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

        /// <summary>
        /// The cause when the reader of the desktop (<see cref="RecurrencePatternHelper"/>) cannot read the rule, or null when
        /// it can. The dock of the holidays reads each rule with that reader on the main thread, with no catch, and a rule that
        /// it cannot read ended the application (from the review of src, test and pkg). The calendar (Ical.Net) reads more of
        /// RFC 5545, and so zpp can compile a file with such a rule.
        /// </summary>
        public static string? FindReadProblem(string? rule)
        {
            try
            {
                RecurrencePatternHelper.ToRule(rule);
                return null;
            }
            catch (Exception ex) when (ex is FormatException or InvalidOperationException)
            {
                return ex.Message;
            }
        }

        /// <summary>
        /// The first problem of the holidays of a project, as a message for a person: a scenario with more holidays than
        /// <see cref="MaximumHolidayCount"/>, a holiday whose rule is not allowed (<see cref="IsAllowed"/>), or a holiday whose
        /// rule the desktop cannot read (<see cref="FindReadProblem"/>). Null when it has none. The desktop opens a project file
        /// with no check before the load, and so it refuses such a file with this message: the load would otherwise hold it for
        /// minutes, or end the application.
        /// </summary>
        public static string? FindProblem(ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);

            foreach (ProjectScenarioFileModel? file in project.Files)
            {
                List<HolidayModel>? holidays = file?.Scenario?.HolidaySettings?.Holidays;

                if (file is null
                    || holidays is null)
                {
                    continue;
                }

                string scenarioName = project.Nodes?.FirstOrDefault(x => x?.Id == file.NodeId)?.Name ?? string.Empty;

                if (holidays.Count > MaximumHolidayCount)
                {
                    return string.Format(
                        CultureInfo.CurrentCulture,
                        Resource.ProjectPlan.Messages.Message_ProjectTooManyHolidays,
                        scenarioName,
                        holidays.Count,
                        MaximumHolidayCount);
                }

                if (holidays.FirstOrDefault(x => x is not null && !IsAllowed(x.RecurrencePattern)) is HolidayModel holiday)
                {
                    return string.Format(
                        CultureInfo.CurrentCulture,
                        Resource.ProjectPlan.Messages.Message_ProjectHolidayRuleNotAllowed,
                        holiday.Name,
                        scenarioName,
                        holiday.RecurrencePattern);
                }

                foreach (HolidayModel? candidate in holidays)
                {
                    if (candidate is not null
                        && FindReadProblem(candidate.RecurrencePattern) is string cause)
                    {
                        return string.Format(
                            CultureInfo.CurrentCulture,
                            Resource.ProjectPlan.Messages.Message_ProjectHolidayRuleNotReadable,
                            candidate.Name,
                            scenarioName,
                            candidate.RecurrencePattern,
                            cause);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// The first holiday of an import whose rule the desktop cannot read (<see cref="FindReadProblem"/>), as a message for a
        /// person, or null when it has none. The import of a workbook reports a rule that is not allowed itself, and zpp reads
        /// the rules with the calendar. Only the desktop reads them with its own reader.
        /// </summary>
        public static string? FindImportProblem(IEnumerable<HolidayModel> holidays)
        {
            ArgumentNullException.ThrowIfNull(holidays);

            foreach (HolidayModel? holiday in holidays)
            {
                if (holiday is not null
                    && FindReadProblem(holiday.RecurrencePattern) is string cause)
                {
                    return string.Format(
                        CultureInfo.CurrentCulture,
                        Resource.ProjectPlan.Messages.Message_ImportHolidayRuleNotReadable,
                        holiday.Name,
                        holiday.RecurrencePattern,
                        cause);
                }
            }

            return null;
        }
    }
}
