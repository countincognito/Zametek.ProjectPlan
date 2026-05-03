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

        public GraphSettingsManagerView(IDataGridManager dataGridManager)
        {
            ArgumentNullException.ThrowIfNull(dataGridManager);
            InitializeComponent();
            BehaviorCollection behaviors = Interaction.GetBehaviors(ActivitySeveritiesGrid);
            behaviors.Add(new DataGridPersistBehavior(dataGridManager));
        }
    }
}
