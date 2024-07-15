using AutoMapper;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Zametek.Common.ProjectPlan;
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

                        services.AddSingleton<IProjectFileImport, ProjectFileImport>();
                        services.AddSingleton<IProjectFileExport, ProjectFileExport>();
                        services.AddSingleton<IProjectFileOpen, ProjectFileOpen>();
                        services.AddSingleton<IProjectFileSave, ProjectFileSave>();





                        var config = new MapperConfiguration(cfg =>
                        {
                            cfg.AddProfile<Data.ProjectPlan.MapperProfile>();
                            cfg.AddProfile<MapperProfile>();
                        });
                        IMapper mapper = config.CreateMapper();

                        services.AddSingleton(mapper);








                        //// Set up our console output class
                        //services.AddSingleton<IConsoleOutput, ConsoleOutput>();

                    })
                    .UseSerilog()
                    .Build();






                // Based upon verb/options, create services, including the task
                var parserResult = Parser.Default.ParseArguments<CompileOptions>(args);
                parserResult
                    .WithParsed<CompileOptions>(options =>
                    {
                        var core = host.Services.GetRequiredService<ICoreViewModel>();
                        var projectFileOpen = host.Services.GetRequiredService<IProjectFileOpen>();
                        var projectFileSave = host.Services.GetRequiredService<IProjectFileSave>();
                        var settingService = host.Services.GetRequiredService<ISettingService>();






                        //services.AddSingleton<ExpressionEvaluator>();
                        //services.AddSingleton(options);
                        //services.AddSingleton<ITaskFactory, CalculateTaskFactory>();

                        string? inputFilename = options.InputFilename;
                        string? outputFilename = options.OutputFilename;

                        if (!string.IsNullOrWhiteSpace(inputFilename)
                            && !string.IsNullOrWhiteSpace(outputFilename))
                        {
                            ProjectPlanModel planModel = projectFileOpen.OpenProjectPlanFileAsync(inputFilename).Result;
                            core.ProcessProjectPlan(planModel);

                            settingService.SetProjectFilePath(inputFilename);
                            core.RunCompile();



                            var projectPlan = core.BuildProjectPlan();

                            projectFileSave.SaveProjectPlanFileAsync(projectPlan, outputFilename).Wait();

                        }



                    });
                //.WithParsed<StatisticsOptions>(options =>
                //{
                //    services.AddSingleton(options);
                //    services.AddSingleton<ITaskFactory, StatisticsTaskFactory>();
                //});



                //// If a task was set up to run (i.e. valid command line params) then run it
                //// and return the results
                //var task = host.Services.GetService<ITaskFactory>();





                return -1;


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
