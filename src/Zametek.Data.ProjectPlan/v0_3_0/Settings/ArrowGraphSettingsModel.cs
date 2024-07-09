namespace Zametek.Data.ProjectPlan.v0_3_0
{
    [Serializable]
    public record ArrowGraphSettingsModel
    {
        public List<ActivitySeverityModel> ActivitySeverities { get; init; } = [];

        public List<v0_1_0.EdgeTypeFormatModel> EdgeTypeFormats { get; init; } = [];
    }
}
