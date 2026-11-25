namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record WorkStreamModel
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public bool IsPhase { get; init; }

        // The Gantt chart and the resource chart do not show the work stream. It changes nothing else.
        public bool IsHiddenFromCharts { get; init; }

        public int DisplayOrder { get; init; }

        public ColorFormatModel ColorFormat { get; init; } = new ColorFormatModel();
    }
}
