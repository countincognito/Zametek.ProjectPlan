using Zametek.Common.ProjectPlan;
using Zametek.Engine.ProjectPlan;
using Zametek.Utility;

namespace Zametek.ProjectPlan.CommandLine
{
    // Turns the options a job was sent with into the engine's request for it, as zpp turns its own options into one,
    // once they are checked against zpp serve's limits; turns zpp's own options into a job's, when zpp sends its run to
    // a server; and names each output the job produces as zpp names its file.
    internal static class JobOptionsHelper
    {
        // The media type of each output, by its file's extension.
        private static readonly Dictionary<string, string> s_ContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [Resource.ProjectPlan.Filters.Filter_ProjectFileExtension] = @"application/json",
            [Resource.ProjectPlan.Filters.Filter_ProjectXlsxFileExtension] = @"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [Resource.ProjectPlan.Filters.Filter_ImageJpegFileExtension] = @"image/jpeg",
            [Resource.ProjectPlan.Filters.Filter_ImagePngFileExtension] = @"image/png",
            [Resource.ProjectPlan.Filters.Filter_ImageBmpFileExtension] = @"image/bmp",
            [Resource.ProjectPlan.Filters.Filter_ImageWebpFileExtension] = @"image/webp",
            [Resource.ProjectPlan.Filters.Filter_ImageSvgFileExtension] = @"image/svg+xml",
            [Resource.ProjectPlan.Filters.Filter_PdfFileExtension] = @"application/pdf",
            [Resource.ProjectPlan.Filters.Filter_GraphMLFileExtension] = @"application/graphml+xml",
            [Resource.ProjectPlan.Filters.Filter_GraphVizFileExtension] = @"text/vnd.graphviz",
        };

        // Why the options cannot run within the limits, or null when they can. What zpp refuses as a usage error, this
        // refuses too; what zpp serve limits beyond that, it refuses here.
        public static string? Validate(
            JobOptions options,
            bool isImport,
            ServeLimits limits)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(limits);

            if (isImport
                && options.Scenario is not null)
            {
                return Resource.ProjectPlan.Messages.Message_ServeScenarioOnlyWithInput;
            }

            // Unlike zpp, a job cannot switch the compilation's limit off: the server's limit is the most it can have.
            if (options.CompileTimeout is int compileTimeout
                && (compileTimeout < 1 || compileTimeout > limits.MaxCompileTimeoutMilliseconds))
            {
                return string.Format(Resource.ProjectPlan.Messages.Message_ServeCompileTimeoutOutOfRange, limits.MaxCompileTimeoutMilliseconds);
            }

            if (options.Now is not null
                && !Program.TryParseNow(options.Now, out _))
            {
                return string.Format(Resource.ProjectPlan.Messages.Message_OptionMustBeADateTimeWithOffset, @"'now'");
            }

            foreach ((string name, ChartOptions? chart) in Charts(options))
            {
                if (chart is not null
                    && (chart.Width < 1 || chart.Width > limits.MaxChartWidth || chart.Height < 1 || chart.Height > limits.MaxChartHeight))
                {
                    return string.Format(Resource.ProjectPlan.Messages.Message_ServeChartSizeOutOfRange, name, limits.MaxChartWidth, limits.MaxChartHeight);
                }
            }

            return null;
        }

        // The engine's request for a job with these options, which Validate has passed. A job that gives no compile
        // timeout has zpp's default, or the server's limit if that is lower.
        public static JobRequest ToJobRequest(
            JobOptions options,
            Stream input,
            ProjectScenarioImportFormat? importFormat,
            ServeLimits limits)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(limits);

            return new JobRequest
            {
                Input = input,
                ImportFormat = importFormat,
                Scenario = options.Scenario,
                BaseTheme = options.BaseTheme,
                CompileTimeoutMilliseconds = options.CompileTimeout
                    ?? Math.Min(AppSettingsModel.DefaultCompilationTimeoutMilliseconds, limits.MaxCompileTimeoutMilliseconds),
                Now = options.Now is not null && Program.TryParseNow(options.Now, out DateTimeOffset now) ? now : null,
                SaveProject = options.Output,
                ExportFormat = options.Export ? ProjectScenarioExportFormat.Xlsx : null,
                GanttChart = ToChartOutputRequest(options.Gantt),
                ArrowGraph = ToGraphOutputRequest(options.Arrow),
                VertexGraph = ToGraphOutputRequest(options.Vertex),
                ResourceChart = ToChartOutputRequest(options.Resource),
                EarnedValueChart = ToChartOutputRequest(options.EV),
                ScenarioChart = ToChartOutputRequest(options.ScenarioChart),
            };
        }

        // The options zpp sends a server for a run with these options of its own: the same, less the paths - where zpp
        // names the file or directory an output goes to, the job asks for the output. ValidateOptions has checked them,
        // and the parser that each size has exactly two values.
        public static JobOptions FromOptions(Options options)
        {
            ArgumentNullException.ThrowIfNull(options);

            return new JobOptions
            {
                Scenario = options.Scenario,
                Output = options.OutputFilename is not null,
                Export = options.ExportFilename is not null,
                BaseTheme = options.BaseTheme,
                MetricsFormat = options.MetricsFormat,
                CompileTimeout = options.CompileTimeoutMilliseconds,
                Now = options.Now,
                Gantt = ToChartOptions(options.GanttDirectory, options.GanttFormat, options.GanttSize),
                Arrow = ToGraphOptions(options.ArrowGraphDirectory, options.ArrowGraphFormat),
                Vertex = ToGraphOptions(options.VertexGraphDirectory, options.VertexGraphFormat),
                Resource = ToChartOptions(options.ResourceDirectory, options.ResourceFormat, options.ResourceSize),
                EV = ToChartOptions(options.EVDirectory, options.EVFormat, options.EVSize),
                ScenarioChart = ToChartOptions(options.ScenarioChartDirectory, options.ScenarioChartFormat, options.ScenarioChartSize),
            };
        }

        // The name zpp gives the output's file: each chart and graph's as zpp names it in its directory, and - where zpp
        // writes them to whichever files --output and --export name - the project's and the scenario export's after the
        // plan they came from.
        public static string BuildOutputFilename(
            JobOutput output,
            JobOptions options,
            string projectTitle)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(projectTitle);

            return output switch
            {
                JobOutput.Project => $@"{projectTitle}.{Resource.ProjectPlan.Filters.Filter_ProjectFileExtension}",
                JobOutput.ScenarioExport => $@"{projectTitle}.{Resource.ProjectPlan.Filters.Filter_ProjectXlsxFileExtension}",
                JobOutput.GanttChart => ChartFilename(projectTitle, Resource.ProjectPlan.Suffixes.Suffix_GanttChart, options.Gantt),
                JobOutput.ArrowGraph => GraphFilename(projectTitle, Resource.ProjectPlan.Suffixes.Suffix_ArrowChart, options.Arrow),
                JobOutput.VertexGraph => GraphFilename(projectTitle, Resource.ProjectPlan.Suffixes.Suffix_VertexChart, options.Vertex),
                JobOutput.ResourceChart => ChartFilename(projectTitle, Resource.ProjectPlan.Suffixes.Suffix_ResourceChart, options.Resource),
                JobOutput.EarnedValueChart => ChartFilename(projectTitle, Resource.ProjectPlan.Suffixes.Suffix_EarnedValueChart, options.EV),
                JobOutput.ScenarioChart => ChartFilename(projectTitle, Resource.ProjectPlan.Suffixes.Suffix_ScenarioChart, options.ScenarioChart),
                _ => throw new ArgumentOutOfRangeException(nameof(output), output, null),
            };
        }

        // The media type of a file the job produced.
        public static string GetContentType(string filename)
        {
            ArgumentNullException.ThrowIfNull(filename);

            return s_ContentTypes.TryGetValue(Path.GetExtension(filename).TrimStart('.'), out string? contentType)
                ? contentType
                : @"application/octet-stream";
        }

        // The charts, by the names the options give them.
        private static IEnumerable<(string Name, ChartOptions? Chart)> Charts(JobOptions options)
        {
            yield return (@"gantt", options.Gantt);
            yield return (@"resource", options.Resource);
            yield return (@"ev", options.EV);
            yield return (@"scenarioChart", options.ScenarioChart);
        }

        private static ChartOptions? ToChartOptions(
            string? directory,
            PlotExport format,
            IEnumerable<int> size)
        {
            if (directory is null)
            {
                return null;
            }

            IList<int> sizeList = [.. size];
            return new ChartOptions { Format = format, Width = sizeList[0], Height = sizeList[1] };
        }

        private static GraphOptions? ToGraphOptions(
            string? directory,
            GraphExport format)
        {
            return directory is null
                ? null
                : new GraphOptions { Format = format };
        }

        private static ChartOutputRequest? ToChartOutputRequest(ChartOptions? chart)
        {
            return chart is null
                ? null
                : new ChartOutputRequest(Program.ToChartImageFormat(chart.Format), chart.Width, chart.Height);
        }

        private static GraphOutputRequest? ToGraphOutputRequest(GraphOptions? graph)
        {
            return graph is null
                ? null
                : new GraphOutputRequest(Program.ToGraphExportFormat(graph.Format));
        }

        private static string ChartFilename(
            string projectTitle,
            string suffix,
            ChartOptions? chart)
        {
            return Program.BuildExportFilename(projectTitle, suffix, (chart?.Format ?? PlotExport.Jpeg).GetDescription());
        }

        private static string GraphFilename(
            string projectTitle,
            string suffix,
            GraphOptions? graph)
        {
            return Program.BuildExportFilename(projectTitle, suffix, (graph?.Format ?? GraphExport.Jpeg).GetDescription());
        }
    }
}
