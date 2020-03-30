using Zametek.ViewModel.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public partial class AboutView
    {
        public AboutView()
        {
            DataContext = new AboutViewModel();
            InitializeComponent();
        }
    }
}
