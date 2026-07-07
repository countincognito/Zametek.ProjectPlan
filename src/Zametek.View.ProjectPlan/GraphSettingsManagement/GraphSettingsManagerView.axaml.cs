using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using System;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public partial class GraphSettingsManagerView
        : UserControl
    {
        public GraphSettingsManagerView()
        {
            InitializeComponent();
        }

        public GraphSettingsManagerView(
            IDataGridLayoutManager dataGridLayoutManager,
            IDataGridScrollManager dataGridScrollManager)
        {
            ArgumentNullException.ThrowIfNull(dataGridLayoutManager);
            ArgumentNullException.ThrowIfNull(dataGridScrollManager);
            InitializeComponent();
            BehaviorCollection behaviors = Interaction.GetBehaviors(ActivitySeveritiesGrid);
            behaviors.Add(new DataGridPersistLayoutBehavior(dataGridLayoutManager));
            behaviors.Add(new DataGridPersistScrollBehavior(dataGridScrollManager));
        }
    }
}
