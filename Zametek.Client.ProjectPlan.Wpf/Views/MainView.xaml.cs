using System;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public partial class MainView
    {
        #region Ctors

        public MainView(IMainViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }

        #endregion

        #region Properties

        public IMainViewModel ViewModel
        {
            get
            {
                return DataContext as IMainViewModel;
            }
            set
            {
                DataContext = value;
            }
        }

        #endregion
    }
}
