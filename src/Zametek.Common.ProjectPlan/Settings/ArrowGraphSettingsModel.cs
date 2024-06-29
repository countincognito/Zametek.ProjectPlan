namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ArrowGraphSettingsModel
    {
        public List<ActivitySeverityModel> ActivitySeverities { get; init; } = [];

        public List<EdgeTypeFormatModel> EdgeTypeFormats { get; init; } = [];
    }
}
