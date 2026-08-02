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
            IDataGridScrollManager dataGridScrollManager)
        {
            ArgumentNullException.ThrowIfNull(dataGridLayoutManager);
            ArgumentNullException.ThrowIfNull(dataGridScrollManager);
            InitializeComponent();

            for (int i = 0; i < TimesheetHelper.DayCount; i++)
            {
                TrackerActivitiesGrid.Columns.Add(new DataGridProgressTrackingColumn(i));
            }
            {
                BehaviorCollection behaviors = Interaction.GetBehaviors(TrackerActivitiesGrid);
                behaviors.Add(new DataGridPersistLayoutBehavior(dataGridLayoutManager));
                behaviors.Add(new DataGridPersistScrollBehavior(dataGridScrollManager));
                behaviors.Add(new FadeInBehavior());
            }
        }
    }
}
