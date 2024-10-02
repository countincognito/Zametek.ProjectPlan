using ReactiveUI;
using System.Globalization;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class SelectableWorkStreamViewModel
        : ViewModelBase, ISelectableWorkStreamViewModel
    {
        #region Ctors

        public SelectableWorkStreamViewModel(
            int id,
            string name,
            bool isPhase)
        {
            Id = id;
            m_Name = name;
            m_IsPhase = isPhase;
        }

        #endregion

        #region Properties

        public int Id
        {
            get;
        }

        private string m_Name;
        public string Name
        {
            get => m_Name;
            set
            {
                this.RaiseAndSetIfChanged(ref m_Name, value);
                this.RaisePropertyChanged(nameof(DisplayName));
            }
        }

        public string DisplayName
        {
            get
            {
                return string.IsNullOrWhiteSpace(Name) ? Id.ToString(CultureInfo.InvariantCulture) : Name;
            }
        }

        private bool m_IsPhase;
        public bool IsPhase
        {
            get => m_IsPhase;
            set => this.RaiseAndSetIfChanged(ref m_IsPhase, value);
        }

        #endregion
    }
}
