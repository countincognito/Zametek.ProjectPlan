using Zametek.ViewModel.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public partial class BasicNotificationView
    {
        public BasicNotificationView()
        {
            DataContext = new BasicNotificationViewModel();
            InitializeComponent();
        }
    }
}
