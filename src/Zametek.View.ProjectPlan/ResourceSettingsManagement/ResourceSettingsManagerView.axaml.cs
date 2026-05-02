using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using System;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public partial class ResourceSettingsManagerView
        : UserControl
    {
        public ResourceSettingsManagerView()
        {
            InitializeComponent();
        }

        public ResourceSettingsManagerView(ISettingService settingService)
        {
            ArgumentNullException.ThrowIfNull(settingService);
            InitializeComponent();
            BehaviorCollection behaviors = Interaction.GetBehaviors(ResourcesGrid);
            behaviors.Add(new DataGridPersistColumnOrderBehavior(settingService));
        }
    }
}
