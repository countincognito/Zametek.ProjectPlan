namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record AppSettingsModel
    {
        public string Version { get; init; } = string.Empty;

        public string ProjectPlanDirectory { get; init; } = string.Empty;

        public bool DefaultShowDates { get; init; } = false;

        public bool DefaultUseClassicDates { get; init; } = false;

        public bool DefaultUseBusinessDays { get; init; } = true;

        public string SelectedTheme { get; init; } = string.Empty;
    }
}
