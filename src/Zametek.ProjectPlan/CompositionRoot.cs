using Autofac;
using Autofac.Extensions.DependencyInjection;
using Dock.Model.Core;
using Dock.Serializer.SystemTextJson;
using Splat;
using Splat.Autofac;
using System;
using Zametek.Contract.ProjectPlan;
using Zametek.Graphs.ProjectPlan;
using Zametek.View.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan
{
    public static class CompositionRoot
    {
        private static ContainerBuilder? s_Builder;
        private static AutofacDependencyResolver? s_Resolver;

        public static IContainer Container { get; private set; } = null!;

        // Configure() must run before BuildAvaloniaApp().UseReactiveUI(...) so that
        // AutofacDependencyResolver is the active AppLocator when ReactiveUI registers
        // its plugins (ICreatesObservableForProperty, binders, etc.) — otherwise those
        // registrations land in the default ModernDependencyResolver and are lost when
        // we later swap in the Autofac one.
        public static void Configure()
        {
            s_Builder = new ContainerBuilder();

            s_Resolver = s_Builder.UseAutofacDependencyResolver();
            s_Resolver.InitializeSplat();

            s_Builder.Register(c => new AutofacServiceProvider(c.Resolve<ILifetimeScope>()))
                .As<IServiceProvider>()
                .InstancePerLifetimeScope();

            // File settings.
            string settingsFilename = SettingFileHelper.DefaultUserSettingsFileLocation();
            string dockLayoutFilename = SettingFileHelper.DefaultDockLayoutFileLocation();
            string dataGridLayoutFilename = SettingFileHelper.DefaultDataGridLayoutFileLocation();
            var settingService = new SettingService(settingsFilename, dockLayoutFilename, dataGridLayoutFilename);

            s_Builder.RegisterInstance(settingService)
                .As<ISettingService>()
                .As<SettingService>();

            // Services and ViewModels.
            s_Builder.RegisterInstance(TimeProvider.System);
            s_Builder.RegisterType<DateTimeCalculator>()
                .As<IDateTimeCalculator>()
                .As<DateTimeCalculator>()
                .SingleInstance();
            s_Builder.RegisterType<GraphImageExporter>()
                .As<IGraphImageExporter>()
                .As<GraphImageExporter>()
                .SingleInstance();
            s_Builder.RegisterType<MsaglSvgRenderer>()
                .As<IMsaglSvgRenderer>()
                .As<MsaglSvgRenderer>()
                .SingleInstance();
            s_Builder.RegisterType<ArrowGraphSerializer>()
                .As<IArrowGraphSerializer>()
                .As<ArrowGraphSerializer>()
                .SingleInstance();
            s_Builder.RegisterType<VertexGraphSerializer>()
                .As<IVertexGraphSerializer>()
                .As<VertexGraphSerializer>()
                .SingleInstance();
            s_Builder.RegisterType<MicrosoftProjectFileImporter>()
                .As<IMicrosoftProjectFileImporter>()
                .As<MicrosoftProjectFileImporter>()
                .SingleInstance();
            s_Builder.RegisterType<XlsxFileImporter>()
                .As<IXlsxFileImporter>()
                .As<XlsxFileImporter>()
                .SingleInstance();
            s_Builder.RegisterType<ProjectScenarioFileImport>()
                .As<IProjectScenarioFileImport>()
                .As<ProjectScenarioFileImport>()
                .SingleInstance();
            s_Builder.RegisterType<ScottPlotImageExporter>()
                .As<IScottPlotImageExporter>()
                .As<ScottPlotImageExporter>()
                .SingleInstance();
            s_Builder.RegisterType<XlsxScenarioExporter>()
                .As<IXlsxScenarioExporter>()
                .As<XlsxScenarioExporter>()
                .SingleInstance();
            s_Builder.RegisterType<ProjectScenarioFileExport>()
                .As<IProjectScenarioFileExport>()
                .As<ProjectScenarioFileExport>()
                .SingleInstance();
            s_Builder.RegisterType<ProjectFileOpen>()
                .As<IProjectFileOpen>()
                .As<ProjectFileOpen>()
                .SingleInstance();
            s_Builder.RegisterType<ProjectFileSave>()
                .As<IProjectFileSave>()
                .As<ProjectFileSave>()
                .SingleInstance();
            s_Builder.RegisterType<DialogService>()
                .As<IDialogService>()
                .As<DialogService>()
                .SingleInstance();
            s_Builder.RegisterType<GraphCompilationService>()
                .As<IGraphCompilationService>()
                .As<GraphCompilationService>()
                .SingleInstance();
            s_Builder.RegisterType<ResourceSchedulingService>()
                .As<IResourceSchedulingService>()
                .As<ResourceSchedulingService>()
                .SingleInstance();
            s_Builder.RegisterType<MetricCalculationService>()
                .As<IMetricCalculationService>()
                .As<MetricCalculationService>()
                .SingleInstance();
            s_Builder.RegisterType<CoreViewModel>()
                .As<ICoreViewModel>()
                .As<CoreViewModel>()
                .SingleInstance();
            s_Builder.RegisterType<ActivitiesManagerViewModel>()
                .As<IActivitiesManagerViewModel>()
                .As<ActivitiesManagerViewModel>()
                .SingleInstance();
            s_Builder.RegisterType<TrackingManagerViewModel>()
                .As<ITrackingManagerViewModel>()
                .As<TrackingManagerViewModel>()
                .SingleInstance();
            s_Builder.RegisterType<ArrowGraphManagerViewModel>()
                .As<IArrowGraphManagerViewModel>()
                .As<ArrowGraphManagerViewModel>()
                .SingleInstance();
            s_Builder.RegisterType<VertexGraphManagerViewModel>()
                .As<IVertexGraphManagerViewModel>()
                .As<VertexGraphManagerViewModel>()
                .SingleInstance();
            s_Builder.RegisterType<ResourceChartManagerViewModel>()
                .As<IResourceChartManagerViewModel>()
                .As<ResourceChartManagerViewModel>()
                .SingleInstance();
            s_Builder.RegisterType<GanttChartManagerViewModel>()
                .As<IGanttChartManagerViewModel>()
                .As<GanttChartManagerViewModel>()
                .SingleInstance();
            s_Builder.RegisterType<EarnedValueChartManagerViewModel>()
                .As<IEarnedValueChartManagerViewModel>()
                .As<EarnedValueChartManagerViewModel>()
                .SingleInstance();
            s_Builder.RegisterType<MetricManagerViewModel>()
                .As<IMetricManagerViewModel>()
                .As<MetricManagerViewModel>()
                .SingleInstance();
            s_Builder.RegisterType<OutputManagerViewModel>()
                .As<IOutputManagerViewModel>()
                .As<OutputManagerViewModel>()
                .SingleInstance();
            s_Builder.RegisterType<GraphSettingsManagerViewModel>()
                .As<IGraphSettingsManagerViewModel>()
                .As<GraphSettingsManagerViewModel>()
                .SingleInstance();
            s_Builder.RegisterType<ResourceSettingsManagerViewModel>()
                .As<IResourceSettingsManagerViewModel>()
                .As<ResourceSettingsManagerViewModel>()
                .SingleInstance();
            s_Builder.RegisterType<WorkStreamSettingsManagerViewModel>()
                .As<IWorkStreamSettingsManagerViewModel>()
                .As<WorkStreamSettingsManagerViewModel>()
                .SingleInstance();
            s_Builder.RegisterType<HolidaySettingsManagerViewModel>()
                .As<IHolidaySettingsManagerViewModel>()
                .As<HolidaySettingsManagerViewModel>()
                .SingleInstance();
            s_Builder.RegisterType<ProjectScenarioManagerViewModel>()
                .As<IProjectScenarioManagerViewModel>()
                .As<ProjectScenarioManagerViewModel>()
                .SingleInstance();
            s_Builder.RegisterType<ScenarioChartManagerViewModel>()
                .As<IScenarioChartManagerViewModel>()
                .As<ScenarioChartManagerViewModel>()
                .SingleInstance();
            s_Builder.RegisterType<MainViewModel>()
                .As<IMainViewModel>()
                .As<MainViewModel>()
                .SingleInstance();
            s_Builder.RegisterType<DockFactory>()
                .As<IFactory>()
                .As<DockFactory>()
                .SingleInstance();
            s_Builder.RegisterType<DockSerializer>()
                .As<IDockSerializer>()
                .As<DockSerializer>()
                .SingleInstance();

            // Views.
            // Docked views must be transient: the ViewLocator resolves them from this
            // container, and Dock re-materialises a dockable's content whenever it is
            // floated, redocked, or the layout is reset. A singleton Control cannot live
            // in two visual trees, so reparenting a shared instance leaves the previous
            // location blank. InstancePerDependency hands out a fresh Control per resolve.
            s_Builder.RegisterType<ActivitiesManagerView>()
                .AsSelf()
                .InstancePerDependency();
            s_Builder.RegisterType<TrackingManagerView>()
                .AsSelf()
                .InstancePerDependency();
            s_Builder.RegisterType<ArrowGraphManagerView>()
                .AsSelf()
                .InstancePerDependency();
            s_Builder.RegisterType<VertexGraphManagerView>()
                .AsSelf()
                .InstancePerDependency();
            s_Builder.RegisterType<ResourceChartManagerView>()
                .AsSelf()
                .InstancePerDependency();
            s_Builder.RegisterType<GanttChartManagerView>()
                .AsSelf()
                .InstancePerDependency();
            s_Builder.RegisterType<EarnedValueChartManagerView>()
                .AsSelf()
                .InstancePerDependency();
            s_Builder.RegisterType<MetricManagerView>()
                .AsSelf()
                .InstancePerDependency();
            s_Builder.RegisterType<OutputManagerView>()
                .AsSelf()
                .InstancePerDependency();
            s_Builder.RegisterType<GraphSettingsManagerView>()
                .AsSelf()
                .InstancePerDependency();
            s_Builder.RegisterType<ResourceSettingsManagerView>()
                .AsSelf()
                .InstancePerDependency();
            s_Builder.RegisterType<WorkStreamSettingsManagerView>()
                .AsSelf()
                .InstancePerDependency();
            s_Builder.RegisterType<HolidaySettingsManagerView>()
                .AsSelf()
                .InstancePerDependency();
            s_Builder.RegisterType<ProjectScenarioManagerView>()
                .AsSelf()
                .InstancePerDependency();
            s_Builder.RegisterType<ScenarioChartManagerView>()
                .AsSelf()
                .InstancePerDependency();
            s_Builder.RegisterType<MainView>()
                .AsSelf()
                .SingleInstance();

            s_Builder.RegisterInstance(new Data.ProjectPlan.VersionMapper());
            s_Builder.RegisterInstance(new ViewModel.ProjectPlan.ProjectPlanMapper());
            s_Builder.RegisterInstance(new View.ProjectPlan.ProjectPlanMapper());

            s_Builder.RegisterType<CommitEditHandler>()
                .As<ICommitEditHandler>()
                .As<CommitEditHandler>()
                .SingleInstance();

            s_Builder.RegisterType<DataGridManager>()
                .As<IDataGridManager>()
                .As<DataGridManager>()
                .SingleInstance();
        }

        public static void Build()
        {
            Container = s_Builder!.Build();
            s_Resolver!.SetLifetimeScope(Container);
        }
    }
}
