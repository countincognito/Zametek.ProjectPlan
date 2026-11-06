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

        // --simulation-*: the chart of the result of the simulation, and, with it, its CSV files, which are the statistics of the
        // finish, its histogram split by critical path, the critical paths, and how often each activity was critical. The result
        // is the one that the job made when it ran the simulation (runSimulation), or else the one that the project held. A
        // run also gives the file of its iterations, which a project does not hold. The answer lists each file as an output of
        // its own: simulationChart, simulationStatistics, simulationHistogram, simulationPaths, simulationActivities and
        // simulationIterations. A project with no result, and no run, is refused.
        public ChartOptions? Simulation { get; init; }

        // --simulation-workbook-directory: the result of the simulation as one Excel workbook (<name>-simulation.xlsx): a sheet for the
        // settings and for each of the CSV files. It is independent of the output above, and it comes from the same result. It has the
        // sheet of the iterations only when the job ran the simulation (runSimulation).
        public SimulationWorkbookOptions? SimulationWorkbook { get; init; }
    }
}
