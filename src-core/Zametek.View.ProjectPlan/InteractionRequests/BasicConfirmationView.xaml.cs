using Zametek.ViewModel.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public partial class BasicConfirmationView
    {
        public BasicConfirmationView()
        {
            DataContext = new BasicConfirmationViewModel();
            InitializeComponent();
        }
    }
}
