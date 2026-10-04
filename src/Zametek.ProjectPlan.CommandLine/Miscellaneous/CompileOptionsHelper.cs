using System.Globalization;
using System.Text.Json;
using Zametek.Common.ProjectPlan;
using Zametek.Engine.ProjectPlan;
using Zametek.Utility;

namespace Zametek.ProjectPlan.CommandLine
{
    // Reads the options a request to compile a project was sent with - every problem with them, not the first - and turns
    // them into the engine's request, as zpp turns its own options into one, once they are checked against zpp serve's
    // limits; turns zpp's own options into a request's, when zpp sends its run to a server; and names each output the
    // request produces as zpp names its file.
    internal static class CompileOptionsHelper
    {
        // The most the options of a request may be: a few lines of JSON, and nested a few levels at most.
        public const int MaxOptionsBytes = 64 * 1024;

        // Where the options are in a request, as a JSON pointer: the request, as OpenAPI models a multipart body, is an
        // object whose properties are its parts.
        public const string OptionsPointer = @"#/options";

        private const int c_MaxDepth = 16;

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

        // A pointer to a member of what a pointer points to, as RFC 6901 writes it - ~ and / in the name escaped - in URI
        // fragment form.
        public static string GetPointer(
            string parent,
            string name)
        {
            ArgumentNullException.ThrowIfNull(parent);
            ArgumentNullException.ThrowIfNull(name);

            return $@"{parent}/{Uri.EscapeDataString(name.Replace(@"~", @"~0", StringComparison.Ordinal).Replace(@"/", @"~1", StringComparison.Ordinal))}";
        }

        // The options the JSON holds - or zpp's defaults, when there is none. Whatever is wrong with them is added to the
        // problems, and when any is, there are no options: the JSON is not JSON (a problem with the request as sent), or it
        // names what the endpoint does not take, or gives a member what it does not take, or goes beyond the limits, or asks
        // for what zpp refuses as a usage error.
        public static CompileOptions? Read(
            string? json,
            bool isImport,
            ServeLimits limits,
            ProblemCollector problems)
        {
            ArgumentNullException.ThrowIfNull(limits);
            ArgumentNullException.ThrowIfNull(problems);

            if (json is null)
            {
                return new CompileOptions();
            }

            JsonDocument document;

            try
            {
                document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = c_MaxDepth });
            }
            catch (JsonException ex)
            {
                problems.AddMalformed(Error(
                    OptionsPointer,
                    ProblemCodes.InvalidFormat,
                    string.Format(CultureInfo.CurrentCulture, Resource.ProjectPlan.Messages.Message_ServeErrorMustBeJson, (ex.LineNumber ?? 0) + 1, (ex.BytePositionInLine ?? 0) + 1)));
                return null;
            }

            using (document)
            {
                return ReadOptions(document.RootElement, isImport, limits, problems);
            }
        }

        // The engine's request for a job with these options, which Read has passed. A job that gives no compile timeout has
        // zpp's default, or the server's limit if that is lower.
        public static JobRequest ToJobRequest(
            CompileOptions options,
            Stream input,
            ProjectScenarioImportFormat? importFormat,
            ServeLimits limits)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(limits);

            OutputsOptions outputs = options.Outputs ?? new OutputsOptions();

            return new JobRequest
            {
                Input = input,
                ImportFormat = importFormat,
                Scenario = options.Scenario,
                BaseTheme = options.BaseTheme,
                CompileTimeoutMilliseconds = options.CompileTimeout is TimeSpan timeout
                    ? Math.Max(1, (int)timeout.TotalMilliseconds)
                    : Math.Min(AppSettingsModel.DefaultCompilationTimeoutMilliseconds, limits.MaxCompileTimeoutMilliseconds),
                Now = options.Now is not null && Program.TryParseNow(options.Now, out DateTimeOffset now) ? now : null,
                SaveProject = outputs.Project is not null,
                ExportFormat = outputs.ScenarioExport is not null ? ProjectScenarioExportFormat.Xlsx : null,
                GanttChart = ToChartOutputRequest(outputs.GanttChart),
                ArrowGraph = ToGraphOutputRequest(outputs.ArrowGraph),
                VertexGraph = ToGraphOutputRequest(outputs.VertexGraph),
                ResourceChart = ToChartOutputRequest(outputs.ResourceChart),
                EarnedValueChart = ToChartOutputRequest(outputs.EarnedValueChart),
                ScenarioChart = ToChartOutputRequest(outputs.ScenarioChart),
            };
        }

        // The options zpp sends a server for a run with these options of its own: the same, less the paths - where zpp
        // names the file or directory an output goes to, the request asks for the output. ValidateOptions has checked them,
        // and the parser that each size has exactly two values.
        public static CompileOptions FromOptions(Options options)
        {
            ArgumentNullException.ThrowIfNull(options);

            var outputs = new OutputsOptions
            {
                Project = options.OutputFilename is not null ? new ProjectOptions() : null,
                ScenarioExport = options.ExportFilename is not null ? new ScenarioExportOptions() : null,
                GanttChart = ToChartOptions(options.GanttDirectory, options.GanttFormat, options.GanttSize),
                ArrowGraph = ToGraphOptions(options.ArrowGraphDirectory, options.ArrowGraphFormat),
                VertexGraph = ToGraphOptions(options.VertexGraphDirectory, options.VertexGraphFormat),
                ResourceChart = ToChartOptions(options.ResourceDirectory, options.ResourceFormat, options.ResourceSize),
                EarnedValueChart = ToChartOptions(options.EVDirectory, options.EVFormat, options.EVSize),
                ScenarioChart = ToChartOptions(options.ScenarioChartDirectory, options.ScenarioChartFormat, options.ScenarioChartSize),
            };

            return new CompileOptions
            {
                Scenario = options.Scenario,
                BaseTheme = options.BaseTheme,
                MetricsFormat = options.MetricsFormat,
                CompileTimeout = TimeSpan.FromMilliseconds(options.CompileTimeoutMilliseconds),
                Now = options.Now,
                Outputs = outputs == new OutputsOptions() ? null : outputs,
            };
        }

        // The kinds of output the options ask for, in the order the job produces them.
        public static IReadOnlyList<JobOutput> GetRequestedOutputs(CompileOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            OutputsOptions outputs = options.Outputs ?? new OutputsOptions();
            var requested = new List<JobOutput>();

            AddIf(requested, outputs.Project, JobOutput.Project);
            AddIf(requested, outputs.ScenarioExport, JobOutput.ScenarioExport);
            AddIf(requested, outputs.GanttChart, JobOutput.GanttChart);
            AddIf(requested, outputs.ArrowGraph, JobOutput.ArrowGraph);
            AddIf(requested, outputs.VertexGraph, JobOutput.VertexGraph);
            AddIf(requested, outputs.ResourceChart, JobOutput.ResourceChart);
            AddIf(requested, outputs.EarnedValueChart, JobOutput.EarnedValueChart);
            AddIf(requested, outputs.ScenarioChart, JobOutput.ScenarioChart);

            return requested;
        }

        // The name zpp gives the output's file: each chart and graph's as zpp names it in its directory, and - where zpp
        // writes them to whichever files --output and --export name - the project's and the scenario export's after the
        // plan they came from.
        public static string BuildOutputFilename(
            JobOutput output,
            CompileOptions options,
            string projectTitle)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(projectTitle);

            OutputsOptions outputs = options.Outputs ?? new OutputsOptions();

            return output switch
            {
                JobOutput.Project => $@"{projectTitle}.{Resource.ProjectPlan.Filters.Filter_ProjectFileExtension}",
                JobOutput.ScenarioExport => $@"{projectTitle}.{Resource.ProjectPlan.Filters.Filter_ProjectXlsxFileExtension}",
                JobOutput.GanttChart => ChartFilename(projectTitle, Resource.ProjectPlan.Suffixes.Suffix_GanttChart, outputs.GanttChart),
                JobOutput.ArrowGraph => GraphFilename(projectTitle, Resource.ProjectPlan.Suffixes.Suffix_ArrowChart, outputs.ArrowGraph),
                JobOutput.VertexGraph => GraphFilename(projectTitle, Resource.ProjectPlan.Suffixes.Suffix_VertexChart, outputs.VertexGraph),
                JobOutput.ResourceChart => ChartFilename(projectTitle, Resource.ProjectPlan.Suffixes.Suffix_ResourceChart, outputs.ResourceChart),
                JobOutput.EarnedValueChart => ChartFilename(projectTitle, Resource.ProjectPlan.Suffixes.Suffix_EarnedValueChart, outputs.EarnedValueChart),
                JobOutput.ScenarioChart => ChartFilename(projectTitle, Resource.ProjectPlan.Suffixes.Suffix_ScenarioChart, outputs.ScenarioChart),
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

        // The name an output's kind goes by in a request and in an answer: camelCase, as the API names things.
        public static string GetOutputName(JobOutput output)
        {
            return ApiNamingPolicy.Instance.ConvertName(output.ToString());
        }

        #region Reading

        private static CompileOptions? ReadOptions(
            JsonElement root,
            bool isImport,
            ServeLimits limits,
            ProblemCollector problems)
        {
            if (root.ValueKind != JsonValueKind.Object)
            {
                problems.AddInvalid(Error(OptionsPointer, ProblemCodes.WrongType, Resource.ProjectPlan.Messages.Message_ServeErrorMustBeAnObject));
                return null;
            }

            int before = problems.Errors.Count;
            string? scenario = null;
            BaseTheme baseTheme = BaseTheme.Light;
            MetricsExport metricsFormat = MetricsExport.Markdown;
            TimeSpan? compileTimeout = null;
            string? now = null;
            OutputsOptions? outputs = null;

            foreach (JsonProperty member in root.EnumerateObject())
            {
                string pointer = GetPointer(OptionsPointer, member.Name);

                switch (member.Name)
                {
                    case @"scenario":
                        scenario = ReadString(member.Value, pointer, problems);

                        // As zpp's --scenario is only valid with --input.
                        if (scenario is not null
                            && isImport)
                        {
                            problems.AddInvalid(Error(pointer, ProblemCodes.NotAllowedWithImport, Resource.ProjectPlan.Messages.Message_ServeErrorOnlyWithProject));
                        }
                        break;
                    case @"baseTheme":
                        baseTheme = ReadEnum(member.Value, pointer, BaseTheme.Light, problems);
                        break;
                    case @"metricsFormat":
                        metricsFormat = ReadEnum(member.Value, pointer, MetricsExport.Markdown, problems);
                        break;
                    case @"compileTimeout":
                        compileTimeout = ReadCompileTimeout(member.Value, pointer, limits, problems);
                        break;
                    case @"now":
                        now = ReadNow(member.Value, pointer, problems);
                        break;
                    case @"outputs":
                        outputs = ReadOutputs(member.Value, pointer, limits, problems);
                        break;
                    default:
                        problems.AddInvalid(Error(pointer, ProblemCodes.UnknownProperty, Resource.ProjectPlan.Messages.Message_ServeErrorNotKnown));
                        break;
                }
            }

            return problems.Errors.Count == before
                ? new CompileOptions
                {
                    Scenario = scenario,
                    BaseTheme = baseTheme,
                    MetricsFormat = metricsFormat,
                    CompileTimeout = compileTimeout,
                    Now = now,
                    Outputs = outputs,
                }
                : null;
        }

        private static OutputsOptions? ReadOutputs(
            JsonElement value,
            string pointer,
            ServeLimits limits,
            ProblemCollector problems)
        {
            if (value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (value.ValueKind != JsonValueKind.Object)
            {
                problems.AddInvalid(Error(pointer, ProblemCodes.WrongType, Resource.ProjectPlan.Messages.Message_ServeErrorMustBeAnObject));
                return null;
            }

            ProjectOptions? project = null;
            ScenarioExportOptions? scenarioExport = null;
            ChartOptions? ganttChart = null;
            GraphOptions? arrowGraph = null;
            GraphOptions? vertexGraph = null;
            ChartOptions? resourceChart = null;
            ChartOptions? earnedValueChart = null;
            ChartOptions? scenarioChart = null;

            foreach (JsonProperty member in value.EnumerateObject())
            {
                string memberPointer = GetPointer(pointer, member.Name);

                switch (member.Name)
                {
                    case @"project":
                        project = ReadEmptyObject(member.Value, memberPointer, problems) ? new ProjectOptions() : null;
                        break;
                    case @"scenarioExport":
                        scenarioExport = ReadEmptyObject(member.Value, memberPointer, problems) ? new ScenarioExportOptions() : null;
                        break;
                    case @"ganttChart":
                        ganttChart = ReadChart(member.Value, memberPointer, limits, problems);
                        break;
                    case @"arrowGraph":
                        arrowGraph = ReadGraph(member.Value, memberPointer, problems);
                        break;
                    case @"vertexGraph":
                        vertexGraph = ReadGraph(member.Value, memberPointer, problems);
                        break;
                    case @"resourceChart":
                        resourceChart = ReadChart(member.Value, memberPointer, limits, problems);
                        break;
                    case @"earnedValueChart":
                        earnedValueChart = ReadChart(member.Value, memberPointer, limits, problems);
                        break;
                    case @"scenarioChart":
                        scenarioChart = ReadChart(member.Value, memberPointer, limits, problems);
                        break;
                    default:
                        problems.AddInvalid(Error(memberPointer, ProblemCodes.UnknownProperty, Resource.ProjectPlan.Messages.Message_ServeErrorNotKnown));
                        break;
                }
            }

            return new OutputsOptions
            {
                Project = project,
                ScenarioExport = scenarioExport,
                GanttChart = ganttChart,
                ArrowGraph = arrowGraph,
                VertexGraph = vertexGraph,
                ResourceChart = resourceChart,
                EarnedValueChart = earnedValueChart,
                ScenarioChart = scenarioChart,
            };
        }

        // An output that has no settings: an object, which is empty. Whether it is asked for.
        private static bool ReadEmptyObject(
            JsonElement value,
            string pointer,
            ProblemCollector problems)
        {
            if (value.ValueKind == JsonValueKind.Null)
            {
                return false;
            }

            if (value.ValueKind != JsonValueKind.Object)
            {
                problems.AddInvalid(Error(pointer, ProblemCodes.WrongType, Resource.ProjectPlan.Messages.Message_ServeErrorMustBeAnObject));
                return false;
            }

            foreach (JsonProperty member in value.EnumerateObject())
            {
                problems.AddInvalid(Error(GetPointer(pointer, member.Name), ProblemCodes.UnknownProperty, Resource.ProjectPlan.Messages.Message_ServeErrorNotKnown));
            }

            return true;
        }

        private static ChartOptions? ReadChart(
            JsonElement value,
            string pointer,
            ServeLimits limits,
            ProblemCollector problems)
        {
            if (value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (value.ValueKind != JsonValueKind.Object)
            {
                problems.AddInvalid(Error(pointer, ProblemCodes.WrongType, Resource.ProjectPlan.Messages.Message_ServeErrorMustBeAnObject));
                return null;
            }

            PlotExport format = PlotExport.Jpeg;
            int? width = null;
            int? height = null;
            bool hasWidth = false;
            bool hasHeight = false;

            foreach (JsonProperty member in value.EnumerateObject())
            {
                string memberPointer = GetPointer(pointer, member.Name);

                switch (member.Name)
                {
                    case @"format":
                        format = ReadEnum(member.Value, memberPointer, PlotExport.Jpeg, problems);
                        break;
                    case @"width":
                        hasWidth = member.Value.ValueKind != JsonValueKind.Null;
                        width = ReadPixels(member.Value, memberPointer, limits.MaxChartWidth, problems);
                        break;
                    case @"height":
                        hasHeight = member.Value.ValueKind != JsonValueKind.Null;
                        height = ReadPixels(member.Value, memberPointer, limits.MaxChartHeight, problems);
                        break;
                    default:
                        problems.AddInvalid(Error(memberPointer, ProblemCodes.UnknownProperty, Resource.ProjectPlan.Messages.Message_ServeErrorNotKnown));
                        break;
                }
            }

            // A chart needs its size, as zpp's --*-size is required with its directory.
            if (!hasWidth)
            {
                problems.AddInvalid(Error(GetPointer(pointer, @"width"), ProblemCodes.Required, Resource.ProjectPlan.Messages.Message_ServeErrorRequired));
            }
            if (!hasHeight)
            {
                problems.AddInvalid(Error(GetPointer(pointer, @"height"), ProblemCodes.Required, Resource.ProjectPlan.Messages.Message_ServeErrorRequired));
            }

            return new ChartOptions { Format = format, Width = width ?? 0, Height = height ?? 0 };
        }

        private static GraphOptions? ReadGraph(
            JsonElement value,
            string pointer,
            ProblemCollector problems)
        {
            if (value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (value.ValueKind != JsonValueKind.Object)
            {
                problems.AddInvalid(Error(pointer, ProblemCodes.WrongType, Resource.ProjectPlan.Messages.Message_ServeErrorMustBeAnObject));
                return null;
            }

            GraphExport format = GraphExport.Jpeg;

            foreach (JsonProperty member in value.EnumerateObject())
            {
                string memberPointer = GetPointer(pointer, member.Name);

                if (member.Name == @"format")
                {
                    format = ReadEnum(member.Value, memberPointer, GraphExport.Jpeg, problems);
                }
                else
                {
                    problems.AddInvalid(Error(memberPointer, ProblemCodes.UnknownProperty, Resource.ProjectPlan.Messages.Message_ServeErrorNotKnown));
                }
            }

            return new GraphOptions { Format = format };
        }

        // A number of pixels, from 1 to the most the server allows.
        private static int? ReadPixels(
            JsonElement value,
            string pointer,
            int most,
            ProblemCollector problems)
        {
            if (value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (value.ValueKind != JsonValueKind.Number
                || !value.TryGetInt32(out int pixels))
            {
                problems.AddInvalid(Error(pointer, ProblemCodes.WrongType, Resource.ProjectPlan.Messages.Message_ServeErrorMustBeAWholeNumber));
                return null;
            }

            if (pixels < 1
                || pixels > most)
            {
                problems.AddInvalid(Error(pointer, ProblemCodes.OutOfRange, string.Format(CultureInfo.CurrentCulture, Resource.ProjectPlan.Messages.Message_ServeErrorPixelsOutOfRange, most)));
                return null;
            }

            return pixels;
        }

        private static string? ReadString(
            JsonElement value,
            string pointer,
            ProblemCollector problems)
        {
            if (value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (value.ValueKind != JsonValueKind.String)
            {
                problems.AddInvalid(Error(pointer, ProblemCodes.WrongType, Resource.ProjectPlan.Messages.Message_ServeErrorMustBeAString));
                return null;
            }

            return value.GetString();
        }

        // One of the enum's values, by the name the API gives it; the fallback when the member is null or wrong.
        private static TEnum ReadEnum<TEnum>(
            JsonElement value,
            string pointer,
            TEnum fallback,
            ProblemCollector problems)
            where TEnum : struct, Enum
        {
            if (value.ValueKind == JsonValueKind.Null)
            {
                return fallback;
            }

            string[] names = [.. Enum.GetValues<TEnum>().Select(x => ApiNamingPolicy.Instance.ConvertName(x.ToString()))];

            if (value.ValueKind == JsonValueKind.String)
            {
                string? text = value.GetString();

                foreach (TEnum candidate in Enum.GetValues<TEnum>())
                {
                    if (string.Equals(text, ApiNamingPolicy.Instance.ConvertName(candidate.ToString()), StringComparison.Ordinal))
                    {
                        return candidate;
                    }
                }
            }

            problems.AddInvalid(Error(
                pointer,
                value.ValueKind == JsonValueKind.String ? ProblemCodes.NotAllowed : ProblemCodes.WrongType,
                string.Format(CultureInfo.CurrentCulture, Resource.ProjectPlan.Messages.Message_ServeErrorMustBeOneOf, string.Join(@", ", names))));
            return fallback;
        }

        // The compile timeout: an ISO 8601 duration of at least a millisecond, and at most the server's limit - a job cannot
        // switch the limit off.
        private static TimeSpan? ReadCompileTimeout(
            JsonElement value,
            string pointer,
            ServeLimits limits,
            ProblemCollector problems)
        {
            if (value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (value.ValueKind != JsonValueKind.String)
            {
                problems.AddInvalid(Error(pointer, ProblemCodes.WrongType, Resource.ProjectPlan.Messages.Message_ServeErrorMustBeADuration));
                return null;
            }

            if (!IsoDurationHelper.TryParse(value.GetString(), out TimeSpan duration))
            {
                problems.AddInvalid(Error(pointer, ProblemCodes.InvalidFormat, Resource.ProjectPlan.Messages.Message_ServeErrorMustBeADuration));
                return null;
            }

            var shortest = TimeSpan.FromMilliseconds(1);
            var longest = TimeSpan.FromMilliseconds(limits.MaxCompileTimeoutMilliseconds);

            if (duration < shortest
                || duration > longest)
            {
                problems.AddInvalid(Error(
                    pointer,
                    ProblemCodes.OutOfRange,
                    string.Format(CultureInfo.CurrentCulture, Resource.ProjectPlan.Messages.Message_ServeErrorDurationOutOfRange, IsoDurationHelper.ToString(shortest), IsoDurationHelper.ToString(longest))));
                return null;
            }

            return duration;
        }

        // The time the job runs at, as zpp's --now takes it: a time with its offset from UTC.
        private static string? ReadNow(
            JsonElement value,
            string pointer,
            ProblemCollector problems)
        {
            string? text = ReadString(value, pointer, problems);

            if (text is not null
                && !Program.TryParseNow(text, out _))
            {
                problems.AddInvalid(Error(pointer, ProblemCodes.InvalidFormat, Resource.ProjectPlan.Messages.Message_ServeErrorMustBeADateTimeWithOffset));
                return null;
            }

            return text;
        }

        private static ProblemError Error(
            string pointer,
            string code,
            string detail)
        {
            return new ProblemError { Pointer = pointer, Code = code, Detail = detail };
        }

        #endregion

        private static void AddIf(
            List<JobOutput> requested,
            object? options,
            JobOutput output)
        {
            if (options is not null)
            {
                requested.Add(output);
            }
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
