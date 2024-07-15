using AutoMapper;
using CommandLine;
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
                var host = Host.CreateDefaultBuilder(args)
                    .ConfigureServices((context, services) =>
                    {
                        //// Configure Serilog
                        //Log.Logger = new LoggerConfiguration()
                        //    .ReadFrom.Configuration(context.Configuration)
                        //    .CreateLogger();

                        services.AddSingleton<ICoreViewModel, CoreViewModel>();
                        services.AddSingleton<ISettingService, SettingService>();
                        services.AddSingleton<IDateTimeCalculator, DateTimeCalculator>();

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





                var parserResult = Parser.Default.ParseArguments<Options>(args);




                parserResult
                    .WithParsed(options =>
                    {

                        var core = host.Services.GetRequiredService<ICoreViewModel>();

                        var projectFileOpen = host.Services.GetRequiredService<IProjectFileOpen>();
                        var projectFileImport = host.Services.GetRequiredService<IProjectFileImport>();

                        var projectFileSave = host.Services.GetRequiredService<IProjectFileSave>();
                        var projectFileExport = host.Services.GetRequiredService<IProjectFileExport>();

                        var settingService = host.Services.GetRequiredService<ISettingService>();

                        string? inputFilename = options.InputFilename;
                        string? importFilename = options.ImportFilename;

                        string? outputFilename = options.OutputFilename;
                        string? exportFilename = options.ExportFilename;

                        bool compile = options.Compile;

                        IEnumerable<int> ganttSize = options.GanttSize;





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
    }
}
