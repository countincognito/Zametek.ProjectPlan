namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record HolidayModel
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public string Notes { get; init; } = string.Empty;

        public DateTimeOffset? StartDateTime { get; init; }

        public string RecurrencePattern { get; init; } = string.Empty;
    }
}
