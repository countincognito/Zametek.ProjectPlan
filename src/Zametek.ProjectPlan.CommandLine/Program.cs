using CommandLine;
using CommandLine.Text;
using ConsoleTables;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Serilog;
using Serilog.Events;
using System.Reflection;
using Zametek.Common.ProjectPlan;
using Zametek.ProjectPlan.Engine;
using Zametek.Utility;
using Zametek.ViewModel.ProjectPlan;

// Using these as a starting point:
// https://github.com/jasonterando/dotnet-console-demo/
// https://medium.com/@eduardosilva_94960/mastering-command-line-parsing-in-net-core-with-commandlineparser-c20721100359
namespace Zametek.ProjectPlan.CommandLine
{
    public class Program
    {
        // Exit codes are part of the CLI contract: scripts and CI gates branch on
        // them. 0 = success, 1 = runtime failure (bad paths, unreadable files,
        // outputs that could not be written, unexpected errors), 2 = bad usage
        // (invalid options or combinations), 3 = the project compiled with
        // errors - kept distinct from 1 so a pipeline can tell a broken plan from
        // a broken invocation - and 4 = a compilation ran past --compile-timeout
        // and was cancelled, which says nothing about whether the plan is valid,
        // only that it did not finish.
        private const int c_ExitSuccess = 0;
        private const int c_ExitFailure = 1;
        private const int c_ExitUsageError = 2;
        private const int c_ExitCompilationErrors = 3;
        private const int c_ExitCompilationTimeout = 4;

        public static async Task<int> Main(string[] args)
        {
            try
            {
                using var parser = new Parser(with =>
                {
                    with.CaseInsensitiveEnumValues = true;
                    with.HelpWriter = null;

                    // This needs to be included to prevent the --version option.
                    with.AutoVersion = false;
                });

                ParserResult<Options> parserResult = parser.ParseArguments<Options>(args);

                return await parserResult.MapResult(
                    async options =>
                    {
                        ConfigureSerilog(options.Verbose);

                        // Before the container exists, because the view models the
                        // engine runs depend on it.
                        ProjectPlanEngine.Initialize();

                        // The container is built only once the options parse, so
                        // help and usage errors never pay for it. The run is one
                        // job, which the engine gives a scope of its own.
                        await using ServiceProvider services = BuildServices();

                        return await RunAsync(options, services.GetRequiredService<JobRunner>());
                    },
                    errs => Task.FromResult(OnParseErrors(parserResult, errs)));
            }
            catch (UsageException ex)
            {
                await Console.Error.WriteLineAsync(ex.Message);
                return c_ExitUsageError;
            }
            catch (ScenarioSelectionException ex)
            {
                await Console.Error.WriteLineAsync(BuildScenarioSelectionMessage(ex));
                return c_ExitFailure;
            }
            catch (GraphCompilationTimeoutException ex)
            {
                await Console.Error.WriteLineAsync(ex.Message);
                return c_ExitCompilationTimeout;
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync(ex.Message);
                return c_ExitFailure;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static void ConfigureSerilog(bool verbose)
        {
            // Everything goes to stderr so stdout stays clean for parseable output
            // (the metrics table or JSON). Warnings and errors always show - this
            // is where the view models' ILogger<T> output surfaces, via the
            // AddSerilog registration in BuildServices - and --verbose lowers the
            // threshold to their informational lifecycle logging.
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(verbose ? LogEventLevel.Information : LogEventLevel.Warning)
                .WriteTo.Console(
                    standardErrorFromLevel: LogEventLevel.Verbose,
                    formatProvider: System.Globalization.CultureInfo.InvariantCulture)
                .CreateLogger();
        }

        private static ServiceProvider BuildServices()
        {
            return new ServiceCollection()
                .AddProjectPlanEngine()
                .AddSerilog()
                .BuildServiceProvider();
        }

        private static async Task<int> RunAsync(
            Options options,
            JobRunner jobRunner)
        {
            ValidateOptions(options);

            // File in: a project file, or a file to import. The parser's file-in
            // group requires one of the two, and ValidateOptions rejects both.
            string inputFilename = options.InputFilename ?? options.ImportFilename!;
            ProjectScenarioImportFormat? importFormat = options.ImportFilename is null
                ? null
                : FileFormatHelper.GetProjectScenarioImportFormat(inputFilename);

            await using FileStream input = importFormat is ProjectScenarioImportFormat format
                ? FileStreamHelper.OpenImportFile(inputFilename, format)
                : File.OpenRead(inputFilename);

            if (options.ListScenarios)
            {
                DisplayScenarios(await jobRunner.ListScenariosAsync(input));
                return c_ExitSuccess;
            }

            // An export's format comes from its file's extension. Like the export
            // directories, it is checked before the plan is processed, so that a
            // bad name fails the run before any file has been written.
            ProjectScenarioExportFormat? exportFormat = options.ExportFilename is null
                ? null
                : FileFormatHelper.GetProjectScenarioExportFormat(options.ExportFilename);

            var request = new JobRequest
            {
                Input = input,
                ImportFormat = importFormat,
                Scenario = options.Scenario,
                BaseTheme = options.BaseTheme,
                CompileTimeoutMilliseconds = options.CompileTimeoutMilliseconds,
                SaveProject = options.OutputFilename is not null,
                ExportFormat = exportFormat,
                GanttChart = ToChartOutputRequest(options.GanttDirectory, options.GanttFormat, options.GanttSize),
                ArrowGraph = ToGraphOutputRequest(options.ArrowGraphDirectory, options.ArrowGraphFormat),
                VertexGraph = ToGraphOutputRequest(options.VertexGraphDirectory, options.VertexGraphFormat),
                ResourceChart = ToChartOutputRequest(options.ResourceDirectory, options.ResourceFormat, options.ResourceSize),
                EarnedValueChart = ToChartOutputRequest(options.EVDirectory, options.EVFormat, options.EVSize),
                ScenarioChart = ToChartOutputRequest(options.ScenarioChartDirectory, options.ScenarioChartFormat, options.ScenarioChartSize),
            };

            // Chart and graph files are named after the file the plan came from,
            // not after wherever its results are saved.
            string projectTitle = SettingServiceBase.GetProjectTitle(inputFilename);

            JobResult result = await jobRunner.RunAsync(request, new FileJobSink(BuildOutputFilenames(options, projectTitle)));

            if (result.Status == JobStatus.CompilationErrors)
            {
                Display(result.CompilationOutput, hasErrors: true);
                return c_ExitCompilationErrors;
            }

            // Metrics. A plan that compiled always has them.
            {
                JobMetrics metrics = result.Metrics ?? throw new InvalidOperationException();

                switch (options.MetricsFormat)
                {
                    case MetricsExport.Json:
                        // Machine output: undecorated, no colours, no leading
                        // blank line, so it can be piped straight into a parser.
                        StandardOutput.WriteLine(BuildMetricsJson(metrics));
                        break;
                    case MetricsExport.Table:
                        Display(BuildMetricsTable(metrics).ToString());
                        break;
                    case MetricsExport.Markdown:
                    default:
                        Display(BuildMetricsTable(metrics).ToMarkDownString());
                        break;
                }
            }

            // A chart or graph export that fails does not throw: the job reports
            // the failure through its sink, as the desktop reports it in a dialog.
            // The remaining outputs have still been produced and the metrics
            // printed, but the run as a whole has failed.
            return result.Status == JobStatus.CompletedWithErrors ? c_ExitFailure : c_ExitSuccess;
        }

        private static ConsoleTable BuildMetricsTable(JobMetrics metrics)
        {
            var table = new ConsoleTable(Resource.ProjectPlan.Titles.Title_Metrics, Resource.ProjectPlan.Titles.Title_Values);

            table.AddRow(Resource.ProjectPlan.Labels.Label_ActivityRisk, $@"{metrics.ActivityRisk:F2}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_ActivityRiskWithStdDevCorrection, $@"{metrics.ActivityRiskWithStdDevCorrection:F2}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_CriticalityRisk, $@"{metrics.CriticalityRisk:F2}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_FibonacciRisk, $@"{metrics.FibonacciRisk:F2}");

            table.AddRow(Resource.ProjectPlan.Labels.Label_GeometricActivityRisk, $@"{metrics.GeometricActivityRisk:F2}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_GeometricCriticalityRisk, $@"{metrics.GeometricCriticalityRisk:F2}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_GeometricFibonacciRisk, $@"{metrics.GeometricFibonacciRisk:F2}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_CyclomaticComplexity, $@"{metrics.NetworkCyclomaticComplexity}");

            table.AddRow(Resource.ProjectPlan.Labels.Label_ActivityEffort, $@"{metrics.ActivityEffort:F0}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_DurationManMonths, $@"{metrics.NetworkDurationManMonths:F1}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_ProjectFinish, $@"{metrics.ProjectFinish}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_EffortEfficiency, $@"{metrics.EffortEfficiency:F3}");

            table.AddRow(Resource.ProjectPlan.Labels.Label_DirectEffort, $@"{metrics.DirectEffort:F0}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_IndirectEffort, $@"{metrics.IndirectEffort:F0}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_OtherEffort, $@"{metrics.OtherEffort:F0}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_TotalEffort, $@"{metrics.TotalEffort:F0}");

            table.AddRow(Resource.ProjectPlan.Labels.Label_DirectCost, $@"{metrics.DirectCost:F2}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_IndirectCost, $@"{metrics.IndirectCost:F2}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_OtherCost, $@"{metrics.OtherCost:F2}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_TotalCost, $@"{metrics.TotalCost:F2}");

            table.AddRow(Resource.ProjectPlan.Labels.Label_DirectBilling, $@"{metrics.DirectBilling:F2}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_IndirectBilling, $@"{metrics.IndirectBilling:F2}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_OtherBilling, $@"{metrics.OtherBilling:F2}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_TotalBilling, $@"{metrics.TotalBilling:F2}");

            table.AddRow(Resource.ProjectPlan.Labels.Label_DirectMargin, $@"{metrics.DirectMarginAbsolute:F2}{metrics.DisplayDirectMargin}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_IndirectMargin, $@"{metrics.IndirectMarginAbsolute:F2}{metrics.DisplayIndirectMargin}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_OtherMargin, $@"{metrics.OtherMarginAbsolute:F2}{metrics.DisplayOtherMargin}");
            table.AddRow(Resource.ProjectPlan.Labels.Label_TotalMargin, $@"{metrics.TotalMarginAbsolute:F2}{metrics.DisplayTotalMargin}");

            table.Configure(x =>
            {
                x.NumberAlignment = Alignment.Left;
                x.EnableCount = false;
            });

            return table;
        }

        private static string BuildMetricsJson(JobMetrics metrics)
        {
            // Raw values rather than display strings wherever the contract offers
            // them: JSON numbers are culture-invariant by construction, and the
            // *Margin/*MarginAbsolute pairs carry the ratio and the currency value
            // that the table's display strings combine. ProjectFinish is the one
            // exception - the contract only exposes it as a display string.
            var output = new
            {
                metrics.ActivityRisk,
                metrics.ActivityRiskWithStdDevCorrection,
                metrics.CriticalityRisk,
                metrics.FibonacciRisk,
                metrics.GeometricActivityRisk,
                metrics.GeometricCriticalityRisk,
                metrics.GeometricFibonacciRisk,
                metrics.NetworkCyclomaticComplexity,
                metrics.NetworkDuration,
                metrics.NetworkDurationManMonths,
                metrics.ProjectFinish,
                metrics.EffortEfficiency,
                metrics.ActivityEffort,
                metrics.DirectEffort,
                metrics.IndirectEffort,
                metrics.OtherEffort,
                metrics.TotalEffort,
                metrics.DirectCost,
                metrics.IndirectCost,
                metrics.OtherCost,
                metrics.TotalCost,
                metrics.DirectBilling,
                metrics.IndirectBilling,
                metrics.OtherBilling,
                metrics.TotalBilling,
                metrics.DirectMargin,
                metrics.IndirectMargin,
                metrics.OtherMargin,
                metrics.TotalMargin,
                metrics.DirectMarginAbsolute,
                metrics.IndirectMarginAbsolute,
                metrics.OtherMarginAbsolute,
                metrics.TotalMarginAbsolute,
            };

            // Json.NET indents with the platform's line end. JSON escapes the line breaks in its strings, so every one
            // left in the text is indentation.
            return NewLineHelper.NormalizeNewLines(JsonConvert.SerializeObject(output, Formatting.Indented));
        }

        // Error messages name options by their long form, resolved via
        // OptionLongName from the Option attributes on the Options class, so a
        // renamed option can never leave a stale name behind in a message.
        // This and the helpers below are internal rather than private so the test
        // assembly (see InternalsVisibleTo in the csproj) can exercise them
        // directly, without spinning up the whole host.
        internal static void ValidateOptions(Options options)
        {
            string input = OptionLongName(nameof(Options.InputFilename));
            string import = OptionLongName(nameof(Options.ImportFilename));
            string scenario = OptionLongName(nameof(Options.Scenario));
            string listScenarios = OptionLongName(nameof(Options.ListScenarios));

            if (options.InputFilename is not null
                && options.ImportFilename is not null)
            {
                throw new UsageException(string.Format(Resource.ProjectPlan.Messages.Message_SpecifyEitherOptionNotBoth, input, import));
            }

            if (options.InputFilename is null
                && (options.Scenario is not null || options.ListScenarios))
            {
                throw new UsageException(string.Format(Resource.ProjectPlan.Messages.Message_OptionsOnlyValidWithOption, scenario, listScenarios, input));
            }

            if (options.Scenario is not null
                && options.ListScenarios)
            {
                throw new UsageException(string.Format(Resource.ProjectPlan.Messages.Message_SpecifyEitherOptionNotBoth, scenario, listScenarios));
            }

            // Zero switches the limit off; a negative value is meaningless rather
            // than a second way of saying that, so it is rejected outright.
            if (options.CompileTimeoutMilliseconds < 0)
            {
                throw new UsageException(string.Format(Resource.ProjectPlan.Messages.Message_OptionCannotBeNegative, OptionLongName(nameof(Options.CompileTimeoutMilliseconds))));
            }

            RequireSize(
                options.GanttDirectory,
                options.GanttSize,
                OptionLongName(nameof(Options.GanttDirectory)),
                OptionLongName(nameof(Options.GanttSize)));
            RequireSize(
                options.ResourceDirectory,
                options.ResourceSize,
                OptionLongName(nameof(Options.ResourceDirectory)),
                OptionLongName(nameof(Options.ResourceSize)));
            RequireSize(
                options.EVDirectory,
                options.EVSize,
                OptionLongName(nameof(Options.EVDirectory)),
                OptionLongName(nameof(Options.EVSize)));
            RequireSize(
                options.ScenarioChartDirectory,
                options.ScenarioChartSize,
                OptionLongName(nameof(Options.ScenarioChartDirectory)),
                OptionLongName(nameof(Options.ScenarioChartSize)));

            // All export directories are checked up front so that a bad path fails
            // the run before any file has been written.
            RequireDirectory(options.GanttDirectory);
            RequireDirectory(options.ArrowGraphDirectory);
            RequireDirectory(options.VertexGraphDirectory);
            RequireDirectory(options.ResourceDirectory);
            RequireDirectory(options.EVDirectory);
            RequireDirectory(options.ScenarioChartDirectory);
        }

        private static void RequireSize(
            string? directory,
            IEnumerable<int> size,
            string directoryOption,
            string sizeOption)
        {
            // The parser already guarantees a present size has exactly two values
            // (Min = 2, Max = 2), so absence is the only case left to catch.
            if (directory is not null
                && !size.Any())
            {
                throw new UsageException(string.Format(Resource.ProjectPlan.Messages.Message_OptionRequiredWithOption, sizeOption, directoryOption));
            }
        }

        // Resolves an option's display name ("--long-name") from the Option
        // attribute on the named Options property, so messages track the attribute
        // text. Callers pass nameof(Options.X), which keeps the property end
        // rename-safe as well. Throws (rather than degrading) when the property is
        // not an option, so a refactor that breaks the link fails loudly in tests.
        internal static string OptionLongName(string optionPropertyName)
        {
            OptionAttribute? attribute = typeof(Options)
                .GetProperty(optionPropertyName)?
                .GetCustomAttribute<OptionAttribute>();

            return attribute is null
                ? throw new InvalidOperationException($@"{optionPropertyName} is not an option property on {nameof(Options)}")
                : $@"--{attribute.LongName}";
        }

        private static void RequireDirectory(string? directory)
        {
            if (directory is not null
                && !Directory.Exists(directory))
            {
                throw new InvalidOperationException(string.Format(Resource.ProjectPlan.Messages.Message_DirectoryDoesNotExist, directory));
            }
        }

        // A chart the options ask for, when they give its directory. ValidateOptions
        // has checked that its size comes with the directory, and the parser that
        // the size has exactly two values.
        private static ChartOutputRequest? ToChartOutputRequest(
            string? directory,
            PlotExport format,
            IEnumerable<int> size)
        {
            if (directory is null)
            {
                return null;
            }

            IList<int> sizeList = [.. size];
            return new ChartOutputRequest(ToChartImageFormat(format), sizeList[0], sizeList[1]);
        }

        // A graph the options ask for, when they give its directory.
        private static GraphOutputRequest? ToGraphOutputRequest(
            string? directory,
            GraphExport format)
        {
            return directory is null
                ? null
                : new GraphOutputRequest(ToGraphExportFormat(format));
        }

        // The file each output the options ask for is written to: the project and
        // the export where the options name them, and each chart and graph in its
        // directory, named after the project.
        private static Dictionary<JobOutput, string> BuildOutputFilenames(
            Options options,
            string projectTitle)
        {
            var filenames = new Dictionary<JobOutput, string>();

            if (options.OutputFilename is not null)
            {
                filenames.Add(JobOutput.Project, options.OutputFilename);
            }
            if (options.ExportFilename is not null)
            {
                filenames.Add(JobOutput.ScenarioExport, options.ExportFilename);
            }

            AddExportFilePath(filenames, JobOutput.GanttChart, options.GanttDirectory, projectTitle, Resource.ProjectPlan.Suffixes.Suffix_GanttChart, options.GanttFormat.GetDescription());
            AddExportFilePath(filenames, JobOutput.ArrowGraph, options.ArrowGraphDirectory, projectTitle, Resource.ProjectPlan.Suffixes.Suffix_ArrowChart, options.ArrowGraphFormat.GetDescription());
            AddExportFilePath(filenames, JobOutput.VertexGraph, options.VertexGraphDirectory, projectTitle, Resource.ProjectPlan.Suffixes.Suffix_VertexChart, options.VertexGraphFormat.GetDescription());
            AddExportFilePath(filenames, JobOutput.ResourceChart, options.ResourceDirectory, projectTitle, Resource.ProjectPlan.Suffixes.Suffix_ResourceChart, options.ResourceFormat.GetDescription());
            AddExportFilePath(filenames, JobOutput.EarnedValueChart, options.EVDirectory, projectTitle, Resource.ProjectPlan.Suffixes.Suffix_EarnedValueChart, options.EVFormat.GetDescription());
            AddExportFilePath(filenames, JobOutput.ScenarioChart, options.ScenarioChartDirectory, projectTitle, Resource.ProjectPlan.Suffixes.Suffix_ScenarioChart, options.ScenarioChartFormat.GetDescription());

            return filenames;
        }

        private static void AddExportFilePath(
            Dictionary<JobOutput, string> filenames,
            JobOutput output,
            string? directory,
            string projectTitle,
            string suffix,
            string formatDescription)
        {
            if (directory is not null)
            {
                filenames.Add(output, BuildExportFilePath(directory, projectTitle, suffix, formatDescription));
            }
        }

        // The chart formats zpp offers, as the file layer names them. PDF, which the desktop's Save-As also writes, is not
        // among zpp's chart formats.
        internal static ChartImageFormat ToChartImageFormat(PlotExport format)
        {
            return format switch
            {
                PlotExport.Jpeg => ChartImageFormat.Jpeg,
                PlotExport.Png => ChartImageFormat.Png,
                PlotExport.Bmp => ChartImageFormat.Bmp,
                PlotExport.Webp => ChartImageFormat.Webp,
                PlotExport.Svg => ChartImageFormat.Svg,
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
            };
        }

        // The graph formats zpp offers, as the file layer names them.
        internal static GraphExportFormat ToGraphExportFormat(GraphExport format)
        {
            return format switch
            {
                GraphExport.Jpeg => GraphExportFormat.Jpeg,
                GraphExport.Png => GraphExportFormat.Png,
                GraphExport.Pdf => GraphExportFormat.Pdf,
                GraphExport.Svg => GraphExportFormat.Svg,
                GraphExport.GraphML => GraphExportFormat.GraphML,
                GraphExport.Dot => GraphExportFormat.GraphViz,
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
            };
        }

        internal static string BuildExportFilePath(
            string directory,
            string projectTitle,
            string suffix,
            string formatDescription)
        {
            return Path.Combine(directory, $@"{projectTitle}{suffix}.{formatDescription.ToLowerInvariant()}");
        }

        // The engine says why it could not select the scenario; zpp says it in
        // its own words, which point at the option that lists the scenarios.
        internal static string BuildScenarioSelectionMessage(ScenarioSelectionException ex)
        {
            string listScenarios = OptionLongName(nameof(Options.ListScenarios));

            return ex.Failure switch
            {
                ScenarioSelectionFailure.NoMatch => string.Format(Resource.ProjectPlan.Messages.Message_NoScenarioMatches, ex.Selector, listScenarios),
                ScenarioSelectionFailure.SeveralMatches => string.Format(Resource.ProjectPlan.Messages.Message_SeveralScenariosMatch, ex.Selector, ex.MatchCount, listScenarios),
                ScenarioSelectionFailure.NoScenarioData => string.Format(Resource.ProjectPlan.Messages.Message_ScenarioHasNoScenarioData, ex.Selector),
                _ => ex.Message,
            };
        }

        private static void DisplayScenarios(IEnumerable<ScenarioSummary> scenarios)
        {
            var table = new ConsoleTable(
                Resource.ProjectPlan.Titles.Title_Scenario,
                Resource.ProjectPlan.Titles.Title_Id,
                Resource.ProjectPlan.Titles.Title_Tracked,
                Resource.ProjectPlan.Titles.Title_Current);

            foreach (ScenarioSummary scenario in scenarios)
            {
                table.AddRow(
                    scenario.Path,
                    scenario.Id,
                    scenario.IsTracked ? Resource.ProjectPlan.Labels.Label_Yes : string.Empty,
                    scenario.IsCurrent ? Resource.ProjectPlan.Symbols.Symbol_Current : string.Empty);
            }

            table.Configure(x =>
            {
                x.NumberAlignment = Alignment.Left;
                x.EnableCount = false;
            });

            Display(table.ToMarkDownString());
        }

        private static int OnParseErrors<T>(
            ParserResult<T> result,
            IEnumerable<Error> errs)
        {
            DisplayHelp(result);

            // Help explicitly requested is a successful outcome; anything else
            // that lands here is a genuine usage error.
            return errs.Any(x => x.Tag is ErrorType.HelpRequestedError or ErrorType.HelpVerbRequestedError)
                ? c_ExitSuccess
                : c_ExitUsageError;
        }

        private static void Display(
            string content,
            bool hasErrors = false)
        {
            if (hasErrors)
            {
                Console.ForegroundColor = ConsoleColor.Red;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
            }
            StandardOutput.WriteLine();
            StandardOutput.WriteLine(content);
            Console.ResetColor();
        }

        private static void DisplayHelp<T>(ParserResult<T> result)
        {
            // https://github.com/commandlineparser/commandline/wiki/How-To#q1
            // https://github.com/commandlineparser/commandline/wiki/HelpText-Configuration
            HelpText helpText = HelpText.AutoBuild(result, h =>
            {
                // Remove the extra newline between options.
                h.AdditionalNewLineAfterOption = false;

                // Change header.
                h.Heading = $@"{Resource.ProjectPlan.Labels.Label_CliAppName}, {Resource.ProjectPlan.Labels.Label_Version} {Resource.ProjectPlan.Labels.Label_AppVersion}";

                // Change copyright.
                h.Copyright = $@"{Resource.ProjectPlan.Labels.Label_Copyright}, {Resource.ProjectPlan.Labels.Label_Author}";

                // This needs to be included to prevent the --version option.
                h.AutoVersion = false;

                return HelpText.DefaultParsingErrorsHandler(result, h);
            }, e => e);

            Console.Out.WriteLine(helpText);
        }

        // Thrown for invalid option combinations: caught in Main and mapped to
        // the usage-error exit code, distinct from runtime failures.
        internal sealed class UsageException
            : Exception
        {
            public UsageException(string message)
                : base(message)
            {
            }
        }
    }
}
