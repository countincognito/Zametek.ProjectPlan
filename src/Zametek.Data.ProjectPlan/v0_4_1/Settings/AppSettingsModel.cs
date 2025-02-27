namespace Zametek.Data.ProjectPlan.v0_4_1
{
    [Serializable]
    public record AppSettingsModel
    {
        public string Version { get; } = Versions.v0_4_1;

        public string ProjectPlanDirectory { get; init; } = string.Empty;

        public bool DefaultShowDates { get; init; } = false;

        public bool DefaultUseClassicDates { get; init; } = false;

        public bool DefaultUseBusinessDays { get; init; } = true;

        public string SelectedTheme { get; init; } = string.Empty;
    }
}
