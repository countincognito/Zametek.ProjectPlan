using System;
using Prism;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public partial class ResourceChartsManagerView
        : IActiveAware
    {
        #region Fields

        private bool m_IsActive;

        #endregion

        #region Ctors

        public ResourceChartsManagerView(IResourceChartsManagerViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }

        #endregion

        #region Properties

        public IResourceChartsManagerViewModel ViewModel
        {
            get
            {
                return DataContext as IResourceChartsManagerViewModel;
            }
            set
            {
                DataContext = value;
            }
        }

        #endregion

        #region IActiveAware Members

        public event EventHandler IsActiveChanged;

        public bool IsActive
        {
            get
            {
                return m_IsActive;
            }
            set
            {
                if (m_IsActive != value)
                {
                    m_IsActive = value;
                    IsActiveChanged?.Invoke(this, new EventArgs());
                }
            }
        }

        #endregion
    }
}
