using AutoMapper;
using CommandLine;
using CommandLine.Text;
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
        private const string c_GanttSuffix = @"-gantt";
        private const string c_GraphSuffix = @"-graph";
        private const string c_ResourceSuffix = @"-resource";
        private const string c_EVSuffix = @"-ev";

        /// <summary>
        /// Main routine
        /// </summary>
        /// <param name="args"></param>
        /// <returns>Exit code</returns>
        public static async Task<int> Main(string[] args)
        {
            try
            {
                var parser = new Parser(with =>
                {
                    with.CaseInsensitiveEnumValues = true;
                    with.HelpWriter = Console.Error;
                });

                IHost host = Host.CreateDefaultBuilder(args)
                    .ConfigureServices((context, services) =>
                    {
                        //// Configure Serilog
                        //Log.Logger = new LoggerConfiguration()
                        //    .ReadFrom.Configuration(context.Configuration)
                        //    .CreateLogger();

                        services.AddSingleton(parser.Settings.HelpWriter);

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
                    .WithParsed(options =>
                    {
                        TextWriter writer = host.Services.GetRequiredService<TextWriter>();
                        string helpText = GetHelp(parserResult);

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
                                writer.WriteLine(helpText);
                                return;
                            }
                        }

                        // Use business days.
                        {
                            core.UseBusinessDays = options.UseBusinessDays ?? default;
                        }

                        // Show dates.
                        {
                            core.ShowDates = options.ShowDates ?? default;
                        }

                        // Compile.
                        {
                            core.RunCompile();
                            outputs.BuildCompilationOutput();

                            if (core.HasCompilationErrors)
                            {
                                writer.WriteLine(outputs.CompilationOutput);
                                return;
                            }

                            metrics.BuildMetrics();

                            core.BuildCyclomaticComplexity();
                            core.BuildArrowGraph();
                            core.BuildResourceSeriesSet();
                            core.BuildTrackingSeriesSet();
                        }

                        // File out.
                        {
                            string? outputFilename = options.OutputFilename;
                            string? exportFilename = options.ExportFilename;

                            if (outputFilename is not null)
                            {
                                settingService.SetProjectFilePath(outputFilename);
                                ProjectPlanModel plan = core.BuildProjectPlan();
                                projectFileSave.SaveProjectPlanFileAsync(plan, outputFilename).Wait();
                            }
                            else if (exportFilename is not null)
                            {
                                settingService.SetProjectFilePath(exportFilename);
                                ProjectPlanModel plan = core.BuildProjectPlan();
                                projectFileExport.ExportProjectFile(
                                    plan,
                                    core.ResourceSeriesSet,
                                    core.TrackingSeriesSet,
                                    core.ShowDates,
                                    exportFilename);
                            }
                            else
                            {
                                writer.WriteLine(helpText);
                                return;
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
                                    writer.WriteLine(helpText);
                                    return;
                                }

                                int width = ganttSize[0];
                                int height = ganttSize[1];

                                GroupByMode ganttGroup = options.GanttGroup;
                                bool ganttAnnotate = options.GanttAnnotate ?? default;

                                gantt.GroupByMode = ganttGroup;
                                gantt.AnnotateGroups = ganttAnnotate;

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
                                    writer.WriteLine(helpText);
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
                                    writer.WriteLine(helpText);
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
                    });

                return 0;
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync(ex.Message);
                return -1;
            }
        }

        //Generate Help text
        private static string GetHelp<T>(ParserResult<T> result)
        {
            // use default configuration
            // you can customize HelpText and pass different configuratins
            //see wiki
            // https://github.com/commandlineparser/commandline/wiki/How-To#q1
            // https://github.com/commandlineparser/commandline/wiki/HelpText-Configuration
            return HelpText.AutoBuild(result, h => h, e => e);
        }
    }
}
