namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record UpdateHolidayModel
    {
        public int Id { get; init; } = default;

        public string Name { get; init; } = string.Empty;
        public bool IsNameEdited { get; init; } = false;

        public string Notes { get; init; } = string.Empty;
        public bool IsNotesEdited { get; init; } = false;

        public string RecurrencePattern { get; init; } = string.Empty;
        public bool IsRecurrencePatternEdited { get; init; } = false;
    }
}
