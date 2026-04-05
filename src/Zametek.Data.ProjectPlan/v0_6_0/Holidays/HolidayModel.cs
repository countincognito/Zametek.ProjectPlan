namespace Zametek.Data.ProjectPlan.v0_6_0
{
    [Serializable]
    public record HolidayModel
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public string Notes { get; init; } = string.Empty;

        public string RecurrencePattern { get; init; } = string.Empty;
    }
}
