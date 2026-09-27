using Zametek.Common.ProjectPlan;

namespace Zametek.ProjectPlan.Engine
{
    // One run of a plan: where it comes from, how it is compiled, and which outputs to produce. Each output is produced
    // only when it is asked for, and always in the order JobOutput lists them, whatever order they are set in here.
    public record JobRequest
    {
        // The plan: a project file, or - when ImportFormat is set - a file to import into a new project's Base
        // scenario. The job reads it but does not close it: the stream is the caller's.
        public required Stream Input { get; init; }

        public ProjectScenarioImportFormat? ImportFormat { get; init; }

        // The scenario of a project file to load: by name, by id, or by a unique prefix of its id. When it is not set,
        // the project's current scenario loads. An import has only the one scenario, so it ignores this.
        public string? Scenario { get; init; }

        public BaseTheme BaseTheme { get; init; } = BaseTheme.Light;

        // Milliseconds a compilation may run before it is cancelled, or 0 for no limit.
        public int CompileTimeoutMilliseconds { get; init; } = AppSettingsModel.DefaultCompilationTimeoutMilliseconds;

        // The time the job runs at: the time it stamps on what it creates or modifies, and the day an imported plan
        // starts on. When it is not set, the job reads the host's clock. Either way the time is given in the host's time
        // zone, so the same instant gives the same outputs only on hosts that share one.
        public DateTimeOffset? Now { get; init; }

        // JobOutput.Project: the project, with the scenario the job loaded as its current scenario.
        public bool SaveProject { get; init; }

        // JobOutput.ScenarioExport: the loaded scenario, exported in this format.
        public ProjectScenarioExportFormat? ExportFormat { get; init; }

        public ChartOutputRequest? GanttChart { get; init; }

        public GraphOutputRequest? ArrowGraph { get; init; }

        public GraphOutputRequest? VertexGraph { get; init; }

        public ChartOutputRequest? ResourceChart { get; init; }

        public ChartOutputRequest? EarnedValueChart { get; init; }

        public ChartOutputRequest? ScenarioChart { get; init; }
    }
}
