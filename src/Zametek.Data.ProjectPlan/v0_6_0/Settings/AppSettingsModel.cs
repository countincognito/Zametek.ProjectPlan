using Zametek.Common.ProjectPlan;

namespace Zametek.Data.ProjectPlan.v0_6_0
{
    [Serializable]
    public record AppSettingsModel
    {
        public string Version { get; } = Versions.v0_6_0;

        public string ProjectDirectory { get; init; } = string.Empty;

        public bool DefaultShowDates { get; init; } = false;

        public bool DefaultUseClassicDates { get; init; } = false;

        public bool DefaultUseBusinessDays { get; init; } = true;

        public bool DefaultHideCost { get; init; } = false;

        public bool DefaultHideBilling { get; init; } = false;

        public SortMode ProjectPlanSortMode { get; init; } = default;

        public SortDirection ProjectPlanSortDirection { get; init; } = default;

        public string SelectedTheme { get; init; } = string.Empty;
    }
}
