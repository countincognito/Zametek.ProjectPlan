using System;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public partial class MetricsManagerView
    {
        #region Ctors

        public MetricsManagerView(IMetricsManagerViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }

        #endregion

        #region Properties

        public IMetricsManagerViewModel ViewModel
        {
            get
            {
                return DataContext as IMetricsManagerViewModel;
            }
            set
            {
                DataContext = value;
            }
        }

        #endregion
    }
}
