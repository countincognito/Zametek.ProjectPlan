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

        public HolidaySettingsManagerView(IDataGridManager dataGridManager)
        {
            ArgumentNullException.ThrowIfNull(dataGridManager);
            InitializeComponent();
            BehaviorCollection behaviors = Interaction.GetBehaviors(HolidaysGrid);
            behaviors.Add(new DataGridPersistBehavior(dataGridManager));
        }
    }
}
