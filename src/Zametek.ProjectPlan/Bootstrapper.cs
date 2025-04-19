using AutoMapper;
using Dock.Model.Core;
using Splat;
using Zametek.Contract.ProjectPlan;
using Zametek.View.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan
{
    public static class Bootstrapper
    {
        public static void RegisterSettings()
        {
            string settingsFilename = SettingFileHelper.DefaultFileLocation();
            var settingService = new SettingService(settingsFilename);
            SplatRegistrations.RegisterConstant<ISettingService>(settingService);
        }

        public static void RegisterIOC()
        {
            SplatRegistrations.SetupIOC();

            // ViewModels.
            SplatRegistrations.RegisterLazySingleton<IDateTimeCalculator, DateTimeCalculator>();
            SplatRegistrations.RegisterLazySingleton<IArrowGraphSerializer, ArrowGraphSerializer>();
            SplatRegistrations.RegisterLazySingleton<IProjectFileImport, ProjectFileImport>();
            SplatRegistrations.RegisterLazySingleton<IProjectFileExport, ProjectFileExport>();
            SplatRegistrations.RegisterLazySingleton<IProjectFileOpen, ProjectFileOpen>();
            SplatRegistrations.RegisterLazySingleton<IProjectFileSave, ProjectFileSave>();
            SplatRegistrations.RegisterLazySingleton<IDialogService, DialogService>();
            SplatRegistrations.RegisterLazySingleton<ICoreViewModel, CoreViewModel>();
            SplatRegistrations.RegisterLazySingleton<IActivitiesManagerViewModel, ActivitiesManagerViewModel>();
            SplatRegistrations.RegisterLazySingleton<ITrackingManagerViewModel, TrackingManagerViewModel>();
            SplatRegistrations.RegisterLazySingleton<IArrowGraphManagerViewModel, ArrowGraphManagerViewModel>();
            SplatRegistrations.RegisterLazySingleton<IResourceChartManagerViewModel, ResourceChartManagerViewModel>();
            SplatRegistrations.RegisterLazySingleton<IGanttChartManagerViewModel, GanttChartManagerViewModel>();
            SplatRegistrations.RegisterLazySingleton<IEarnedValueChartManagerViewModel, EarnedValueChartManagerViewModel>();
            SplatRegistrations.RegisterLazySingleton<IMetricManagerViewModel, MetricManagerViewModel>();
            SplatRegistrations.RegisterLazySingleton<IOutputManagerViewModel, OutputManagerViewModel>();
            SplatRegistrations.RegisterLazySingleton<IArrowGraphSettingsManagerViewModel, ArrowGraphSettingsManagerViewModel>();
            SplatRegistrations.RegisterLazySingleton<IResourceSettingsManagerViewModel, ResourceSettingsManagerViewModel>();
            SplatRegistrations.RegisterLazySingleton<IWorkStreamSettingsManagerViewModel, WorkStreamSettingsManagerViewModel>();
            SplatRegistrations.RegisterLazySingleton<IMainViewModel, MainViewModel>();
            SplatRegistrations.RegisterLazySingleton<IFactory, DockFactory>();

            // Views.
            SplatRegistrations.RegisterLazySingleton<ActivitiesManagerView>();
            SplatRegistrations.RegisterLazySingleton<TrackingManagerView>();
            SplatRegistrations.RegisterLazySingleton<ArrowGraphManagerView>();
            SplatRegistrations.RegisterLazySingleton<ResourceChartManagerView>();
            SplatRegistrations.RegisterLazySingleton<GanttChartManagerView>();
            SplatRegistrations.RegisterLazySingleton<EarnedValueChartManagerView>();
            SplatRegistrations.RegisterLazySingleton<MetricManagerView>();
            SplatRegistrations.RegisterLazySingleton<OutputManagerView>();
            SplatRegistrations.RegisterLazySingleton<ArrowGraphSettingsManagerView>();
            SplatRegistrations.RegisterLazySingleton<ResourceSettingsManagerView>();
            SplatRegistrations.RegisterLazySingleton<MainView>();

            var config = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<Data.ProjectPlan.MapperProfile>();
                cfg.AddProfile<ViewModel.ProjectPlan.MapperProfile>();
                cfg.AddProfile<View.ProjectPlan.MapperProfile>();
            });
            IMapper mapper = config.CreateMapper();

            SplatRegistrations.RegisterConstant(mapper);
        }
    }
}
