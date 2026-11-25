namespace Zametek.Data.ProjectPlan.v0_6_2
{
    // v0.6.2 adds IsHiddenFromCharts. The rest is the work stream of v0.3.2.
    [Serializable]
    public record WorkStreamModel
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public bool IsPhase { get; init; }

        // The Gantt chart and the resource chart do not show the work stream. It is new in v0.6.2.
        public bool IsHiddenFromCharts { get; init; }

        public int DisplayOrder { get; init; }

        public v0_1_0.ColorFormatModel ColorFormat { get; init; } = new v0_1_0.ColorFormatModel();
    }
}
