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

        /// <summary>
        /// The settings of the schedule simulation. A workbook that has no sheet for them gives the defaults.
        /// </summary>
        public SimulationSettingsModel SimulationSettings { get; init; } = new SimulationSettingsModel();

        /// <summary>
        /// The result that the workbook holds, or null when it holds none. The scenario trusts it, as it trusts the
        /// result that a project file holds.
        /// </summary>
        public SimulationSummaryModel? SimulationSummary { get; init; }
    }
}
