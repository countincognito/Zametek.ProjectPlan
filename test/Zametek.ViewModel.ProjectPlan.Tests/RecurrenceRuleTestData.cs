using System;
using System.Collections.Generic;
using System.Text;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    public static class RecurrenceRuleTestData
    {
        public static readonly List<string> InputPatterns =
        [
            "RRULE:FREQ=DAILY",
            "RRULE:FREQ=DAILY;INTERVAL=2",
            "RRULE:FREQ=WEEKLY",
            "RRULE:FREQ=WEEKLY;BYDAY=MO",
            "RRULE:FREQ=WEEKLY;BYDAY=MO,WE,FR",
            "RRULE:FREQ=WEEKLY;INTERVAL=2;BYDAY=TU,TH",
            "RRULE:FREQ=MONTHLY;BYMONTHDAY=15",
            "RRULE:FREQ=MONTHLY;BYMONTHDAY=1,15",
            "RRULE:FREQ=MONTHLY;BYDAY=MO",
            "RRULE:FREQ=MONTHLY;BYDAY=MO;BYSETPOS=1",
            "RRULE:FREQ=MONTHLY;BYMONTHDAY=-1",
            "RRULE:FREQ=MONTHLY;INTERVAL=5;BYDAY=SU;BYSETPOS=4",
            "RRULE:FREQ=YEARLY;BYMONTH=1",
            "RRULE:FREQ=YEARLY;BYMONTH=1,12;BYMONTHDAY=1",
            "RRULE:FREQ=YEARLY;BYMONTH=12;BYDAY=SU;BYSETPOS=-1",
            "RRULE:FREQ=DAILY;COUNT=10",
            "RRULE:FREQ=DAILY;INTERVAL=2;COUNT=10",
            "RRULE:FREQ=DAILY;UNTIL=20261231T000000Z",
            "RRULE:FREQ=DAILY;UNTIL=20261231T235959Z",
            "RRULE:FREQ=WEEKLY;WKST=MO;BYDAY=TU,TH",
            "RRULE:FREQ=MONTHLY;INTERVAL=3;BYMONTHDAY=1,15",
            "RRULE:FREQ=YEARLY;INTERVAL=2;BYMONTH=4;BYDAY=SU;BYSETPOS=2"
        ];

        public static readonly List<RecurrenceRuleModel> OutputModels =
        [
            new() { Frequency = RecurrenceFrequency.Daily },
            new() { Frequency = RecurrenceFrequency.Daily, Interval = 2 },
            new() { Frequency = RecurrenceFrequency.Weekly },
            new() { Frequency = RecurrenceFrequency.Weekly, ByDay = [RecurrenceDay.MO] },
            new() { Frequency = RecurrenceFrequency.Weekly, ByDay = [RecurrenceDay.MO, RecurrenceDay.WE, RecurrenceDay.FR] },
            new() { Frequency = RecurrenceFrequency.Weekly, Interval = 2, ByDay = [RecurrenceDay.TU, RecurrenceDay.TH] },
            new() { Frequency = RecurrenceFrequency.Monthly, ByMonthDay = [15] },
            new() { Frequency = RecurrenceFrequency.Monthly, ByMonthDay = [1,15] },
            new() { Frequency = RecurrenceFrequency.Monthly, ByDay = [RecurrenceDay.MO] },
            new() { Frequency = RecurrenceFrequency.Monthly, ByDay = [RecurrenceDay.MO], BySetPos = [1] },
            new() { Frequency = RecurrenceFrequency.Monthly, ByMonthDay = [-1] },
            new() { Frequency = RecurrenceFrequency.Monthly, Interval = 5, ByDay = [RecurrenceDay.SU], BySetPos = [4]},
            new() { Frequency = RecurrenceFrequency.Yearly, ByMonth = [1] },
            new() { Frequency = RecurrenceFrequency.Yearly, ByMonth = [1, 12], ByMonthDay = [1] },
            new() { Frequency = RecurrenceFrequency.Yearly, ByMonth = [12], ByDay = [RecurrenceDay.SU], BySetPos = [-1] },
            new() { Frequency = RecurrenceFrequency.Daily, Count = 10 },
            new() { Frequency = RecurrenceFrequency.Daily, Interval = 2, Count = 10 },
            new() { Frequency = RecurrenceFrequency.Daily, Until = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc) },
            new() { Frequency = RecurrenceFrequency.Daily, Until = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc) },
            new() { Frequency = RecurrenceFrequency.Weekly, ByDay = [RecurrenceDay.TU, RecurrenceDay.TH], WeekStart = RecurrenceDay.MO },
            new() { Frequency = RecurrenceFrequency.Monthly, Interval = 3, ByMonthDay = [1, 15] },
            new() { Frequency = RecurrenceFrequency.Yearly, Interval = 2, ByMonth = [4], ByDay = [RecurrenceDay.SU], BySetPos = [2] }
        ];

        public static readonly List<string> ExpectedEnglishPhrases =
        [
            "every day",
            "every 2 days",
            "every week",
            "every week on Monday",
            "every week on Monday, Wednesday and Friday",
            "every 2 weeks on Tuesday and Thursday",
            "every month on the 15th day",
            "every month on the 1st and 15th days",
            "every month on Monday",
            "every month on the 1st Monday",
            "every month on the last day",
            "every 5 months on the 4th Sunday",
            "every year in January",
            "every year on the 1st day in January and December",
            "every year on the last Sunday in December",
            "every day for 10 occurrences",
            "every 2 days for 10 occurrences",
            "every day until 2026-12-31",
            "every day until 2026-12-31",
            "every week on Tuesday and Thursday with week starting Monday",
            "every 3 months on the 1st and 15th days",
            "every 2 years on the 2nd Sunday in April"
        ];
    }
}
