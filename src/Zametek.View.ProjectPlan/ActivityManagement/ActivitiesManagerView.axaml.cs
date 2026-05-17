using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using System;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public partial class ActivitiesManagerView
        : UserControl
    {
        public ActivitiesManagerView()
        {
            InitializeComponent();
        }

        public ActivitiesManagerView(IDataGridManager dataGridManager)
        {
            ArgumentNullException.ThrowIfNull(dataGridManager);
            InitializeComponent();
            BehaviorCollection behaviors = Interaction.GetBehaviors(ActivitiesGrid);
            behaviors.Add(new DataGridPersistBehavior(dataGridManager));
        }
    }
}
