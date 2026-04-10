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
                resourcesGrid.Columns.Add(new DataGridResourceTrackingColumn(i));
                activitiesGrid.Columns.Add(new DataGridActivityTrackingColumn(i));
            };
        }
    }
}
