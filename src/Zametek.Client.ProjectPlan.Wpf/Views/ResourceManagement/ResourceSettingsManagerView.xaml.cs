using Prism;
using System;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public partial class ResourceSettingsManagerView
        : IActiveAware
    {
        #region Fields

        private bool m_IsActive;

        #endregion

        #region Ctors

        public ResourceSettingsManagerView()
        {
            InitializeComponent();
        }

        #endregion

        #region Properties

        public IResourceSettingsManagerViewModel ViewModel
        {
            get
            {
                return DataContext as IResourceSettingsManagerViewModel;
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
