using CommandLine;
using CommandLine.Text;
using ConsoleTables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using ReactiveUI.Builder;
using Serilog;
using Serilog.Events;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Graphs.Avalonia;
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
        // unexpected errors), 2 = bad usage (invalid options or combinations), and
        // 3 = the project compiled with errors - kept distinct from 1 so a
        // pipeline can tell a broken plan from a broken invocation.
        private const int c_ExitSuccess = 0;
        private const int c_ExitFailure = 1;
        private const int c_ExitUsageError = 2;
        private const int c_ExitCompilationErrors = 3;

        public static async Task<int> Main(string[] args)
        {
            try
            {
                InitializeReactiveUI();

                using var parser = new Parser(with =>
                {
                    with.CaseInsensitiveEnumValues = true;
                    with.HelpWriter = null;

                    // This needs to be included to prevent the --version option.
                    with.AutoVersion = false;
                });

                ParserResult<Options> parserResult = parser.ParseArguments<Options>(args);

                return await parserResult.MapResult(
                    options =>
                    {
                        ConfigureSerilog(options.Verbose);

                        // The host is built only once the options parse, so help
                        // and usage errors never pay for the container. It is
                        // deliberately never disposed: its singletons are view
                        // models wired for desktop lifetimes, and the process is
                        // about to exit anyway.
                        IHost host = BuildHost(args);

                        return RunAsync(options, host.Services);
                    },
                    errs => Task.FromResult(OnParseErrors(parserResult, errs)));
            }
            catch (UsageException ex)
            {
                await Console.Error.WriteLineAsync(ex.Message);
                return c_ExitUsageError;
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

        private static int s_ReactiveUIInitialized;

        private static void InitializeReactiveUI()
        {
            // ReactiveUI 23 requires explicit initialization before any WhenAnyValue is used.
            // The desktop app does this via Avalonia's .UseReactiveUI(); this headless CLI has no
            // UI platform, so initialize the core (non-UI) ReactiveUI services directly. The
            // initialization is process-global and Main is invoked repeatedly in-process by the
            // test suite, so it must run exactly once.
            if (Interlocked.Exchange(ref s_ReactiveUIInitialized, 1) == 0)
            {
                RxAppBuilder.CreateReactiveUIBuilder()
                    .WithCoreServices()
                    .BuildApp();
            }
        }

        private static void ConfigureSerilog(bool verbose)
        {
            // Everything goes to stderr so stdout stays clean for parseable output
            // (the metrics table or JSON). Warnings and errors always show - this
            // is where the view models' ILogger<T> output surfaces, via the
            // UseSerilog registration in BuildHost - and --verbose lowers the
            // threshold to their informational lifecycle logging.
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(verbose ? LogEventLevel.Information : LogEventLevel.Warning)
                .WriteTo.Console(
                    standardErrorFromLevel: LogEventLevel.Verbose,
                    formatProvider: System.Globalization.CultureInfo.InvariantCulture)
                .CreateLogger();
        }

        private static IHost BuildHost(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton(TimeProvider.System);
                    services.AddSingleton<IProjectScenarioManagerViewModel, ProjectScenarioManagerViewModel>();
                    services.AddSingleton<ICoreViewModel, CoreViewModel>();
                    services.AddSingleton<ISettingService, SettingService>();
                    services.AddSingleton<IDialogService, DialogService>();

                    services.AddSingleton<IGraphCompilationService, GraphCompilationService>();
                    services.AddSingleton<IResourceSchedulingService, ResourceSchedulingService>();
                    services.AddSingleton<IMetricCalculationService, MetricCalculationService>();

                    services.AddSingleton<IDateTimeCalculator, DateTimeCalculator>();
                    services.AddSingleton<IGraphLayoutEngine, MsaglGraphLayoutEngine>();

                    services.AddSingleton<IGanttChartManagerViewModel, GanttChartManagerViewModel>();
                    services.AddSingleton<IArrowGraphManagerViewModel, ArrowGraphManagerViewModel>();
                    services.AddSingleton<IVertexGraphManagerViewModel, VertexGraphManagerViewModel>();
                    services.AddSingleton<IResourceChartManagerViewModel, ResourceChartManagerViewModel>();
                    services.AddSingleton<IEarnedValueChartManagerViewModel, EarnedValueChartManagerViewModel>();
                    services.AddSingleton<IScenarioChartManagerViewModel, ScenarioChartManagerViewModel>();
                    services.AddSingleton<IMetricManagerViewModel, MetricManagerViewModel>();
                    services.AddSingleton<IOutputManagerViewModel, OutputManagerViewModel>();

                    services.AddSingleton<IProjectFileOpen, ProjectFileOpen>();
                    services.AddSingleton<IMicrosoftProjectFileImporter, MicrosoftProjectFileImporter>();
                    services.AddSingleton<IXlsxScenarioFileImporter, XlsxScenarioFileImporter>();
                    services.AddSingleton<IProjectScenarioFileImport, ProjectScenarioFileImport>();
                    services.AddSingleton<IProjectFileSave, ProjectFileSave>();
                    services.AddSingleton<IScottPlotImageExporter, ScottPlotImageExporter>();
                    services.AddSingleton<IXlsxScenarioFileExporter, XlsxScenarioFileExporter>();
                    services.AddSingleton<IProjectScenarioFileExport, ProjectScenarioFileExport>();

                    services.AddSingleton(new Data.ProjectPlan.VersionMapper());
                    services.AddSingleton(new ProjectPlanMapper());

                    services.AddSingleton<IDataGridLayoutManager, DataGridLayoutManager>();
                    services.AddSingleton<IDataGridScrollManager, DataGridScrollManager>();
                })
                .UseSerilog()
                .Build();
        }

        private static async Task<int> RunAsync(
            Options options,
            IServiceProvider services)
        {
            ValidateOptions(options);

            IProjectScenarioManagerViewModel project = ResolveMuted<IProjectScenarioManagerViewModel>(services);
            ICoreViewModel core = ResolveMuted<ICoreViewModel>(services);
            IMetricManagerViewModel metrics = ResolveMuted<IMetricManagerViewModel>(services);
            IOutputManagerViewModel outputs = ResolveMuted<IOutputManagerViewModel>(services);

            ISettingService settingService = services.GetRequiredService<ISettingService>();

            core.AutoCompile = false;

            // File in.
            {
                string? inputFilename = options.InputFilename;
                string? importFilename = options.ImportFilename;

                if (inputFilename is not null)
                {
                    IProjectFileOpen projectFileOpen = services.GetRequiredService<IProjectFileOpen>();
                    ProjectModel projectModel = await projectFileOpen.OpenProjectFileAsync(inputFilename);

                    if (options.ListScenarios)
                    {
                        DisplayScenarios(projectModel);
                        return c_ExitSuccess;
                    }

                    if (options.Scenario is not null)
                    {
                        // Re-point the project's current-scenario marker before any
                        // processing: ProcessProject loads whichever scenario Current
                        // names, so this is the entire mechanism of --scenario.
                        projectModel = projectModel with { Current = ResolveScenarioId(projectModel, options.Scenario) };
                    }

                    // First process the project.
                    project.ProcessProject(projectModel);
                    settingService.SetProjectFilePath(inputFilename, bindTitleToFilename: true);
                }
                else if (importFilename is not null)
                {
                    IProjectScenarioFileImport projectFileImport = services.GetRequiredService<IProjectScenarioFileImport>();
                    Guid projectScenarioId = settingService.ScenarioId;
                    string projectScenarioTitle = settingService.ScenarioTitle;
                    ProjectScenarioImportModel projectImport = projectFileImport.ImportProjectScenarioFile(importFilename);
                    core.ProcessProjectScenarioImport(projectImport, projectScenarioId, projectScenarioTitle);
                    settingService.SetProjectFilePath(importFilename, bindTitleToFilename: true);
                }
            }

            // Pin the export title now: the file-out block below rebinds the title
            // to each file it writes (matching the desktop's save-as behaviour), and
            // chart images must be named after the project that was processed, not
            // after wherever its results were saved.
            string projectTitle = settingService.ProjectTitle;

            // Base theme.
            {
                core.BaseTheme = options.BaseTheme;
            }

            // Compile.
            {
                // We do not need to set IsReadyToReviseTrackers since this is a one step
                // process (i.e. we are not changing any tracker UI elements).

                core.RunCompile();
                outputs.BuildCompilationOutput();

                if (core.HasCompilationErrors)
                {
                    Display(outputs.CompilationOutput, core.HasCompilationErrors);
                    return c_ExitCompilationErrors;
                }

                // Mirrors the order of CoreViewModel.RunBuildCascade, which is what
                // builds these outputs in the desktop app after each compile.
                core.BuildArrowGraph();
                core.BuildVertexGraph();
                core.BuildResourceSeriesSet();
                core.BuildTrackingSeriesSet();
                core.BuildNetworkMetrics();
                core.BuildRiskMetrics();
                core.BuildFinancialMetrics();
            }

            // File out.
            {
                string? outputFilename = options.OutputFilename;
                string? exportFilename = options.ExportFilename;

                if (outputFilename is not null)
                {
                    IProjectFileSave projectFileSave = services.GetRequiredService<IProjectFileSave>();
                    ProjectModel projectModel = project.BuildProject();
                    await projectFileSave.SaveProjectFileAsync(projectModel, outputFilename);
                    settingService.SetProjectFilePath(outputFilename, bindTitleToFilename: true);
                }
                if (exportFilename is not null)
                {
                    IProjectScenarioFileExport projectFileExport = services.GetRequiredService<IProjectScenarioFileExport>();
                    ProjectScenarioModel projectScenarioModel = core.BuildProjectScenario();
                    projectFileExport.ExportProjectScenarioFile(
                        projectScenarioModel,
                        core.ResourceSeriesSet,
                        core.TrackingSeriesSet,
                        core.DisplaySettingsViewModel.ShowDates,
                        exportFilename);
                    settingService.SetProjectFilePath(exportFilename, bindTitleToFilename: true);
                }
            }

            // Chart and graph exports. Each manager view model is resolved only
            // when its export was requested - construction is not free, and a
            // typical run wants at most one or two of them.

            // Gantt chart export.
            if (options.GanttDirectory is not null)
            {
                IGanttChartManagerViewModel gantt = ResolveMuted<IGanttChartManagerViewModel>(services);

                await ExportPlotAsync(
                    options.GanttDirectory,
                    options.GanttSize,
                    options.GanttFormat,
                    projectTitle,
                    Resource.ProjectPlan.Suffixes.Suffix_GanttChart,
                    gantt.BuildGanttChartPlotModel,
                    gantt.SaveGanttChartImageFileAsync);
            }

            // Arrow graph export.
            if (options.ArrowGraphDirectory is not null)
            {
                IArrowGraphManagerViewModel arrow = ResolveMuted<IArrowGraphManagerViewModel>(services);

                await ExportGraphAsync(
                    options.ArrowGraphDirectory,
                    options.ArrowGraphFormat,
                    projectTitle,
                    Resource.ProjectPlan.Suffixes.Suffix_ArrowChart,
                    arrow.SaveFixedLayoutArrowGraphImageFileAsync);
            }

            // Vertex graph export.
            if (options.VertexGraphDirectory is not null)
            {
                IVertexGraphManagerViewModel vertex = ResolveMuted<IVertexGraphManagerViewModel>(services);

                await ExportGraphAsync(
                    options.VertexGraphDirectory,
                    options.VertexGraphFormat,
                    projectTitle,
                    Resource.ProjectPlan.Suffixes.Suffix_VertexChart,
                    vertex.SaveFixedLayoutVertexGraphImageFileAsync);
            }

            // Resource chart export.
            if (options.ResourceDirectory is not null)
            {
                IResourceChartManagerViewModel resources = ResolveMuted<IResourceChartManagerViewModel>(services);

                await ExportPlotAsync(
                    options.ResourceDirectory,
                    options.ResourceSize,
                    options.ResourceFormat,
                    projectTitle,
                    Resource.ProjectPlan.Suffixes.Suffix_ResourceChart,
                    resources.BuildResourceChartPlotModel,
                    resources.SaveResourceChartImageFileAsync);
            }

            // EV chart export.
            if (options.EVDirectory is not null)
            {
                IEarnedValueChartManagerViewModel ev = ResolveMuted<IEarnedValueChartManagerViewModel>(services);

                await ExportPlotAsync(
                    options.EVDirectory,
                    options.EVSize,
                    options.EVFormat,
                    projectTitle,
                    Resource.ProjectPlan.Suffixes.Suffix_EarnedValueChart,
                    ev.BuildEarnedValueChartPlotModel,
                    ev.SaveEarnedValueChartImageFileAsync);
            }

            // Scenario chart export.
            if (options.ScenarioChartDirectory is not null)
            {
                IScenarioChartManagerViewModel scenarioChart = ResolveMuted<IScenarioChartManagerViewModel>(services);

                // The tracked-metrics set the chart plots is normally assembled by
                // a reactive pipeline that this headless host mutes, so build it
                // explicitly first.
                project.BuildTrackedMetrics();

                await ExportPlotAsync(
                    options.ScenarioChartDirectory,
                    options.ScenarioChartSize,
                    options.ScenarioChartFormat,
                    projectTitle,
                    Resource.ProjectPlan.Suffixes.Suffix_ScenarioChart,
                    scenarioChart.BuildScenarioChartPlotModel,
                    scenarioChart.SaveScenarioChartImageFileAsync);
            }

            // Metrics.
            {
                switch (options.MetricsFormat)
                {
                    case MetricsExport.Json:
                        // Machine output: undecorated, no colours, no leading
                        // blank line, so it can be piped straight into a parser.
                        Console.Out.WriteLine(BuildMetricsJson(metrics));
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

            return c_ExitSuccess;
        }

        private static ConsoleTable BuildMetricsTable(IMetricManagerViewModel metrics)
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

        private static string BuildMetricsJson(IMetricManagerViewModel metrics)
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

            return JsonConvert.SerializeObject(output, Formatting.Indented);
        }

        // Constructing a view model wires up its reactive subscriptions; in this
        // headless host every build step is invoked explicitly, so those
        // subscriptions are killed the moment each view model is resolved.
        private static T ResolveMuted<T>(IServiceProvider services)
            where T : notnull, IKillSubscriptions
        {
            T viewModel = services.GetRequiredService<T>();
            viewModel.KillSubscriptions();
            return viewModel;
        }

        // This and the helpers below are internal rather than private so the test
        // assembly (see InternalsVisibleTo in the csproj) can exercise them
        // directly, without spinning up the whole host.
        internal static void ValidateOptions(Options options)
        {
            if (options.InputFilename is not null
                && options.ImportFilename is not null)
            {
                throw new UsageException(@"Specify either --input or --import, but not both.");
            }

            if (options.InputFilename is null
                && (options.Scenario is not null || options.ListScenarios))
            {
                throw new UsageException(@"--scenario and --list-scenarios are only valid with --input.");
            }

            if (options.Scenario is not null
                && options.ListScenarios)
            {
                throw new UsageException(@"Specify either --scenario or --list-scenarios, but not both.");
            }

            RequireSize(options.GanttDirectory, options.GanttSize, @"--gantt-directory", @"--gantt-size");
            RequireSize(options.ResourceDirectory, options.ResourceSize, @"--resource-directory", @"--resource-size");
            RequireSize(options.EVDirectory, options.EVSize, @"--ev-directory", @"--ev-size");
            RequireSize(options.ScenarioChartDirectory, options.ScenarioChartSize, @"--scenario-chart-directory", @"--scenario-chart-size");

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
                throw new UsageException($@"{sizeOption} is required when {directoryOption} is specified.");
            }
        }

        private static void RequireDirectory(string? directory)
        {
            if (directory is not null
                && !Directory.Exists(directory))
            {
                throw new InvalidOperationException($@"Directory {directory} does not exist");
            }
        }

        private static async Task ExportPlotAsync(
            string directory,
            IEnumerable<int> size,
            PlotExport format,
            string projectTitle,
            string suffix,
            Action buildPlotModel,
            Func<string, int, int, Task> savePlotImageAsync)
        {
            IList<int> sizeList = [.. size];
            int width = sizeList[0];
            int height = sizeList[1];

            buildPlotModel();

            await savePlotImageAsync(
                BuildExportFilePath(directory, projectTitle, suffix, format.GetDescription()),
                width,
                height);
        }

        private static async Task ExportGraphAsync(
            string directory,
            GraphExport format,
            string projectTitle,
            string suffix,
            Func<string, Task> saveGraphImageAsync)
        {
            await saveGraphImageAsync(
                BuildExportFilePath(directory, projectTitle, suffix, format.GetDescription()));
        }

        internal static string BuildExportFilePath(
            string directory,
            string projectTitle,
            string suffix,
            string formatDescription)
        {
            return Path.Combine(directory, $@"{projectTitle}{suffix}.{formatDescription.ToLowerInvariant()}");
        }

        internal static Guid ResolveScenarioId(
            ProjectModel projectModel,
            string selector)
        {
            List<ProjectScenarioNodeModel> scenarios = [.. projectModel.Nodes.Where(x => x.NodeType == ProjectScenarioNodeType.File)];

            List<ProjectScenarioNodeModel> matches;

            if (Guid.TryParse(selector, out Guid id))
            {
                matches = [.. scenarios.Where(x => x.Id == id)];
            }
            else
            {
                // Exact (case-insensitive) name matches win over id prefixes, the
                // same way git resolves a ref before an abbreviated object id.
                matches = [.. scenarios.Where(x => string.Equals(x.Name, selector, StringComparison.OrdinalIgnoreCase))];

                if (matches.Count == 0)
                {
                    matches = [.. MatchScenarioIdPrefix(scenarios, selector)];
                }
            }

            if (matches.Count == 0)
            {
                throw new InvalidOperationException($@"No scenario matches '{selector}' - use --list-scenarios to see what the project contains");
            }
            if (matches.Count > 1)
            {
                throw new InvalidOperationException($@"'{selector}' matches {matches.Count} scenarios - use --list-scenarios and select one by id");
            }

            Guid scenarioId = matches[0].Id;

            if (!projectModel.Files.Any(x => x.NodeId == scenarioId))
            {
                throw new InvalidOperationException($@"Scenario '{selector}' has no scenario data in the project file");
            }

            return scenarioId;
        }

        // Git's abbreviation floor: an id prefix shorter than this is never
        // treated as an id, it just falls through to the not-found error.
        private const int c_MinimumScenarioIdPrefixLength = 4;

        private static IEnumerable<ProjectScenarioNodeModel> MatchScenarioIdPrefix(
            IEnumerable<ProjectScenarioNodeModel> scenarios,
            string selector)
        {
            // Git-style abbreviation: hyphens are ignored and hex digits are
            // matched case-insensitively against the start of the id, so any
            // portion copied out of --list-scenarios works. The caller treats
            // multiple matches as ambiguous, so a prefix resolves only when it is
            // long enough to be unique.
            string prefix = selector.Replace(@"-", string.Empty).ToLowerInvariant();

            if (prefix.Length < c_MinimumScenarioIdPrefixLength
                || !prefix.All(char.IsAsciiHexDigit))
            {
                return [];
            }

            return scenarios.Where(x => x.Id.ToString(@"N").StartsWith(prefix, StringComparison.Ordinal));
        }

        private static void DisplayScenarios(ProjectModel projectModel)
        {
            Dictionary<Guid, ProjectScenarioNodeModel> nodeLookup = projectModel.Nodes.ToDictionary(x => x.Id);

            var table = new ConsoleTable(@"Scenario", @"Id", @"Tracked", @"Current");

            foreach (ProjectScenarioNodeModel node in projectModel.Nodes.Where(x => x.NodeType == ProjectScenarioNodeType.File))
            {
                table.AddRow(
                    BuildNodePath(projectModel, nodeLookup, node),
                    node.Id,
                    node.IsTracked ? @"Yes" : string.Empty,
                    node.Id == projectModel.Current ? @"*" : string.Empty);
            }

            table.Configure(x =>
            {
                x.NumberAlignment = Alignment.Left;
                x.EnableCount = false;
            });

            Display(table.ToMarkDownString());
        }

        internal static string BuildNodePath(
            ProjectModel projectModel,
            IReadOnlyDictionary<Guid, ProjectScenarioNodeModel> nodeLookup,
            ProjectScenarioNodeModel node)
        {
            // Folder names are prefixed so that scenarios with the same name in
            // different folders stay distinguishable in the listing. The visited
            // set guards against a malformed file with a parent cycle.
            var names = new List<string> { node.Name };
            var visited = new HashSet<Guid> { node.Id };
            Guid parentId = node.ParentId;

            while (parentId != projectModel.Root
                && visited.Add(parentId)
                && nodeLookup.TryGetValue(parentId, out ProjectScenarioNodeModel? parent))
            {
                names.Insert(0, parent.Name);
                parentId = parent.ParentId;
            }

            return string.Join(@"/", names);
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
            Console.Out.WriteLine();
            Console.Out.WriteLine(content);
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
