namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record RecurrenceRuleModel
    {
        public RecurrenceFrequency Frequency { get; init; } = RecurrenceFrequency.Daily;

        public int Interval { get; init; } = 1;

        public int? Count { get; init; }

        public DateTime? Until { get; init; }

        public List<RecurrenceDay> ByDay { get; init; } = [];

        public List<int> ByMonthDay { get; init; } = [];

        public List<int> ByMonth { get; init; } = [];

        public List<int> BySetPos { get; init; } = [];

        public RecurrenceDay? WeekStart { get; init; }
    }
}
