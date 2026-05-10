using CommandLine;
using CommandLine.Text;
using ConsoleTables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Utility;
using Zametek.ViewModel.ProjectPlan;

// Using these as a starting point:
// https://github.com/jasonterando/dotnet-console-demo/
// https://medium.com/@eduardosilva_94960/mastering-command-line-parsing-in-net-core-with-commandlineparser-c20721100359
namespace Zametek.ProjectPlan.CommandLine
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            try
            {
                var parser = new Parser(with =>
                {
                    with.CaseInsensitiveEnumValues = true;
                    with.HelpWriter = null;

                    // This needs to be included to prevent the --version option.
                    with.AutoVersion = false;
                });

                IHost host = Host.CreateDefaultBuilder(args)
                    .ConfigureServices((context, services) =>
                    {
                        //// Configure Serilog
                        //Log.Logger = new LoggerConfiguration()
                        //    .ReadFrom.Configuration(context.Configuration)
                        //    .CreateLogger();

                        services.AddSingleton(TimeProvider.System);
                        services.AddSingleton<IProjectScenarioManagerViewModel, ProjectScenarioManagerViewModel>();
                        services.AddSingleton<ICoreViewModel, CoreViewModel>();
                        services.AddSingleton<ISettingService, SettingService>();
                        services.AddSingleton<IDialogService, DialogService>();
                        services.AddSingleton<IDateTimeCalculator, DateTimeCalculator>();
                        services.AddSingleton<IArrowGraphSerializer, ArrowGraphSerializer>();
                        services.AddSingleton<IVertexGraphSerializer, VertexGraphSerializer>();

                        services.AddSingleton<IGanttChartManagerViewModel, GanttChartManagerViewModel>();
                        services.AddSingleton<IArrowGraphManagerViewModel, ArrowGraphManagerViewModel>();
                        services.AddSingleton<IVertexGraphManagerViewModel, VertexGraphManagerViewModel>();
                        services.AddSingleton<IResourceChartManagerViewModel, ResourceChartManagerViewModel>();
                        services.AddSingleton<IEarnedValueChartManagerViewModel, EarnedValueChartManagerViewModel>();
                        services.AddSingleton<IMetricManagerViewModel, MetricManagerViewModel>();
                        services.AddSingleton<IOutputManagerViewModel, OutputManagerViewModel>();

                        services.AddSingleton<IProjectFileOpen, ProjectFileOpen>();
                        services.AddSingleton<IMicrosoftProjectFileImporter, MicrosoftProjectFileImporter>();
                        services.AddSingleton<IXlsxFileImporter, XlsxFileImporter>();
                        services.AddSingleton<IProjectScenarioFileImport, ProjectScenarioFileImport>();
                        services.AddSingleton<IProjectFileSave, ProjectFileSave>();
                        services.AddSingleton<IProjectScenarioFileExport, ProjectScenarioFileExport>();

                        services.AddSingleton(new Data.ProjectPlan.VersionMapper());
                        services.AddSingleton(new ProjectPlanMapper());
                    })
                    .UseSerilog()
                    .Build();

                IProjectScenarioManagerViewModel project = host.Services.GetRequiredService<IProjectScenarioManagerViewModel>();
                project.KillSubscriptions();

                ICoreViewModel core = host.Services.GetRequiredService<ICoreViewModel>();
                core.KillSubscriptions();

                IProjectFileOpen projectFileOpen = host.Services.GetRequiredService<IProjectFileOpen>();
                IProjectScenarioFileImport projectFileImport = host.Services.GetRequiredService<IProjectScenarioFileImport>();

                IProjectFileSave projectFileSave = host.Services.GetRequiredService<IProjectFileSave>();
                IProjectScenarioFileExport projectFileExport = host.Services.GetRequiredService<IProjectScenarioFileExport>();

                ISettingService settingService = host.Services.GetRequiredService<ISettingService>();

                core.AutoCompile = false;

                ParserResult<Options> parserResult = parser.ParseArguments<Options>(args);

                parserResult
                    .WithNotParsed(errs =>
                    {
                        DisplayHelp(parserResult);
                    })
                    .WithParsed(options =>
                    {
                        IMetricManagerViewModel metrics = host.Services.GetRequiredService<IMetricManagerViewModel>();
                        metrics.KillSubscriptions();

                        IOutputManagerViewModel outputs = host.Services.GetRequiredService<IOutputManagerViewModel>();
                        outputs.KillSubscriptions();

                        IGanttChartManagerViewModel gantt = host.Services.GetRequiredService<IGanttChartManagerViewModel>();
                        gantt.KillSubscriptions();

                        IArrowGraphManagerViewModel arrow = host.Services.GetRequiredService<IArrowGraphManagerViewModel>();
                        arrow.KillSubscriptions();

                        IVertexGraphManagerViewModel vertex = host.Services.GetRequiredService<IVertexGraphManagerViewModel>();
                        vertex.KillSubscriptions();

                        IResourceChartManagerViewModel resources = host.Services.GetRequiredService<IResourceChartManagerViewModel>();
                        resources.KillSubscriptions();

                        IEarnedValueChartManagerViewModel ev = host.Services.GetRequiredService<IEarnedValueChartManagerViewModel>();
                        ev.KillSubscriptions();

                        // File in.
                        {

                            string? inputFilename = options.InputFilename;
                            string? importFilename = options.ImportFilename;

                            if (inputFilename is not null)
                            {
                                ProjectModel projectModel = projectFileOpen.OpenProjectFileAsync(inputFilename).Result;

                                // First process the project.
                                project.ProcessProject(projectModel);
                                settingService.SetProjectFilePath(inputFilename, bindTitleToFilename: true);
                            }
                            else if (importFilename is not null)
                            {
                                Guid projectScenarioId = settingService.ScenarioId;
                                string projectScenarioTitle = settingService.ScenarioTitle;
                                ProjectScenarioImportModel projectImport = projectFileImport.ImportProjectScenarioFile(importFilename);
                                core.ProcessProjectScenarioImport(projectImport, projectScenarioId, projectScenarioTitle);
                                settingService.SetProjectFilePath(importFilename, bindTitleToFilename: true);
                            }
                            else
                            {
                                DisplayHelp(parserResult);
                                return;
                            }
                        }

                        // Base theme
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
                                return;
                            }

                            core.BuildNetworkMetrics();
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
                                ProjectModel projectModel = project.BuildProject();
                                projectFileSave.SaveProjectFileAsync(projectModel, outputFilename).Wait();
                                settingService.SetProjectFilePath(outputFilename, bindTitleToFilename: true);
                            }
                            if (exportFilename is not null)
                            {
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

                        // Gantt chart export.
                        {
                            string? ganttDirectory = options.GanttDirectory;

                            if (ganttDirectory is not null)
                            {
                                if (!Directory.Exists(ganttDirectory))
                                {
                                    throw new InvalidOperationException($@"Directory {ganttDirectory} does not exist");
                                }

                                IList<int> ganttSize = [.. options.GanttSize];

                                if (ganttSize.Count != 2)
                                {
                                    DisplayHelp(parserResult);
                                    return;
                                }

                                int width = ganttSize[0];
                                int height = ganttSize[1];

                                gantt.BuildGanttChartPlotModel();

                                PlotExport ganttFormat = options.GanttFormat;

                                string ganttOutputFile = Path.Combine(
                                    ganttDirectory,
                                    $@"{settingService.ProjectTitle}{Resource.ProjectPlan.Suffixes.Suffix_GanttChart}.{ganttFormat.GetDescription().ToLowerInvariant()}");

                                gantt.SaveGanttChartImageFileAsync(ganttOutputFile, width, height).Wait();
                            }
                        }

                        // Arrow graph export.
                        {
                            string? graphDirectory = options.ArrowGraphDirectory;
                            GraphExport graphFormat = options.ArrowGraphFormat;

                            if (graphDirectory is not null)
                            {
                                if (!Directory.Exists(graphDirectory))
                                {
                                    throw new InvalidOperationException($@"Directory {graphDirectory} does not exist");
                                }

                                arrow.BuildArrowGraphDiagramData();
                                arrow.BuildArrowGraphDiagramImage();

                                string graphOutputFile = Path.Combine(
                                    graphDirectory,
                                    $@"{settingService.ProjectTitle}{Resource.ProjectPlan.Suffixes.Suffix_ArrowChart}.{graphFormat.GetDescription().ToLowerInvariant()}");

                                arrow.SaveArrowGraphImageFileAsync(graphOutputFile).Wait();
                            }
                        }

                        // Vertex graph export.
                        {
                            string? graphDirectory = options.VertexGraphDirectory;
                            GraphExport graphFormat = options.VertexGraphFormat;

                            if (graphDirectory is not null)
                            {
                                if (!Directory.Exists(graphDirectory))
                                {
                                    throw new InvalidOperationException($@"Directory {graphDirectory} does not exist");
                                }

                                vertex.BuildVertexGraphDiagramData();
                                vertex.BuildVertexGraphDiagramImage();

                                string graphOutputFile = Path.Combine(
                                    graphDirectory,
                                    $@"{settingService.ProjectTitle}{Resource.ProjectPlan.Suffixes.Suffix_VertexChart}.{graphFormat.GetDescription().ToLowerInvariant()}");

                                vertex.SaveVertexGraphImageFileAsync(graphOutputFile).Wait();
                            }
                        }

                        // Resource chart export.
                        {
                            string? resourceDirectory = options.ResourceDirectory;

                            if (resourceDirectory is not null)
                            {
                                if (!Directory.Exists(resourceDirectory))
                                {
                                    throw new InvalidOperationException($@"Directory {resourceDirectory} does not exist");
                                }

                                IList<int> resourceSize = [.. options.ResourceSize];

                                if (resourceSize.Count != 2)
                                {
                                    DisplayHelp(parserResult);
                                    return;
                                }

                                int width = resourceSize[0];
                                int height = resourceSize[1];

                                resources.BuildResourceChartPlotModel();

                                PlotExport resourceFormat = options.ResourceFormat;

                                string resourceOutputFile = Path.Combine(
                                    resourceDirectory,
                                    $@"{settingService.ProjectTitle}{Resource.ProjectPlan.Suffixes.Suffix_ResourceChart}.{resourceFormat.GetDescription().ToLowerInvariant()}");

                                resources.SaveResourceChartImageFileAsync(resourceOutputFile, width, height).Wait();
                            }
                        }

                        // EV chart export.
                        {
                            string? evDirectory = options.EVDirectory;

                            if (evDirectory is not null)
                            {
                                if (!Directory.Exists(evDirectory))
                                {
                                    throw new InvalidOperationException($@"Directory {evDirectory} does not exist");
                                }

                                IList<int> evSize = [.. options.EVSize];

                                if (evSize.Count != 2)
                                {
                                    DisplayHelp(parserResult);
                                    return;
                                }

                                int width = evSize[0];
                                int height = evSize[1];

                                ev.BuildEarnedValueChartPlotModel();

                                PlotExport evFormat = options.EVFormat;

                                string evOutputFile = Path.Combine(
                                    evDirectory,
                                    $@"{settingService.ProjectTitle}{Resource.ProjectPlan.Suffixes.Suffix_EarnedValueChart}.{evFormat.GetDescription().ToLowerInvariant()}");

                                ev.SaveEarnedValueChartImageFileAsync(evOutputFile, width, height).Wait();
                            }
                        }

                        // Metrics.
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

                            Display(table.ToMarkDownString());
                        }
                    });

                return 0;
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync(ex.Message);
                return -1;
            }
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
    }
}
