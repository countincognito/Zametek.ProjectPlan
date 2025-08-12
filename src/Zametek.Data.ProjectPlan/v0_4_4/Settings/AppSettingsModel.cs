namespace Zametek.Data.ProjectPlan.v0_4_4
{
    [Serializable]
    public record AppSettingsModel
    {
        public string Version { get; } = Versions.v0_4_4;

        public string ProjectPlanDirectory { get; init; } = string.Empty;

        public bool DefaultShowDates { get; init; } = false;

        public bool DefaultUseClassicDates { get; init; } = false;

        public bool DefaultUseBusinessDays { get; init; } = true;

        public bool DefaultHideCost { get; init; } = false;

        public bool DefaultHideBilling { get; init; } = false;

        public string SelectedTheme { get; init; } = string.Empty;
    }
}
