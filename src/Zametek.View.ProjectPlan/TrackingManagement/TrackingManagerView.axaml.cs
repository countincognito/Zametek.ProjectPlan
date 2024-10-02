using Avalonia.Controls;

namespace Zametek.View.ProjectPlan
{
    public partial class TrackingManagerView
        : UserControl
    {
        public TrackingManagerView()
        {
            InitializeComponent();

            for (int i = 0; i < 15; i++)
            {
                ResourcesGrid.Columns.Add(new DataGridResourceTrackingColumn(i));
                ActivitiesGrid.Columns.Add(new DataGridActivityTrackingColumn(i));
            };
        }
    }
}
