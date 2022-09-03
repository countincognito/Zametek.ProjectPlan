namespace Zametek.Data.ProjectPlan.v0_3_0
{
    [Serializable]
    public record ArrowGraphSettingsModel
    {
        public List<ActivitySeverityModel> ActivitySeverities { get; init; } = new List<ActivitySeverityModel>();

        public List<v0_1_0.EdgeTypeFormatModel> EdgeTypeFormats { get; init; } = new List<v0_1_0.EdgeTypeFormatModel>();
    }
}
