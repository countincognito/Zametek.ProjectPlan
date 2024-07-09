namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    public record ArrowGraphSettingsModel
    {
        public List<ActivitySeverityModel> ActivitySeverities { get; init; } = [];

        public List<EdgeTypeFormatModel> EdgeTypeFormats { get; init; } = [];
    }
}
