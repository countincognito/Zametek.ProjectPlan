namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ProjectScenarioImportModel
    {
        public DateTimeOffset ProjectStart { get; init; }

        public DateTimeOffset Today { get; init; }

        public List<DependentActivityModel> DependentActivities { get; init; } = [];

        public ResourceSettingsModel ResourceSettings { get; init; } = new ResourceSettingsModel();

        public List<ActivitySeverityModel> ActivitySeverities { get; init; } = [];

        public List<WorkStreamModel> WorkStreams { get; init; } = [];

        public List<HolidayModel> Holidays { get; init; } = [];

        public ProjectScenarioDisplaySettingsModel DisplaySettings { get; init; } = new ProjectScenarioDisplaySettingsModel();
    }
}
