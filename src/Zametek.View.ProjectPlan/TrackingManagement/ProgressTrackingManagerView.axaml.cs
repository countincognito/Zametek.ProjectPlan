using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using System;
using Zametek.Contract.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public partial class ProgressTrackingManagerView
        : UserControl
    {
        public ProgressTrackingManagerView()
        {
            InitializeComponent();
        }

        public ProgressTrackingManagerView(
            IDataGridLayoutManager dataGridLayoutManager,
            IDataGridScrollManager dataGridScrollManager,
            ICommitEditHandler commitEditHandler)
        {
            ArgumentNullException.ThrowIfNull(dataGridLayoutManager);
            ArgumentNullException.ThrowIfNull(dataGridScrollManager);
            ArgumentNullException.ThrowIfNull(commitEditHandler);
            InitializeComponent();

            for (int i = 0; i < TrackingHelper.DayCount; i++)
            {
                TrackerActivitiesGrid.Columns.Add(new DataGridProgressTrackingColumn(i));
            }
            {
                BehaviorCollection behaviors = Interaction.GetBehaviors(TrackerActivitiesGrid);
                behaviors.Add(new DataGridPersistLayoutBehavior(dataGridLayoutManager));
                behaviors.Add(new DataGridPersistScrollBehavior(dataGridScrollManager));
                behaviors.Add(new DataGridCommitEditBehavior(commitEditHandler));
                behaviors.Add(new FadeInBehavior());
            }
        }
    }
}
