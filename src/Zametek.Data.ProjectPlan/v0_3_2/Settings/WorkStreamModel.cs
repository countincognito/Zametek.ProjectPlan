namespace Zametek.Data.ProjectPlan.v0_3_2
{
    [Serializable]
    public record WorkStreamModel
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public bool IsPhase { get; init; }

        public int DisplayOrder { get; init; }

        public v0_1_0.ColorFormatModel ColorFormat { get; init; } = new v0_1_0.ColorFormatModel();
    }
}
