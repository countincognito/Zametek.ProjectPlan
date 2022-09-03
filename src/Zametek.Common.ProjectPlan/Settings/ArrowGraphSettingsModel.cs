namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ArrowGraphSettingsModel
    {
        public List<ActivitySeverityModel> ActivitySeverities { get; init; } = new List<ActivitySeverityModel>();

        public List<EdgeTypeFormatModel> EdgeTypeFormats { get; init; } = new List<EdgeTypeFormatModel>();
    }
}
