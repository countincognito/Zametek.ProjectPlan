using Autofac;
using Autofac.Extensions.DependencyInjection;
using Dock.Model.Core;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Splat;
using Splat.Autofac;
using System;
using Zametek.Contract.ProjectPlan;
using Zametek.Graphs.ProjectPlan;
using Zametek.View.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan
{
    public static class Bootstrapper
    {
        public static void RegisterIOC()
        {
            // 1. Create the Autofac builder
            var builder = new ContainerBuilder();

            builder.Register(c => new AutofacServiceProvider(c.Resolve<ILifetimeScope>()))
                .As<IServiceProvider>()
                .InstancePerLifetimeScope();

            // File settings.
            string settingsFilename = SettingFileHelper.DefaultUserSettingsFileLocation();
            string dockLayoutFilename = SettingFileHelper.DefaultDockLayoutFileLocation();
            string dataGridLayoutFilename = SettingFileHelper.DefaultDataGridLayoutFileLocation();
            var settingService = new SettingService(settingsFilename, dockLayoutFilename, dataGridLayoutFilename);

            builder.RegisterInstance(settingService)
                .As<ISettingService>()
                .As<SettingService>();

            // 2. Register services and ViewModels
            builder.RegisterInstance(TimeProvider.System);
            builder.RegisterType<DateTimeCalculator>()
                .As<IDateTimeCalculator>()
                .As<DateTimeCalculator>()
                .SingleInstance();
            builder.RegisterType<GraphImageExporter>()
                .As<IGraphImageExporter>()
                .As<GraphImageExporter>()
                .SingleInstance();
            builder.RegisterType<MsaglSvgRenderer>()
                .As<IMsaglSvgRenderer>()
                .As<MsaglSvgRenderer>()
                .SingleInstance();
            builder.RegisterType<ArrowGraphSerializer>()
                .As<IArrowGraphSerializer>()
                .As<ArrowGraphSerializer>()
                .SingleInstance();
            builder.RegisterType<VertexGraphSerializer>()
                .As<IVertexGraphSerializer>()
                .As<VertexGraphSerializer>()
                .SingleInstance();
            builder.RegisterType<MicrosoftProjectFileImporter>()
                .As<IMicrosoftProjectFileImporter>()
                .As<MicrosoftProjectFileImporter>()
                .SingleInstance();
            builder.RegisterType<XlsxFileImporter>()
                .As<IXlsxFileImporter>()
                .As<XlsxFileImporter>()
                .SingleInstance();
            builder.RegisterType<ProjectScenarioFileImport>()
                .As<IProjectScenarioFileImport>()
                .As<ProjectScenarioFileImport>()
                .SingleInstance();
            builder.RegisterType<ScottPlotImageExporter>()
                .As<IScottPlotImageExporter>()
                .As<ScottPlotImageExporter>()
                .SingleInstance();
            builder.RegisterType<XlsxScenarioExporter>()
                .As<IXlsxScenarioExporter>()
                .As<XlsxScenarioExporter>()
                .SingleInstance();
            builder.RegisterType<ProjectScenarioFileExport>()
                .As<IProjectScenarioFileExport>()
                .As<ProjectScenarioFileExport>()
                .SingleInstance();
            builder.RegisterType<ProjectFileOpen>()
                .As<IProjectFileOpen>()
                .As<ProjectFileOpen>()
                .SingleInstance();
            builder.RegisterType<ProjectFileSave>()
                .As<IProjectFileSave>()
                .As<ProjectFileSave>()
                .SingleInstance();
            builder.RegisterType<DialogService>()
                .As<IDialogService>()
                .As<DialogService>()
                .SingleInstance();
            builder.RegisterType<GraphCompilationService>()
                .As<IGraphCompilationService>()
                .As<GraphCompilationService>()
                .SingleInstance();
            builder.RegisterType<ResourceSchedulingService>()
                .As<IResourceSchedulingService>()
                .As<ResourceSchedulingService>()
                .SingleInstance();
            builder.RegisterType<MetricCalculationService>()
                .As<IMetricCalculationService>()
                .As<MetricCalculationService>()
                .SingleInstance();
            builder.RegisterType<CoreViewModel>()
                .As<ICoreViewModel>()
                .As<CoreViewModel>()
                .SingleInstance();
            builder.RegisterType<ActivitiesManagerViewModel>()
                .As<IActivitiesManagerViewModel>()
                .As<ActivitiesManagerViewModel>()
                .SingleInstance();
            builder.RegisterType<TrackingManagerViewModel>()
                .As<ITrackingManagerViewModel>()
                .As<TrackingManagerViewModel>()
                .SingleInstance();
            builder.RegisterType<ArrowGraphManagerViewModel>()
                .As<IArrowGraphManagerViewModel>()
                .As<ArrowGraphManagerViewModel>()
                .SingleInstance();
            builder.RegisterType<VertexGraphManagerViewModel>()
                .As<IVertexGraphManagerViewModel>()
                .As<VertexGraphManagerViewModel>()
                .SingleInstance();
            builder.RegisterType<ResourceChartManagerViewModel>()
                .As<IResourceChartManagerViewModel>()
                .As<ResourceChartManagerViewModel>()
                .SingleInstance();
            builder.RegisterType<GanttChartManagerViewModel>()
                .As<IGanttChartManagerViewModel>()
                .As<GanttChartManagerViewModel>()
                .SingleInstance();
            builder.RegisterType<EarnedValueChartManagerViewModel>()
                .As<IEarnedValueChartManagerViewModel>()
                .As<EarnedValueChartManagerViewModel>()
                .SingleInstance();
            builder.RegisterType<MetricManagerViewModel>()
                .As<IMetricManagerViewModel>()
                .As<MetricManagerViewModel>()
                .SingleInstance();
            builder.RegisterType<OutputManagerViewModel>()
                .As<IOutputManagerViewModel>()
                .As<OutputManagerViewModel>()
                .SingleInstance();
            builder.RegisterType<GraphSettingsManagerViewModel>()
                .As<IGraphSettingsManagerViewModel>()
                .As<GraphSettingsManagerViewModel>()
                .SingleInstance();
            builder.RegisterType<ResourceSettingsManagerViewModel>()
                .As<IResourceSettingsManagerViewModel>()
                .As<ResourceSettingsManagerViewModel>()
                .SingleInstance();
            builder.RegisterType<WorkStreamSettingsManagerViewModel>()
                .As<IWorkStreamSettingsManagerViewModel>()
                .As<WorkStreamSettingsManagerViewModel>()
                .SingleInstance();
            builder.RegisterType<HolidaySettingsManagerViewModel>()
                .As<IHolidaySettingsManagerViewModel>()
                .As<HolidaySettingsManagerViewModel>()
                .SingleInstance();
            builder.RegisterType<ProjectScenarioManagerViewModel>()
                .As<IProjectScenarioManagerViewModel>()
                .As<ProjectScenarioManagerViewModel>()
                .SingleInstance();
            builder.RegisterType<ScenarioChartManagerViewModel>()
                .As<IScenarioChartManagerViewModel>()
                .As<ScenarioChartManagerViewModel>()
                .SingleInstance();
            builder.RegisterType<MainViewModel>()
                .As<IMainViewModel>()
                .As<MainViewModel>()
                .SingleInstance();
            builder.RegisterType<DockFactory>()
                .As<IFactory>()
                .As<DockFactory>()
                .SingleInstance();

            // Views.
            builder.RegisterType<ActivitiesManagerView>()
                .AsSelf()
                .SingleInstance();
            builder.RegisterType<TrackingManagerView>()
                .AsSelf()
                .SingleInstance();
            builder.RegisterType<ArrowGraphManagerView>()
                .AsSelf()
                .SingleInstance();
            builder.RegisterType<VertexGraphManagerView>()
                .AsSelf()
                .SingleInstance();
            builder.RegisterType<ResourceChartManagerView>()
                .AsSelf()
                .SingleInstance();
            builder.RegisterType<GanttChartManagerView>()
                .AsSelf()
                .SingleInstance();
            builder.RegisterType<EarnedValueChartManagerView>()
                .AsSelf()
                .SingleInstance();
            builder.RegisterType<MetricManagerView>()
                .AsSelf()
                .SingleInstance();
            builder.RegisterType<OutputManagerView>()
                .AsSelf()
                .SingleInstance();
            builder.RegisterType<GraphSettingsManagerView>()
                .AsSelf()
                .SingleInstance();
            builder.RegisterType<ResourceSettingsManagerView>()
                .AsSelf()
                .SingleInstance();
            builder.RegisterType<WorkStreamSettingsManagerView>()
                .AsSelf()
                .SingleInstance();
            builder.RegisterType<HolidaySettingsManagerView>()
                .AsSelf()
                .SingleInstance();
            builder.RegisterType<ProjectScenarioManagerView>()
                .AsSelf()
                .SingleInstance();
            builder.RegisterType<ScenarioChartManagerView>()
                .AsSelf()
                .SingleInstance();
            builder.RegisterType<MainView>()
                .AsSelf()
                .SingleInstance();

            builder.RegisterInstance(new Data.ProjectPlan.VersionMapper());
            builder.RegisterInstance(new ViewModel.ProjectPlan.ProjectPlanMapper());
            builder.RegisterInstance(new View.ProjectPlan.ProjectPlanMapper());

            builder.RegisterType<CommitEditHandler>()
                .As<ICommitEditHandler>()
                .As<CommitEditHandler>()
                .SingleInstance();

            builder.RegisterType<DataGridManager>()
                .As<IDataGridManager>()
                .As<DataGridManager>()
                .SingleInstance();

            // 3. Use the Autofac resolver for Splat
            // This tells Splat/ReactiveUI to look into Autofac for dependencies
            AutofacDependencyResolver autofacResolver = builder.UseAutofacDependencyResolver();
            Locator.SetLocator(autofacResolver);

            // https://stackoverflow.com/questions/65110470/how-to-use-autofac-as-di-container-in-avalonia-reactiveui
            // 4. Initialize ReactiveUI/Splat integration
            autofacResolver.InitializeSplat();
            autofacResolver.InitializeReactiveUI();

            // This is important for ensuring that UI components are instantiated
            // on the correct thread.
            autofacResolver.RegisterConstant<IActivationForViewFetcher>(new AvaloniaActivationForViewFetcher());
            autofacResolver.RegisterConstant<IPropertyBindingHook>(new AutoDataTemplateBindingHook());
            RxApp.MainThreadScheduler = AvaloniaScheduler.Instance;

            // 5. Build the container and set the lifetime scope
            IContainer container = builder.Build();
            autofacResolver.SetLifetimeScope(container);
        }
    }
}
