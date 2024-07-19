using AutoMapper;
using CommandLine;
using CommandLine.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Zametek.Contract.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

// Using these as a starting point:
// https://github.com/jasonterando/dotnet-console-demo/
// https://medium.com/@eduardosilva_94960/mastering-command-line-parsing-in-net-core-with-commandlineparser-c20721100359
namespace Zametek.ProjectPlan.CommandLine
{
    internal class Program
    {
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

                var host = Host.CreateDefaultBuilder(args)
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
                        services.AddSingleton<IGanttChartManagerViewModel, GanttChartManagerViewModel>();

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





                var parserResult = parser.ParseArguments<Options>(args);

                parserResult
                    .WithParsed(options =>
                    {
                        string? inputFilename = options.InputFilename;
                        string? importFilename = options.ImportFilename;

                        string? outputFilename = options.OutputFilename;
                        string? exportFilename = options.ExportFilename;

                        bool compile = options.Compile;

                        IEnumerable<int> ganttSize = options.GanttSize;

                        //if (!Validate(options))
                        //{
                        //    Console.WriteLine("Validation fail");
                        //    var helpText = GetHelp(parserResult);
                        //    parser.Settings.HelpWriter.WriteLine(helpText);
                        //    return;
                        //}



                        var core = host.Services.GetRequiredService<ICoreViewModel>();
                        var gantt = host.Services.GetRequiredService<IGanttChartManagerViewModel>();

                        var projectFileOpen = host.Services.GetRequiredService<IProjectFileOpen>();
                        var projectFileImport = host.Services.GetRequiredService<IProjectFileImport>();

                        var projectFileSave = host.Services.GetRequiredService<IProjectFileSave>();
                        var projectFileExport = host.Services.GetRequiredService<IProjectFileExport>();

                        var settingService = host.Services.GetRequiredService<ISettingService>();









                        var plan = projectFileOpen.OpenProjectPlanFileAsync(inputFilename).Result;



                        core.ProcessProjectPlan(plan);


                        settingService.SetProjectFilePath(outputFilename);

                        string ganttOutputFile = Path.Combine(
                            settingService.ProjectDirectory,
                            $@"{settingService.ProjectTitle}.png");



                        gantt.SaveGanttChartImageFileAsync(ganttOutputFile, 500, 500).Wait();


                    });






                // Based upon verb/options, create services, including the task
                //var parserResult = Parser.Default.ParseArguments<CompileOptions, ExportOptions>(args);

                //parserResult
                //.WithParsed<CompileOptions>(options =>
                //{
                //    var core = host.Services.GetRequiredService<ICoreViewModel>();
                //    var projectFileOpen = host.Services.GetRequiredService<IProjectFileOpen>();
                //    var projectFileSave = host.Services.GetRequiredService<IProjectFileSave>();
                //    var settingService = host.Services.GetRequiredService<ISettingService>();

                //    string? inputFilename = options.InputFilename;
                //    string? outputFilename = options.OutputFilename;

                //    ArgumentException.ThrowIfNullOrWhiteSpace(inputFilename);
                //    ArgumentException.ThrowIfNullOrWhiteSpace(outputFilename);

                //    ProjectPlanModel planModel = projectFileOpen.OpenProjectPlanFileAsync(inputFilename).Result;
                //    core.ProcessProjectPlan(planModel);

                //    settingService.SetProjectFilePath(inputFilename);
                //    core.RunCompile();

                //    ProjectPlanModel projectPlan = core.BuildProjectPlan();

                //    projectFileSave.SaveProjectPlanFileAsync(projectPlan, outputFilename).Wait();
                //})
                //.WithParsed<ExportOptions>(options =>
                //{
                //    var core = host.Services.GetRequiredService<ICoreViewModel>();
                //    var projectFileOpen = host.Services.GetRequiredService<IProjectFileOpen>();
                //    var projectFileExport = host.Services.GetRequiredService<IProjectFileExport>();

                //    string? inputFilename = options.InputFilename;
                //    string? outputFilename = options.OutputFilename;

                //    ArgumentException.ThrowIfNullOrWhiteSpace(inputFilename);
                //    ArgumentException.ThrowIfNullOrWhiteSpace(outputFilename);

                //    ProjectPlanModel planModel = projectFileOpen.OpenProjectPlanFileAsync(inputFilename).Result;
                //    core.ProcessProjectPlan(planModel);

                //    projectFileExport.ExportProjectFile(
                //        planModel,
                //        core.ResourceSeriesSet,
                //        core.TrackingSeriesSet,
                //        core.ShowDates,
                //        outputFilename);
                //});











                //// If a task was set up to run (i.e. valid command line params) then run it
                //// and return the results
                //var task = host.Services.GetService<ITaskFactory>();





                return 0;


                //return task == null
                //    ? -1 // This can happen on --help or invalid arguments
                //    : await task.Launch();
            }
            catch (Exception ex)
            {
                // Note that this should only occur if something went wrong with building Host
                await Console.Error.WriteLineAsync(ex.Message);
                return -1;
            }
        }



        private static bool Validate(Options options)
        {
            //// do validation 
            //if (options.FileName == null)
            //    return false;
            return false;
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
