using Microsoft.Extensions.DependencyInjection;
using Zametek.Contract.ProjectPlan;
using Zametek.Graphs.Avalonia;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan.Engine
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the engine: <see cref="JobRunner"/>, and everything a job needs. Only what holds no state is
        /// shared between jobs. Everything that holds a project - or depends on something that does - is scoped, and
        /// JobRunner runs each job in a scope of its own. Where logging goes is the host's to decide: this registers
        /// the abstraction the view models log through, not a destination for it.
        /// </summary>
        public static IServiceCollection AddProjectPlanEngine(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddLogging();

            // Shared between jobs: immutable or stateless. The clock is the host's, which a job reads through a
            // JobClock of its own.
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton(new Data.ProjectPlan.VersionMapper());
            services.AddSingleton(new ProjectPlanMapper());
            // A job has no UI thread, so the work the view models would hand to one runs inline.
            services.AddSingleton<IUIDispatcher, InlineUIDispatcher>();
            services.AddSingleton<IGraphDispatcher, InlineGraphDispatcher>();
            services.AddSingleton<IGraphLayoutEngine, MsaglGraphLayoutEngine>();
            services.AddSingleton<IScottPlotImageExporter, ScottPlotImageExporter>();
            services.AddSingleton<IProjectFileSave, ProjectFileSave>();
            services.AddSingleton<JobRunner>();

            // One per job: what stands in for the desktop's settings, dialogs and
            // data grids...
            services.AddScoped<ISettingService, SettingService>();
            services.AddScoped<JobDialogService>();
            services.AddScoped<IDialogService>(x => x.GetRequiredService<JobDialogService>());
            services.AddScoped<IDataGridScrollManager, DataGridScrollManager>();

            // ...the job's clock, which reads the host's unless the request fixes the time...
            services.AddScoped<JobClock>();

            // ...and the project, with everything that works on it.
            services.AddScoped<IDateTimeCalculator>(x => new DateTimeCalculator(x.GetRequiredService<JobClock>()));

            services.AddScoped<IGraphCompilationService, GraphCompilationService>();
            services.AddScoped<IResourceSchedulingService, ResourceSchedulingService>();
            services.AddScoped<IMetricCalculationService, MetricCalculationService>();

            services.AddScoped<IProjectFileOpen, ProjectFileOpen>();
            services.AddScoped<IMicrosoftProjectFileImporter, MicrosoftProjectFileImporter>();
            services.AddScoped<IXlsxScenarioFileImporter, XlsxScenarioFileImporter>();
            services.AddScoped<IProjectScenarioFileImport, ProjectScenarioFileImport>();
            services.AddScoped<IXlsxScenarioFileExporter, XlsxScenarioFileExporter>();
            services.AddScoped<IProjectScenarioFileExport, ProjectScenarioFileExport>();

            services.AddScoped<ICoreViewModel, CoreViewModel>();
            services.AddScoped<IProjectScenarioManagerViewModel, ProjectScenarioManagerViewModel>();
            services.AddScoped<IGanttChartManagerViewModel, GanttChartManagerViewModel>();
            services.AddScoped<IArrowGraphManagerViewModel, ArrowGraphManagerViewModel>();
            services.AddScoped<IVertexGraphManagerViewModel, VertexGraphManagerViewModel>();
            services.AddScoped<IResourceChartManagerViewModel, ResourceChartManagerViewModel>();
            services.AddScoped<IEarnedValueChartManagerViewModel, EarnedValueChartManagerViewModel>();
            services.AddScoped<IScenarioChartManagerViewModel, ScenarioChartManagerViewModel>();
            services.AddScoped<IMetricManagerViewModel, MetricManagerViewModel>();
            services.AddScoped<IOutputManagerViewModel, OutputManagerViewModel>();

            return services;
        }
    }
}
