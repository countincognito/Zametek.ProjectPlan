namespace Zametek.ProjectPlan.CommandLine
{
    // The outputs a request asks for, one member for each kind there is, in the order the job produces them. An output is
    // asked for by giving it - with its settings, if it has any - and is not produced when it is not given.
    public record OutputsOptions
    {
        // --output: the project, as zpp saves it.
        public ProjectOptions? Project { get; init; }

        // --export: the loaded scenario, exported as zpp exports it to .xlsx.
        public ScenarioExportOptions? ScenarioExport { get; init; }

        // --gantt-*
        public ChartOptions? GanttChart { get; init; }

        // --arrow-*
        public GraphOptions? ArrowGraph { get; init; }

        // --vertex-*
        public GraphOptions? VertexGraph { get; init; }

        // --resource-*
        public ChartOptions? ResourceChart { get; init; }

        // --ev-*
        public ChartOptions? EarnedValueChart { get; init; }

        // --scenario-chart-*
        public ChartOptions? ScenarioChart { get; init; }

        // --simulation-chart-*: the chart of the simulation. This and the CSV files that follow are only valid with a
        // simulation.
        public ChartOptions? SimulationChart { get; init; }

        // --simulation-csv-directory writes these five files: the statistics of the finish, its histogram split by critical
        // path, the critical paths, how often each activity was critical, and every iteration.
        public CsvOptions? SimulationStatistics { get; init; }

        public CsvOptions? SimulationHistogram { get; init; }

        public CsvOptions? SimulationPaths { get; init; }

        public CsvOptions? SimulationActivities { get; init; }

        public CsvOptions? SimulationIterations { get; init; }
    }
}
