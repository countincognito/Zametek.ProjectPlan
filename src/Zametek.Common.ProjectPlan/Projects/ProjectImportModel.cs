namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ProjectImportModel
    {
        public DateTimeOffset ProjectStart { get; init; }

        public DateTimeOffset Today { get; init; }

        public List<DependentActivityModel> DependentActivities { get; init; } = [];

        public ResourceSettingsModel ResourceSettings { get; init; } = new ResourceSettingsModel();

        public List<ActivitySeverityModel> ActivitySeverities { get; init; } = [];

        public List<WorkStreamModel> WorkStreams { get; init; } = [];

        public DisplaySettingsModel DisplaySettings { get; init; } = new DisplaySettingsModel();
    }
}
