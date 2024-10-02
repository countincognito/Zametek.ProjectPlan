using ReactiveUI;
using System.Globalization;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class SelectableResourceViewModel
        : ViewModelBase, ISelectableResourceViewModel
    {
        #region Ctors

        public SelectableResourceViewModel(
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
            }
        }

        public string DisplayName
        {
            get
            {
                return string.IsNullOrWhiteSpace(Name) ? Id.ToString(CultureInfo.InvariantCulture) : Name;
            }
        }

        #endregion
    }
}
