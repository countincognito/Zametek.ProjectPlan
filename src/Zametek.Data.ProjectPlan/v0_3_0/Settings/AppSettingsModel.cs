namespace Zametek.Data.ProjectPlan.v0_3_0
{
    [Serializable]
    public record AppSettingsModel
    {
        public string Version { get; } = Versions.v0_3_0;

        public string ProjectPlanDirectory { get; init; } = string.Empty;

        public bool ShowDates { get; init; } = false;

        public bool UseClassicDates { get; init; } = false;

        public bool UseBusinessDays { get; init; } = true;

        public string SelectedTheme { get; init; } = string.Empty;
    }
}
