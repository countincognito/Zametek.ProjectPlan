using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using System;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public partial class TrackingManagerView
        : UserControl
    {
        public TrackingManagerView()
        {
            InitializeComponent();
        }

        public TrackingManagerView(IDataGridManager dataGridManager)
        {
            ArgumentNullException.ThrowIfNull(dataGridManager);
            InitializeComponent();

            for (int i = 0; i < 15; i++)
            {
                TrackerResourcesGrid.Columns.Add(new DataGridResourceTrackingColumn(i));
                TrackerActivitiesGrid.Columns.Add(new DataGridActivityTrackingColumn(i));
            }
            {
                BehaviorCollection behaviors = Interaction.GetBehaviors(TrackerResourcesGrid);
                behaviors.Add(new DataGridPersistBehavior(dataGridManager));
            }
            {
                BehaviorCollection behaviors = Interaction.GetBehaviors(TrackerActivitiesGrid);
                behaviors.Add(new DataGridPersistBehavior(dataGridManager));
            }
        }
    }
}
