using Avalonia.Controls;

namespace Zametek.View.ProjectPlan
{
    public partial class TrackingManagerView
        : UserControl
    {
        public TrackingManagerView()
        {
            InitializeComponent();

            for (int i = 0; i < 20; i++)
            {
                ActivitiesGrid.Columns.Add(new DataGridPercentageTrackingColumn(i));
            };
        }
    }
}
