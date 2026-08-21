using Autofac;
using System;
using Zametek.Contract.ProjectPlan;
using Zametek.View.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan.Desktop
{
    /// <summary>
    /// The three services <see cref="Core.CompositionRoot"/> cannot register for itself, in their
    /// desktop forms: settings persisted as JSON files under the user's profile, dialogs and file
    /// pickers owned by a real window, and MS Project import through MPXJ.
    /// </summary>
    public sealed class DesktopPlatformModule
        : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            // Resolved eagerly rather than through the container because the settings file is read in
            // the constructor: the rest of the graph expects a service that already knows the user's
            // preferences, not one that will load them later.
            string settingsFilename = SettingFileHelper.DefaultUserSettingsFileLocation();
            string dockLayoutFilename = SettingFileHelper.DefaultDockLayoutFileLocation();
            string dataGridLayoutFilename = SettingFileHelper.DefaultDataGridLayoutFileLocation();
            var settingService = new SettingService(settingsFilename, dockLayoutFilename, dataGridLayoutFilename);

            builder.RegisterInstance(settingService)
                .As<ISettingService>()
                .As<SettingService>();

            builder.RegisterType<DialogService>()
                .As<IDialogService>()
                .As<DialogService>()
                .SingleInstance();

            builder.RegisterType<MicrosoftProjectFileImporter>()
                .As<IMicrosoftProjectFileImporter>()
                .As<MicrosoftProjectFileImporter>()
                .SingleInstance();
        }
    }
}
