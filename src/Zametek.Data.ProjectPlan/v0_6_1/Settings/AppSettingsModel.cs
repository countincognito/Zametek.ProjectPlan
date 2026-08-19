using Zametek.Common.ProjectPlan;

namespace Zametek.Data.ProjectPlan.v0_6_1
{
    [Serializable]
    public record AppSettingsModel
    {
        public string Version { get; } = Versions.v0_6_1;

        public string ProjectDirectory { get; init; } = string.Empty;

        public bool DefaultShowDates { get; init; } = false;

        public bool DefaultUseClassicDates { get; init; } = false;

        public NonWorkingDayMode DefaultNonWorkingDayMode { get; init; } = default;

        public bool DefaultHideCost { get; init; } = false;

        public bool DefaultHideBilling { get; init; } = false;

        public string SelectedTheme { get; init; } = string.Empty;

        public int MaxRecentProjectFilePaths { get; init; } = 10;

        public List<string> RecentProjectFilePaths { get; init; } = [];

        public int CompilationTimeoutMilliseconds { get; init; } = 5_000;
    }
}
