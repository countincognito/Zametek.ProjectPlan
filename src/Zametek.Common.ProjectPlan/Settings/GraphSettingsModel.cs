namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record GraphSettingsModel
    {
        public List<ActivitySeverityModel> ActivitySeverities { get; init; } = [];

        public List<EdgeTypeFormatModel> EdgeTypeFormats { get; init; } = [];
    }
}
