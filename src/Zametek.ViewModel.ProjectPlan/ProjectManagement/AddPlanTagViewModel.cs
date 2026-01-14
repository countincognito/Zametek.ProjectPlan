using ReactiveUI;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class AddPlanTagViewModel
        : ViewModelBase, IAddPlanTagViewModel
    {
        #region Ctors

        public AddPlanTagViewModel()
        {
            m_Tag = string.Empty;
        }

        #endregion

        #region IAddPlanTagViewModel Members

        private string m_Tag;
        public string Tag
        {
            get => m_Tag;
            set
            {
                m_Tag = value;
                this.RaisePropertyChanged();
            }
        }

        #endregion
    }
}
