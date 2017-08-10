using Prism.Mvvm;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public class SelectableResourceViewModel
        : BindableBase
    {
        #region Fields

        private string m_Name;
        private bool m_IsSelected;

        #endregion

        #region Ctors

        public SelectableResourceViewModel(int id, string name, bool isSelected)
            : this(id, name)
        {
            m_IsSelected = isSelected;
        }

        public SelectableResourceViewModel(int id, string name)
        {
            Id = id;
            m_Name = name;
        }

        #endregion

        #region Properties

        public int Id
        {
            get;
        }

        public string Name
        {
            get
            {
                return m_Name;
            }
            set
            {
                m_Name = value;
                RaisePropertyChanged(nameof(Name));
                RaisePropertyChanged(nameof(DisplayName));
            }
        }

        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id.ToString() : Name;

        public bool IsSelected
        {
            get
            {
                return m_IsSelected;
            }
            set
            {
                m_IsSelected = value;
                RaisePropertyChanged(nameof(IsSelected));
            }
        }

        #endregion
    }
}
