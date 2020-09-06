using AutoMapper;
using AvalonDock;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using Prism.Unity;
using System;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using Zametek.Contract.ProjectPlan;
using Zametek.View.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;
using Zametek.Wpf.Core;

namespace Zametek.Shell.ProjectPlan
{
    public partial class App
        : PrismApplication
    {
        protected override Window CreateShell()
        {
            CultureInfo currentCulture = CultureInfo.CurrentCulture;

            Thread.CurrentThread.CurrentCulture = currentCulture;
            Thread.CurrentThread.CurrentUICulture = currentCulture;

            // https://stackoverflow.com/questions/520115/stringformat-localization-issues-in-wpf/520334
            // Ensure the current culture passed into bindings is the OS culture.
            // By default, WPF uses en-US as the culture, regardless of the system settings.
            FrameworkElement.LanguageProperty.OverrideMetadata(
                  typeof(FrameworkElement),
                  new FrameworkPropertyMetadata(
                      XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.Name)));

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

            containerRegistry.RegisterSingleton<ICoreViewModel, CoreViewModel>();
            containerRegistry.RegisterSingleton<IEarnedValueChartManagerViewModel, EarnedValueChartManagerViewModel>();
            containerRegistry.RegisterSingleton<IResourceChartManagerViewModel, ResourceChartManagerViewModel>();
            containerRegistry.RegisterSingleton<IMetricsManagerViewModel, MetricsManagerViewModel>();
            containerRegistry.RegisterSingleton<IArrowGraphManagerViewModel, ArrowGraphManagerViewModel>();
            containerRegistry.RegisterSingleton<IGanttChartManagerViewModel, GanttChartManagerViewModel>();
            containerRegistry.RegisterSingleton<IActivitiesManagerViewModel, ActivitiesManagerViewModel>();
            containerRegistry.RegisterSingleton<IMainViewModel, MainViewModel>();

            containerRegistry.RegisterSingleton<IApplicationCommands, ApplicationCommands>();
        }

        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            moduleCatalog.AddModule<AppModule>();
        }

        protected override void ConfigureRegionAdapterMappings(RegionAdapterMappings regionAdapterMappings)
        {
            if (regionAdapterMappings is null)
            {
                throw new ArgumentNullException(nameof(regionAdapterMappings));
            }

            base.ConfigureRegionAdapterMappings(regionAdapterMappings);
            //regionAdapterMappings.RegisterMapping(typeof(ContentControl), Container.Resolve<ContentControlRegionAdapter>());
            regionAdapterMappings.RegisterMapping(typeof(DockingManager), Container.Resolve<DockingManagerRegionAdapter>());
        }
    }
}
