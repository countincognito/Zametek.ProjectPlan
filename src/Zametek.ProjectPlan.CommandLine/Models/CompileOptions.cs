using Zametek.Common.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    // The options of a request to compile a project on zpp serve, in JSON: zpp's own options, less the paths. Where zpp
    // names the file or directory an output goes to, asking for the output is enough here - the answer carries it - and
    // what is asked for is spelt as it is answered: each member of Outputs is the kind of an output the answer holds.
    public record CompileOptions
    {
        // --scenario: the scenario to load, by name, by id, or by a unique prefix of its id. Only valid with a project
        // sent as project.
        public string? Scenario { get; init; }

        // --base-theme
        public BaseTheme BaseTheme { get; init; } = BaseTheme.Light;

        // --metrics-format: how the console shows the metrics.
        public MetricsExport MetricsFormat { get; init; } = MetricsExport.Markdown;

        // --compile-timeout, as an ISO 8601 duration: at least a millisecond, and at most the server's limit. When it is
        // not given, zpp's default applies, or the server's limit if that is lower.
        public TimeSpan? CompileTimeout { get; init; }

        // --now
        public string? Now { get; init; }

        // What the request asks to have produced.
        public OutputsOptions? Outputs { get; init; }
    }
}
