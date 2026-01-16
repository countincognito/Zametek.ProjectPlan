using ReactiveUI;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class AddNodeTagViewModel
        : ViewModelBase, IAddNodeTagViewModel
    {
        #region Ctors

        public AddNodeTagViewModel()
        {
            m_Tag = string.Empty;
        }

        #endregion

        #region IAddNodeTagViewModel Members

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
