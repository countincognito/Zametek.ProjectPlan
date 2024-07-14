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

                        //                    services.AddSingleton<ICoreViewModel, CoreViewModel>();
                        //                    services.AddSingleton<ISettingService, SettingServiceViewModel>();
                        //                    services.AddSingleton<ICoreViewModel, CoreViewModel>();

                        //                    SplatRegistrations.RegisterLazySingleton<IDialogService, DialogService>();
                        //                    ISettingService settingService,
                        //IDateTimeCalculator dateTimeCalculator,





                        //var config = new MapperConfiguration(cfg =>
                        //{
                        //    cfg.AddProfile<Data.ProjectPlan.MapperProfile>();
                        //    cfg.AddProfile<ViewModel.ProjectPlan.MapperProfile>();
                        //    cfg.AddProfile<View.ProjectPlan.MapperProfile>();
                        //});
                        //IMapper mapper = config.CreateMapper();

                        //SplatRegistrations.RegisterConstant(mapper);








                        //// Set up our console output class
                        //services.AddSingleton<IConsoleOutput, ConsoleOutput>();

                        //// Based upon verb/options, create services, including the task
                        //var parserResult = Parser.Default.ParseArguments<CalculateOptions, StatisticsOptions>(args);
                        //parserResult
                        //    .WithParsed<CalculateOptions>(options =>
                        //    {
                        //        services.AddSingleton<ExpressionEvaluator>();
                        //        services.AddSingleton(options);
                        //        services.AddSingleton<ITaskFactory, CalculateTaskFactory>();
                        //    })
                        //    .WithParsed<StatisticsOptions>(options =>
                        //    {
                        //        services.AddSingleton(options);
                        //        services.AddSingleton<ITaskFactory, StatisticsTaskFactory>();
                        //    });
                    })
                    .UseSerilog()
                    .Build();

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
