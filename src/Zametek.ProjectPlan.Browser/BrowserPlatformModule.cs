using Autofac;
using System;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ProjectPlan.Browser
{
    /// <summary>
    /// The three services <see cref="Core.CompositionRoot"/> cannot register for itself, in their
    /// browser forms: settings held for the lifetime of the page, dialogs shown as popups over the
    /// application root, and an MS Project importer that reports it is unavailable.
    /// </summary>
    public sealed class BrowserPlatformModule
        : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.RegisterType<BrowserSettingService>()
                .As<ISettingService>()
                .As<BrowserSettingService>()
                .SingleInstance();

            builder.RegisterType<BrowserDialogService>()
                .As<IDialogService>()
                .As<BrowserDialogService>()
                .SingleInstance();

            builder.RegisterType<UnavailableMicrosoftProjectFileImporter>()
                .As<IMicrosoftProjectFileImporter>()
                .As<UnavailableMicrosoftProjectFileImporter>()
                .SingleInstance();
        }
    }
}
