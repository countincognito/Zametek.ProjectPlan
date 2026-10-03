using CommandLine;
using CommandLine.Text;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using System.Collections;
using System.Globalization;
using System.Reflection;
using Zametek.Common.ProjectPlan;
using Zametek.Engine.ProjectPlan;
using Zametek.Utility;
using Zametek.ViewModel.ProjectPlan;

// Using these as a starting point:
// https://github.com/jasonterando/dotnet-console-demo/
// https://medium.com/@eduardosilva_94960/mastering-command-line-parsing-in-net-core-with-commandlineparser-c20721100359
namespace Zametek.ProjectPlan.CommandLine
{
    public class Program
    {
        // What --now accepts: ISO 8601, to the second or finer, with the offset from UTC or Z for UTC itself.
        private static readonly string[] s_NowFormats =
        [
            @"yyyy-MM-dd'T'HH:mm:sszzz",
            @"yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz",
            @"yyyy-MM-dd'T'HH:mm:ss'Z'",
            @"yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'",
        ];

        public static async Task<int> Main(string[] args)
        {
            return await MainAsync(args, ReadEnvironment());
        }

        // Main, with the environment it reads ZPP_SERVER and ZPP_API_KEY from given to it, so that a test can choose
        // what it reads without changing the process's.
        internal static async Task<int> MainAsync(
            string[] args,
            IReadOnlyDictionary<string, string> environment)
        {
            var console = new StandardConsole(Console.Out, Console.Error);

            // zpp serve runs zpp as a server instead, with options of its own.
            if (args is [JobServer.Command, ..])
            {
                return (int)await JobServer.RunAsync(args[1..], console);
            }

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

                return (int)await parserResult.MapResult(
                    async options =>
                    {
                        ConfigureSerilog(options.Verbose);
                        ValidateOptions(options);

                        // A run sent to a server never builds the engine, nor so
                        // much as loads its container: running here is a method
                        // of its own, which only a run here compiles.
                        return ClientSettingsHelper.Resolve(options, environment) is ClientSettings server
                            ? await JobClient.RunAsync(options, server, console)
                            : await RunHereAsync(options, console);
                    },
                    errs => Task.FromResult(OnParseErrors(parserResult, errs, Options.Usage)));
            }
            catch (UsageException ex)
            {
                await console.WriteErrorLineAsync(ex.Message);
                return (int)ExitCode.UsageError;
            }
            catch (ServerException ex)
            {
                await console.WriteErrorLineAsync(ex.Message);
                return (int)ExitCode.ServerFailure;
            }
            catch (Exception ex)
            {
                return (int)await JobConsoleHelper.WriteFailureAsync(console, ex);
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        // The environment, by name, whatever its case - as Windows reads it.
        internal static Dictionary<string, string> ReadEnvironment()
        {
            var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables())
            {
                environment[(string)variable.Key] = (string?)variable.Value ?? string.Empty;
            }

            return environment;
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

        // The engine's container, the same for zpp and zpp serve. zpp serve, which keeps its container for as long as it
        // runs, also has it check every registration up front, and that nothing a job holds is resolved outside the
        // job's scope.
        internal static ServiceProvider BuildServices(bool validate = false)
        {
            return new ServiceCollection()
                .AddProjectPlanEngine()
                .AddSerilog()
                .BuildServiceProvider(new ServiceProviderOptions
                {
                    ValidateOnBuild = validate,
                    ValidateScopes = validate,
                });
        }

        // The run, in this process, on an engine built for its one job.
        private static async Task<ExitCode> RunHereAsync(
            Options options,
            IJobConsole console)
        {
            // Before the container exists, because the view models the
            // engine runs depend on it.
            ProjectPlanEngine.Initialize();

            // The container is built only once the options parse, so
            // help and usage errors never pay for it. The run is one
            // job, which the engine gives a scope of its own.
            await using ServiceProvider services = BuildServices();

            return await RunAsync(options, services.GetRequiredService<JobRunner>(), console);
        }

        private static async Task<ExitCode> RunAsync(
            Options options,
            JobRunner jobRunner,
            IJobConsole console)
        {
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
                await JobConsoleHelper.WriteScenariosAsync(console, await jobRunner.ListScenariosAsync(input));
                return ExitCode.Success;
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
                Now = ToNow(options.Now),
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
            string projectTitle = FileFormatHelper.GetProjectTitle(inputFilename);

            JobResult result = await jobRunner.RunAsync(request, new FileJobSink(BuildOutputFilenames(options, projectTitle), console));

            return await JobConsoleHelper.WriteResultAsync(console, result, options.MetricsFormat);
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

            // Without its offset the same time would be a different instant on
            // each machine, which is the one thing a fixed time is there to stop.
            if (options.Now is not null
                && !TryParseNow(options.Now, out _))
            {
                throw new UsageException(string.Format(Resource.ProjectPlan.Messages.Message_OptionMustBeADateTimeWithOffset, OptionLongName(nameof(Options.Now))));
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
            return OptionLongName<Options>(optionPropertyName);
        }

        // The same for another set of options - zpp serve's.
        internal static string OptionLongName<TOptions>(string optionPropertyName)
        {
            OptionAttribute? attribute = typeof(TOptions)
                .GetProperty(optionPropertyName)?
                .GetCustomAttribute<OptionAttribute>();

            return attribute is null
                ? throw new InvalidOperationException($@"{optionPropertyName} is not an option property on {typeof(TOptions).Name}")
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

        // The time the options fix the run at, if they fix one. ValidateOptions has
        // checked that a time they give parses.
        private static DateTimeOffset? ToNow(string? now)
        {
            return now is not null && TryParseNow(now, out DateTimeOffset result)
                ? result
                : null;
        }

        internal static bool TryParseNow(string now, out DateTimeOffset result)
        {
            return DateTimeOffset.TryParseExact(
                now,
                s_NowFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out result);
        }

        // The file each output the options ask for is written to: the project and
        // the export where the options name them, and each chart and graph in its
        // directory, named after the project - whether the job runs here or on a
        // server.
        internal static Dictionary<JobOutput, string> BuildOutputFilenames(
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
            return Path.Combine(directory, BuildExportFilename(projectTitle, suffix, formatDescription));
        }

        // The name of a chart or graph's file: the project's title, the chart or graph's suffix, and its format's
        // extension. zpp serve names the files it returns the same way.
        internal static string BuildExportFilename(
            string projectTitle,
            string suffix,
            string formatDescription)
        {
            return $@"{projectTitle}{suffix}.{formatDescription.ToLowerInvariant()}";
        }

        // zpp serve's options are parsed - and their help shown - the same way, with its own usage.
        internal static ExitCode OnParseErrors<T>(
            ParserResult<T> result,
            IEnumerable<Error> errs,
            IReadOnlyList<string> usage)
        {
            DisplayHelp(result, usage);

            // Help explicitly requested is a successful outcome; anything else
            // that lands here is a genuine usage error.
            return errs.Any(x => x.Tag is ErrorType.HelpRequestedError or ErrorType.HelpVerbRequestedError)
                ? ExitCode.Success
                : ExitCode.UsageError;
        }

        private static void DisplayHelp<T>(
            ParserResult<T> result,
            IReadOnlyList<string> usage)
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

                // What was wrong with the options, if anything was, and then how the command is used, above its
                // options - set off by a blank line, as the library sets off its errors: a line that starts with a
                // new line, since it drops an empty line it is given first.
                HelpText withErrors = HelpText.DefaultParsingErrorsHandler(result, h);
                withErrors.AddPreOptionsLines(usage.Select((line, index) => index == 0 ? Environment.NewLine + line : line));
                return withErrors;
            }, e => e);

            Console.Out.WriteLine(helpText);
        }
    }
}
