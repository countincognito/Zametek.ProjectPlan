using Prism;
using System;
using Zametek.Contract.ProjectPlan;
using Zametek.Wpf.Core;

namespace Zametek.View.ProjectPlan
{
    [AvalonDockAnchorable(Strategy = AnchorableStrategies.Top, IsHidden = false)]
    public partial class ResourceChartManagerView
        : IActiveAware
    {
        #region Fields

        private bool m_IsActive;

        #endregion

        #region Ctors

        public ResourceChartManagerView(IResourceChartManagerViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }

        #endregion

        #region Properties

        public IResourceChartManagerViewModel ViewModel
        {
            get
            {
                return DataContext as IResourceChartManagerViewModel;
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
