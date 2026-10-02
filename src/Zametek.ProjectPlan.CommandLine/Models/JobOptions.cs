using Zametek.Common.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    // The options of a job sent to zpp serve, in JSON: zpp's own options, less the paths. Where zpp names the file or
    // directory an output goes to, asking for the output is enough here - the response carries it - and each option
    // keeps zpp's name for it, and its values and defaults.
    public record JobOptions
    {
        // --scenario: the scenario to load, by name, by id, or by a unique prefix of its id. Only valid with a plan sent
        // as input.
        public string? Scenario { get; init; }

        // --output: return the project, as zpp saves it.
        public bool Output { get; init; }

        // --export: return the loaded scenario, exported as zpp exports it to .xlsx.
        public bool Export { get; init; }

        // --base-theme
        public BaseTheme BaseTheme { get; init; } = BaseTheme.Light;

        // --metrics-format
        public MetricsExport MetricsFormat { get; init; } = MetricsExport.Markdown;

        // --compile-timeout, in milliseconds: at least 1, and at most the server's limit. When it is not given, zpp's
        // default applies, or the server's limit if that is lower.
        public int? CompileTimeout { get; init; }

        // --now
        public string? Now { get; init; }

        // --gantt-*
        public ChartOptions? Gantt { get; init; }

        // --arrow-*
        public GraphOptions? Arrow { get; init; }

        // --vertex-*
        public GraphOptions? Vertex { get; init; }

        // --resource-*
        public ChartOptions? Resource { get; init; }

        // --ev-*
        public ChartOptions? EV { get; init; }

        // --scenario-chart-*
        public ChartOptions? ScenarioChart { get; init; }
    }
}
