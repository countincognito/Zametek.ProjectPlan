using System;
using System.Collections.Generic;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    public static class RecurrenceRuleFixture
    {
        public static readonly List<string> InputPatterns =
        [
            "",
            "FREQ=SECONDLY",
            "FREQ=MINUTELY;INTERVAL=10",
            "FREQ=HOURLY;INTERVAL=3;COUNT=4",
            "FREQ=DAILY",
            "FREQ=DAILY;INTERVAL=2",
            "FREQ=WEEKLY",
            "FREQ=WEEKLY;BYDAY=MO",
            "FREQ=WEEKLY;BYDAY=MO,WE,FR",
            "FREQ=WEEKLY;INTERVAL=2;BYDAY=TU,TH",
            "FREQ=MONTHLY;BYMONTHDAY=15",
            "FREQ=MONTHLY;BYMONTHDAY=1,15",
            "FREQ=MONTHLY;BYDAY=MO",
            "FREQ=MONTHLY;BYDAY=MO;BYSETPOS=1",
            "FREQ=MONTHLY;BYMONTHDAY=-1",
            "FREQ=MONTHLY;BYDAY=FR;BYSETPOS=-1",
            "FREQ=MONTHLY;INTERVAL=5;BYDAY=SU;BYSETPOS=4",
            "FREQ=MONTHLY;BYDAY=MO,FR;BYSETPOS=2,3",
            "FREQ=MONTHLY;INTERVAL=3;UNTIL=20251231T000000Z;BYMONTHDAY=10",
            "FREQ=MONTHLY;BYMONTHDAY=1,2,3,4,11,21,22,23",
            "FREQ=YEARLY;BYMONTH=1",
            "FREQ=YEARLY;BYMONTH=1,12;BYMONTHDAY=1",
            "FREQ=YEARLY;BYMONTH=12;BYDAY=SU;BYSETPOS=-1",
            "FREQ=DAILY;COUNT=10",
            "FREQ=DAILY;INTERVAL=2;COUNT=10",
            "FREQ=DAILY;UNTIL=20261231T000000Z",
            "FREQ=DAILY;UNTIL=20261231T235959Z",
            "FREQ=WEEKLY;WKST=MO;BYDAY=TU,TH",
            "FREQ=MONTHLY;INTERVAL=3;BYMONTHDAY=1,15",
            "FREQ=YEARLY;INTERVAL=2;BYMONTH=4;BYDAY=SU;BYSETPOS=2",
            "FREQ=YEARLY;BYMONTH=4;BYMONTHDAY=10",
            "FREQ=YEARLY;BYMONTH=1,7;BYMONTHDAY=1,15",
            //"FREQ=DAILY;BYHOUR=10;BYMINUTE=30",
            //"FREQ=WEEKLY;BYDAY=1SU,2MO"
        ];

        public static readonly List<RecurrenceRuleModel> OutputModels =
        [
            new() { Frequency = RecurrenceFrequency.None },
            new() { Frequency = RecurrenceFrequency.Secondly },
            new() { Frequency = RecurrenceFrequency.Minutely, Interval = 10 },
            new() { Frequency = RecurrenceFrequency.Hourly, Interval = 3, Count = 4 },
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
            new() { Frequency = RecurrenceFrequency.Monthly, ByDay = [RecurrenceDay.FR], BySetPos = [-1] },
            new() { Frequency = RecurrenceFrequency.Monthly, Interval = 5, ByDay = [RecurrenceDay.SU], BySetPos = [4]},
            new() { Frequency = RecurrenceFrequency.Monthly, ByDay = [RecurrenceDay.MO, RecurrenceDay.FR], BySetPos = [2,3]},
            new() { Frequency = RecurrenceFrequency.Monthly, Interval = 3, ByMonthDay = [10], Until = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc) },
            new() { Frequency = RecurrenceFrequency.Monthly, ByMonthDay = [1, 2, 3, 4, 11, 21, 22, 23] },
            new() { Frequency = RecurrenceFrequency.Yearly, ByMonth = [1] },
            new() { Frequency = RecurrenceFrequency.Yearly, ByMonth = [1, 12], ByMonthDay = [1] },
            new() { Frequency = RecurrenceFrequency.Yearly, ByMonth = [12], ByDay = [RecurrenceDay.SU], BySetPos = [-1] },
            new() { Frequency = RecurrenceFrequency.Daily, Count = 10 },
            new() { Frequency = RecurrenceFrequency.Daily, Interval = 2, Count = 10 },
            new() { Frequency = RecurrenceFrequency.Daily, Until = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc) },
            new() { Frequency = RecurrenceFrequency.Daily, Until = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc) },
            new() { Frequency = RecurrenceFrequency.Weekly, ByDay = [RecurrenceDay.TU, RecurrenceDay.TH], WeekStart = RecurrenceDay.MO },
            new() { Frequency = RecurrenceFrequency.Monthly, Interval = 3, ByMonthDay = [1, 15] },
            new() { Frequency = RecurrenceFrequency.Yearly, Interval = 2, ByMonth = [4], ByDay = [RecurrenceDay.SU], BySetPos = [2] },
            new() { Frequency = RecurrenceFrequency.Yearly, ByMonth = [4], ByMonthDay = [10] },
            new() { Frequency = RecurrenceFrequency.Yearly, ByMonth = [1,7], ByMonthDay = [1, 15] }
        ];

        public static readonly List<string> ExpectedEnglishPhrases =
        [
            "no pattern",
            "every second",
            "every 10 minutes",
            "every 3 hours for 4 occurrences",
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
            "every month on the last Friday",
            "every 5 months on the 4th Sunday",
            "every month on the 2nd and 3rd Monday and Friday",
            "every 3 months on the 10th day until 2025-12-31",
            "every month on the 1st, 2nd, 3rd, 4th, 11th, 21st, 22nd and 23rd days",
            "every year in January",
            "every year on the 1st day in January and December",
            "every year on the last Sunday in December",
            "every day for 10 occurrences",
            "every 2 days for 10 occurrences",
            "every day until 2026-12-31",
            "every day until 2026-12-31",
            "every week on Tuesday and Thursday with week starting Monday",
            "every 3 months on the 1st and 15th days",
            "every 2 years on the 2nd Sunday in April",
            "every year on the 10th day in April",
            "every year on the 1st and 15th days in January and July"
        ];
    }
}
