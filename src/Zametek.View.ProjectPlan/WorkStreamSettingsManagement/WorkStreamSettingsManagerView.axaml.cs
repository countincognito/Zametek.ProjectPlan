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

        public WorkStreamSettingsManagerView(IDataGridManager dataGridManager)
        {
            ArgumentNullException.ThrowIfNull(dataGridManager);
            InitializeComponent();
            BehaviorCollection behaviors = Interaction.GetBehaviors(WorkStreamsGrid);
            behaviors.Add(new DataGridPersistBehavior(dataGridManager));
        }
    }
}
