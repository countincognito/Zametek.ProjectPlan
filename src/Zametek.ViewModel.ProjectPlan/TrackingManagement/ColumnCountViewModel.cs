using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ColumnCountViewModel
        : ViewModelBase, IColumnCountViewModel
    {
        public ColumnCountViewModel(string displayName, int columnCount)
        {
            DisplayName = displayName;
            ColumnCount = columnCount;
        }

        public string DisplayName { get; }

        public int ColumnCount { get; }
    }
}
