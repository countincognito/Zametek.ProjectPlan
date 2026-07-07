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

        public TrackingManagerView(
            IDataGridLayoutManager dataGridLayoutManager,
            IDataGridScrollManager dataGridScrollManager)
        {
            ArgumentNullException.ThrowIfNull(dataGridLayoutManager);
            ArgumentNullException.ThrowIfNull(dataGridScrollManager);
            InitializeComponent();

            for (int i = 0; i < 15; i++)
            {
                TrackerResourcesGrid.Columns.Add(new DataGridResourceTrackingColumn(i));
                TrackerActivitiesGrid.Columns.Add(new DataGridActivityTrackingColumn(i));
            }
            {
                BehaviorCollection behaviors = Interaction.GetBehaviors(TrackerResourcesGrid);
                behaviors.Add(new DataGridPersistLayoutBehavior(dataGridLayoutManager));
                behaviors.Add(new DataGridPersistScrollBehavior(dataGridScrollManager));
            }
            {
                BehaviorCollection behaviors = Interaction.GetBehaviors(TrackerActivitiesGrid);
                behaviors.Add(new DataGridPersistLayoutBehavior(dataGridLayoutManager));
                behaviors.Add(new DataGridPersistScrollBehavior(dataGridScrollManager));
            }
        }
    }
}
