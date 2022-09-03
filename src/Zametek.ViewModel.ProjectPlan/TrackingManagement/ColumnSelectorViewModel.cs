using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ColumnSelectorViewModel
        : ViewModelBase, IColumnSelectorViewModel
    {
        public ColumnSelectorViewModel(string displayName, int columnIndex)
        {
            DisplayName = displayName;
            ColumnIndex = columnIndex;
        }

        public string DisplayName { get; }

        public int ColumnIndex { get; }
    }
}
