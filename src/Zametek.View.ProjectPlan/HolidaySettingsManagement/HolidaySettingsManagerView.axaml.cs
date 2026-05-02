using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using System;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public partial class HolidaySettingsManagerView
        : UserControl
    {
        public HolidaySettingsManagerView()
        {
            InitializeComponent();
        }

        public HolidaySettingsManagerView(ISettingService settingService)
        {
            ArgumentNullException.ThrowIfNull(settingService);
            InitializeComponent();
            BehaviorCollection behaviors = Interaction.GetBehaviors(HolidaysGrid);
            behaviors.Add(new DataGridPersistColumnOrderBehavior(settingService));
        }
    }
}
