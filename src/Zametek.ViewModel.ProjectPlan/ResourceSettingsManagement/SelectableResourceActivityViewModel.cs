using ReactiveUI;
using System.Globalization;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class SelectableResourceActivityViewModel
        : ViewModelBase, ISelectableResourceActivityViewModel
    {
        #region Ctors

        public SelectableResourceActivityViewModel(
            int id,
            string name,
            int percentageWorked)
        {
            Id = id;
            m_Name = name;
            m_PercentageWorked = percentageWorked;
        }

        #endregion

        #region ISelectableResourceActivityViewModel Members

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

        private int m_PercentageWorked;
        public int PercentageWorked
        {
            get => m_PercentageWorked;
            set => this.RaiseAndSetIfChanged(ref m_PercentageWorked, value);
        }

        #endregion
    }
}
