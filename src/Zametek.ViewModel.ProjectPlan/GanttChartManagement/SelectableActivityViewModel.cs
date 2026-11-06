using ReactiveUI;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class SelectableActivityViewModel
        : ViewModelBase, ISelectableActivityViewModel
    {
        #region Ctors

        public SelectableActivityViewModel(
            int id,
            string name)
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

        private string m_Name;
        public string Name
        {
            get => m_Name;
            set
            {
                this.RaiseAndSetIfChanged(ref m_Name, value);
                this.RaisePropertyChanged(nameof(DisplayName));
                this.RaisePropertyChanged(nameof(Label));
            }
        }

        public string DisplayName => ActivityLabelHelper.BuildDisplayName(Id, Name);

        public string Label => ActivityLabelHelper.BuildLabel(Id, Name);

        #endregion
    }
}
