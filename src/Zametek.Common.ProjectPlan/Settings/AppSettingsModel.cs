namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record AppSettingsModel
    {
        // Referenced by the command line tool as well, so its --compile-timeout
        // option and this setting cannot drift apart.
        public const int DefaultCompilationTimeoutMilliseconds = 5_000;

        public string Version { get; init; } = string.Empty;

        public string ProjectDirectory { get; init; } = string.Empty;

        public bool DefaultShowDates { get; init; } = false;

        public bool DefaultUseClassicDates { get; init; } = false;

        public NonWorkingDayMode DefaultNonWorkingDayMode { get; init; } = default;

        public bool DefaultHideCost { get; init; } = false;

        public bool DefaultHideBilling { get; init; } = false;

        public string SelectedTheme { get; init; } = string.Empty;

        public int MaxRecentProjectFilePaths { get; init; } = 10;

        public List<string> RecentProjectFilePaths { get; init; } = [];

        // How long a graph compilation is allowed to run before it is cancelled, in
        // milliseconds. Zero or less means no limit. There is no UI for this - like
        // the recent file cap above, it is an escape hatch for the settings file, for
        // the rare plan that legitimately needs longer than the default.
        public int CompilationTimeoutMilliseconds { get; init; } = DefaultCompilationTimeoutMilliseconds;
    }
}
