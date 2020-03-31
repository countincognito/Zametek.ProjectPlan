using Prism.Mvvm;
using System.Globalization;

namespace Zametek.ViewModel.ProjectPlan
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
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(DisplayName));
            }
        }

        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id.ToString(CultureInfo.InvariantCulture) : Name;

        public bool IsSelected
        {
            get
            {
                return m_IsSelected;
            }
            set
            {
                m_IsSelected = value;
                RaisePropertyChanged();
            }
        }

        #endregion
    }
}
