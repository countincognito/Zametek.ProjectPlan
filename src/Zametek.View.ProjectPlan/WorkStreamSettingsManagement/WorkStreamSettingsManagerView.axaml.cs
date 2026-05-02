using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using System;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public partial class WorkStreamSettingsManagerView
        : UserControl
    {
        public WorkStreamSettingsManagerView()
        {
            InitializeComponent();
        }

        public WorkStreamSettingsManagerView(ISettingService settingService)
        {
            ArgumentNullException.ThrowIfNull(settingService);
            InitializeComponent();
            BehaviorCollection behaviors = Interaction.GetBehaviors(WorkStreamsGrid);
            behaviors.Add(new DataGridPersistColumnOrderBehavior(settingService));
        }
    }
}
