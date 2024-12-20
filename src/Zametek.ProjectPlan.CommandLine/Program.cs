using AutoMapper;
using CommandLine;
using CommandLine.Text;
using ConsoleTables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NPOI.SS.Formula.Functions;
using Serilog;
using System.Text;
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
        private const string c_GanttSuffix = @"-gantt";
        private const string c_GraphSuffix = @"-graph";
        private const string c_ResourceSuffix = @"-resource";
        private const string c_EVSuffix = @"-ev";

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

                        services.AddSingleton<ICoreViewModel, CoreViewModel>();
                        services.AddSingleton<ISettingService, SettingService>();
                        services.AddSingleton<IDialogService, DialogService>();
                        services.AddSingleton<IDateTimeCalculator, DateTimeCalculator>();
                        services.AddSingleton<IArrowGraphSerializer, ArrowGraphSerializer>();

                        services.AddSingleton<IGanttChartManagerViewModel, GanttChartManagerViewModel>();
                        services.AddSingleton<IArrowGraphManagerViewModel, ArrowGraphManagerViewModel>();
                        services.AddSingleton<IResourceChartManagerViewModel, ResourceChartManagerViewModel>();
                        services.AddSingleton<IEarnedValueChartManagerViewModel, EarnedValueChartManagerViewModel>();
                        services.AddSingleton<IMetricManagerViewModel, MetricManagerViewModel>();
                        services.AddSingleton<IOutputManagerViewModel, OutputManagerViewModel>();

                        services.AddSingleton<IProjectFileOpen, ProjectFileOpen>();
                        services.AddSingleton<IProjectFileImport, ProjectFileImport>();
                        services.AddSingleton<IProjectFileSave, ProjectFileSave>();
                        services.AddSingleton<IProjectFileExport, ProjectFileExport>();

                        var config = new MapperConfiguration(cfg =>
                        {
                            cfg.AddProfile<Data.ProjectPlan.MapperProfile>();
                            cfg.AddProfile<MapperProfile>();
                        });
                        IMapper mapper = config.CreateMapper();

                        services.AddSingleton(mapper);
                    })
                    .UseSerilog()
                    .Build();

                ICoreViewModel core = host.Services.GetRequiredService<ICoreViewModel>();
                core.KillSubscriptions();

                IProjectFileOpen projectFileOpen = host.Services.GetRequiredService<IProjectFileOpen>();
                IProjectFileImport projectFileImport = host.Services.GetRequiredService<IProjectFileImport>();

                IProjectFileSave projectFileSave = host.Services.GetRequiredService<IProjectFileSave>();
                IProjectFileExport projectFileExport = host.Services.GetRequiredService<IProjectFileExport>();

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

                        IArrowGraphManagerViewModel graph = host.Services.GetRequiredService<IArrowGraphManagerViewModel>();
                        graph.KillSubscriptions();

                        IResourceChartManagerViewModel resouces = host.Services.GetRequiredService<IResourceChartManagerViewModel>();
                        resouces.KillSubscriptions();

                        IEarnedValueChartManagerViewModel ev = host.Services.GetRequiredService<IEarnedValueChartManagerViewModel>();
                        ev.KillSubscriptions();

                        // File in.
                        {

                            string? inputFilename = options.InputFilename;
                            string? importFilename = options.ImportFilename;

                            if (inputFilename is not null)
                            {
                                ProjectPlanModel projectPlan = projectFileOpen.OpenProjectPlanFileAsync(inputFilename).Result;
                                core.ProcessProjectPlan(projectPlan);
                            }
                            else if (importFilename is not null)
                            {
                                ProjectImportModel projectImport = projectFileImport.ImportProjectFile(importFilename);
                                core.ProcessProjectImport(projectImport);
                            }
                            else
                            {
                                DisplayHelp(parserResult);
                                return;
                            }
                        }

                        // Use business days.
                        {
                            core.UseBusinessDays = options.UseBusinessDays ?? default;
                        }

                        // Classic date format.
                        {
                            core.UseClassicDates = options.ClassicDateFormat ?? default;
                        }

                        // Show dates.
                        {
                            core.ShowDates = options.ShowDates ?? default;
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

                            core.BuildCyclomaticComplexity();
                            core.BuildArrowGraph();
                            core.BuildResourceSeriesSet();
                            core.BuildTrackingSeriesSet();

                            metrics.BuildMetrics();
                            metrics.BuildCostsAndEfforts();
                        }

                        // File out.
                        {
                            string? outputFilename = options.OutputFilename;
                            string? exportFilename = options.ExportFilename;

                            if (outputFilename is not null)
                            {
                                settingService.SetProjectFilePath(outputFilename, bindTitleToFilename: false);
                                ProjectPlanModel plan = core.BuildProjectPlan();
                                projectFileSave.SaveProjectPlanFileAsync(plan, outputFilename).Wait();
                            }
                            if (exportFilename is not null)
                            {
                                settingService.SetProjectFilePath(exportFilename, bindTitleToFilename: false);
                                ProjectPlanModel plan = core.BuildProjectPlan();
                                projectFileExport.ExportProjectFile(
                                    plan,
                                    core.ResourceSeriesSet,
                                    core.TrackingSeriesSet,
                                    core.ShowDates,
                                    exportFilename);
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

                                GroupByMode ganttGroup = options.GanttGroup;
                                AnnotationStyle ganttAnnotate = options.GanttAnnotate;
                                bool labelGroups = options.GanttLabel ?? default;
                                bool showProjectFinish = options.GanttFinish ?? default;
                                bool showTracking = options.GanttTracking ?? default;

                                gantt.GroupByMode = ganttGroup;
                                gantt.AnnotationStyle = ganttAnnotate;
                                gantt.LabelGroups = labelGroups;
                                gantt.ShowProjectFinish = showProjectFinish;
                                gantt.ShowTracking = showTracking;

                                gantt.BuildGanttChartPlotModel();

                                PlotExport ganttFormat = options.GanttFormat;

                                string ganttOutputFile = Path.Combine(
                                    ganttDirectory,
                                    $@"{settingService.ProjectTitle}{c_GanttSuffix}.{ganttFormat.GetDescription().ToLowerInvariant()}");

                                gantt.SaveGanttChartImageFileAsync(ganttOutputFile, width, height).Wait();
                            }
                        }

                        // Arrow graph export.
                        {
                            string? graphDirectory = options.GraphDirectory;
                            GraphExport graphFormat = options.GraphFormat;

                            if (graphDirectory is not null)
                            {
                                if (!Directory.Exists(graphDirectory))
                                {
                                    throw new InvalidOperationException($@"Directory {graphDirectory} does not exist");
                                }

                                graph.BuildArrowGraphDiagramData();
                                graph.BuildArrowGraphDiagramImage();

                                string graphOutputFile = Path.Combine(
                                    graphDirectory,
                                    $@"{settingService.ProjectTitle}{c_GraphSuffix}.{graphFormat.GetDescription().ToLowerInvariant()}");

                                graph.SaveArrowGraphImageFileAsync(graphOutputFile).Wait();
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

                                resouces.BuildResourceChartPlotModel();

                                PlotExport resourceFormat = options.ResourceFormat;

                                string resourceOutputFile = Path.Combine(
                                    resourceDirectory,
                                    $@"{settingService.ProjectTitle}{c_ResourceSuffix}.{resourceFormat.GetDescription().ToLowerInvariant()}");

                                resouces.SaveResourceChartImageFileAsync(resourceOutputFile, width, height).Wait();
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

                                bool evProjections = options.EVProjections ?? default;
                                ev.ViewProjections = evProjections;

                                ev.BuildEarnedValueChartPlotModel();

                                PlotExport evFormat = options.EVFormat;

                                string evOutputFile = Path.Combine(
                                    evDirectory,
                                    $@"{settingService.ProjectTitle}{c_EVSuffix}.{evFormat.GetDescription().ToLowerInvariant()}");

                                ev.SaveEarnedValueChartImageFileAsync(evOutputFile, width, height).Wait();
                            }
                        }

                        // Metrics.
                        {
                            var table = new ConsoleTable(Resource.ProjectPlan.Titles.Title_Metrics, Resource.ProjectPlan.Titles.Title_Values);

                            table.AddRow(Resource.ProjectPlan.Labels.Label_ActivityRisk, $@"{metrics.ActivityRisk:F3}");
                            table.AddRow(Resource.ProjectPlan.Labels.Label_ActivityRiskWithStdDevCorrection, $@"{metrics.ActivityRiskWithStdDevCorrection:F3}");
                            table.AddRow(Resource.ProjectPlan.Labels.Label_CriticalityRisk, $@"{metrics.CriticalityRisk:F3}");
                            table.AddRow(Resource.ProjectPlan.Labels.Label_FibonacciRisk, $@"{metrics.FibonacciRisk:F3}");

                            table.AddRow(Resource.ProjectPlan.Labels.Label_GeometricActivityRisk, $@"{metrics.GeometricActivityRisk:F3}");
                            table.AddRow(Resource.ProjectPlan.Labels.Label_GeometricCriticalityRisk, $@"{metrics.GeometricCriticalityRisk:F3}");
                            table.AddRow(Resource.ProjectPlan.Labels.Label_GeometricFibonacciRisk, $@"{metrics.GeometricFibonacciRisk:F3}");
                            table.AddRow(Resource.ProjectPlan.Labels.Label_CyclomaticComplexity, $@"{metrics.CyclomaticComplexity}");

                            table.AddRow(Resource.ProjectPlan.Labels.Label_ActivityEffort, $@"{metrics.ActivityEffort:F0}");
                            table.AddRow(Resource.ProjectPlan.Labels.Label_DurationManMonths, $@"{metrics.DurationManMonths:F1}");
                            table.AddRow(Resource.ProjectPlan.Labels.Label_ProjectFinish, $@"{metrics.ProjectFinish}");
                            table.AddRow(Resource.ProjectPlan.Labels.Label_Efficiency, $@"{metrics.Efficiency:F3}");

                            table.AddRow(Resource.ProjectPlan.Labels.Label_DirectEffort, $@"{metrics.DirectEffort:F0}");
                            table.AddRow(Resource.ProjectPlan.Labels.Label_IndirectEffort, $@"{metrics.IndirectEffort:F0}");
                            table.AddRow(Resource.ProjectPlan.Labels.Label_OtherEffort, $@"{metrics.OtherEffort:F0}");
                            table.AddRow(Resource.ProjectPlan.Labels.Label_TotalEffort, $@"{metrics.TotalEffort:F0}");

                            table.AddRow(Resource.ProjectPlan.Labels.Label_DirectCost, $@"{metrics.DirectCost:F2}");
                            table.AddRow(Resource.ProjectPlan.Labels.Label_IndirectCost, $@"{metrics.IndirectCost:F2}");
                            table.AddRow(Resource.ProjectPlan.Labels.Label_OtherCost, $@"{metrics.OtherCost:F2}");
                            table.AddRow(Resource.ProjectPlan.Labels.Label_TotalCost, $@"{metrics.TotalCost:F2}");

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
