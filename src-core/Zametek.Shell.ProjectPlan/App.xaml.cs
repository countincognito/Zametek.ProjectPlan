using System;
using AvalonDock;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using Prism.Unity;
using System.Windows;
using Zametek.Wpf.Core;
using Zametek.View.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;
using AutoMapper;
using System.Windows.Controls;

namespace Zametek.Shell.ProjectPlan
{
    public partial class App
        : PrismApplication
    {
        protected override Window CreateShell()
        {
            return Container.Resolve<MainView>();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<MapperProfile>();
            });
            IMapper mapper = config.CreateMapper();

            containerRegistry.RegisterInstance(mapper);

            containerRegistry.Register<IDateTimeCalculator, DateTimeCalculator>();
            containerRegistry.Register<IFileDialogService, FileDialogService>();
            containerRegistry.Register<IProjectService, ProjectService>();
            containerRegistry.Register<ISettingService, SettingService>();

            //containerRegistry.RegisterSingleton<IGraphProcessingEngine, GraphProcessingEngine>();
            //containerRegistry.RegisterSingleton<IAssessingEngine, AssessingEngine>();
            //containerRegistry.RegisterSingleton<IProjectManager, ProjectManager>();
            //containerRegistry.RegisterSingleton<ISettingResourceAccess, SettingResourceAccess>();
            //containerRegistry.RegisterSingleton<ISettingManager, SettingManager>();

            containerRegistry.RegisterSingleton<ICoreViewModel, CoreViewModel>();
            //containerRegistry.RegisterSingleton<IEarnedValueChartManagerViewModel, EarnedValueChartManagerViewModel>();
            //containerRegistry.RegisterSingleton<IResourceChartManagerViewModel, ResourceChartManagerViewModel>();
            containerRegistry.RegisterSingleton<IMetricsManagerViewModel, MetricsManagerViewModel>();
            containerRegistry.RegisterSingleton<IArrowGraphManagerViewModel, ArrowGraphManagerViewModel>();
            containerRegistry.RegisterSingleton<IGanttChartManagerViewModel, GanttChartManagerViewModel>();
            containerRegistry.RegisterSingleton<IActivitiesManagerViewModel, ActivitiesManagerViewModel>();
            containerRegistry.RegisterSingleton<IMainViewModel, MainViewModel>();



            //containerRegistry.RegisterSingleton<BottomAnchorableView>();
            //containerRegistry.RegisterForNavigation<DocumentView>(TestApp.Properties.Resources.DocumentView);
            //containerRegistry.RegisterSingleton<LeftAnchorableView>();
            //containerRegistry.RegisterSingleton<RightAnchorableView>();
            //containerRegistry.RegisterSingleton<ShellView>();


            containerRegistry.RegisterSingleton<MetricsManagerView>();
            containerRegistry.RegisterSingleton<ActivitiesManagerView>();

        }

        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            moduleCatalog.AddModule<AppModule>();
        }

        protected override void ConfigureRegionAdapterMappings(RegionAdapterMappings regionAdapterMappings)
        {
            regionAdapterMappings.RegisterMapping(typeof(ContentControl), Container.Resolve<ContentControlRegionAdapter>());
            regionAdapterMappings.RegisterMapping(typeof(DockingManager), Container.Resolve<DockingManagerRegionAdapter>());
        }
    }
}
