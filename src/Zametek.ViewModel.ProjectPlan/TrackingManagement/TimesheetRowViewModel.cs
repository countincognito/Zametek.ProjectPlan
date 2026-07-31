using ReactiveUI;
using System.Windows.Input;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class TimesheetRowViewModel
        : ViewModelBase, IResourceTimesheetRowViewModel
    {
        #region Fields

        private readonly IManagedResourceViewModel m_Resource;
        private readonly List<TimesheetCellViewModel> m_Cells;

        #endregion

        #region Ctors

        public TimesheetRowViewModel(
            IManagedResourceViewModel resource,
            int activityId,
            string activityName,
            string activityLabel,
            int dayCount,
            Action onCellChanged)
        {
            ArgumentNullException.ThrowIfNull(resource);
            ArgumentNullException.ThrowIfNull(activityName);
            ArgumentNullException.ThrowIfNull(activityLabel);
            ArgumentNullException.ThrowIfNull(onCellChanged);
            m_Resource = resource;
            ActivityId = activityId;
            m_ActivityName = activityName;
            m_ActivityLabel = activityLabel;
            m_Cells = [.. Enumerable.Range(0, dayCount)
                .Select(dayOffset => new TimesheetCellViewModel(resource, activityId, dayOffset, onCellChanged))];
        }

        #endregion

        #region IResourceTimesheetRowViewModel Members

        public int ActivityId { get; }

        private string m_ActivityName;
        public string ActivityName
        {
            get => m_ActivityName;
            set => this.RaiseAndSetIfChanged(ref m_ActivityName, value);
        }

        private string m_ActivityLabel;
        public string ActivityLabel
        {
            get => m_ActivityLabel;
            set => this.RaiseAndSetIfChanged(ref m_ActivityLabel, value);
        }

        public IReadOnlyList<ITimesheetCellViewModel> Cells => m_Cells;

        public int? LastTrackerIndex => m_Resource.TrackerSet.GetLastTrackerIndex(ActivityId);

        public string SearchSymbol => m_Resource.TrackerSet.GetSearchSymbol(ActivityId);

        public ICommand SetTrackerIndexCommand => m_Resource.TrackerSet.SetTrackerIndexCommand;

        #endregion

        #region Public Members

        public bool HasData => m_Cells.Any(cell => cell.PercentageWorked is not null);

        public void RefreshSearch()
        {
            this.RaisePropertyChanged(nameof(LastTrackerIndex));
            this.RaisePropertyChanged(nameof(SearchSymbol));
        }

        public void RefreshCells()
        {
            foreach (TimesheetCellViewModel cell in m_Cells)
            {
                cell.RefreshValue();
            }
            RefreshSearch();
        }

        #endregion
    }
}
