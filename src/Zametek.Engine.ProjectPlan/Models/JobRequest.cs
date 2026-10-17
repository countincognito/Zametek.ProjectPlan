using Zametek.Common.ProjectPlan;

namespace Zametek.Engine.ProjectPlan
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

        // Whether to run the simulation of the schedule once the plan has compiled. It runs with the settings that the scenario
        // holds (the iterations, the distribution, its shape value and the target), and always with the seed that the scenario
        // has saved, also when "New seed each run" is set, so that a run repeats. The result goes to the scenario, and thus a
        // project that the job saves holds it, and its statistics go into the result of the job. A job that does not run the
        // simulation still has the statistics of a result that the scenario holds, and makes the outputs below from it.
        public bool RunSimulation { get; init; }

        // The settings to run with in the place of the scenario's, which the scenario then has. zpp and zpp serve do not set
        // them: they run with the settings of the plan. A host that must run a simulation of a size that it chooses (the
        // warm-up of the server) does, and so does a test.
        public SimulationSettingsModel? SimulationSettings { get; init; }

        // The most iterations that a run of this job may have, when that is fewer than the engine allows. A scenario that has
        // more is a SimulationException of the failure IterationsAboveLimit, before the plan compiles. It is the limit of a
        // server.
        public int? MaximumSimulationIterations { get; init; }

        // JobOutput.SimulationChart, of the result of the scenario: the one that the job made, or else the one that the
        // scenario held. A job with no result of either kind is a SimulationException of the failure NoResult.
        public ChartOutputRequest? SimulationChart { get; init; }

        // JobOutput.SimulationStatistics, SimulationHistogram, SimulationPaths, SimulationActivities and
        // SimulationIterations: each of the CSV files that is set. The iterations of a run are not in a project file, and so
        // the file SimulationIterations is produced only by a job that ran the simulation, and a job that did not skips it.
        public SimulationCsvFile SimulationCsvFiles { get; init; }
    }
}
