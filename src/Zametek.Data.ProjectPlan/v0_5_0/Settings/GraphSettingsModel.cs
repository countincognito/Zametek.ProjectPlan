namespace Zametek.Data.ProjectPlan.v0_5_0
{
    [Serializable]
    public record GraphSettingsModel
    {
        public List<v0_3_0.ActivitySeverityModel> ActivitySeverities { get; init; } = [];

        public List<v0_1_0.EdgeTypeFormatModel> EdgeTypeFormats { get; init; } = [];

        public List<NodeTypeFormatModel> NodeTypeFormats { get; init; } = [];
    }
}
