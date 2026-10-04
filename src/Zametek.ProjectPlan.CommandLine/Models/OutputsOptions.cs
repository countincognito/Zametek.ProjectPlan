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
    }
}
