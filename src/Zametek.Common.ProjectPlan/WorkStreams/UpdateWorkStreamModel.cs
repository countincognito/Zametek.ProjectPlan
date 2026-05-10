namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record UpdateWorkStreamModel
    {
        public int Id { get; init; } = default;

        public bool IsPhase { get; init; }
        public bool IsIsPhaseEdited { get; init; } = false;

        public ColorFormatModel ColorFormat { get; init; } = new ColorFormatModel();
        public bool IsColorFormatActive { get; init; } = false;
    }
}
