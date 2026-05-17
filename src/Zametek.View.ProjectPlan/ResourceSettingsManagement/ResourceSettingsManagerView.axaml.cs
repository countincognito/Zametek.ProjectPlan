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

        public ResourceSettingsManagerView(IDataGridManager dataGridManager)
        {
            ArgumentNullException.ThrowIfNull(dataGridManager);
            InitializeComponent();
            BehaviorCollection behaviors = Interaction.GetBehaviors(ResourcesGrid);
            behaviors.Add(new DataGridPersistBehavior(dataGridManager));
        }
    }
}
