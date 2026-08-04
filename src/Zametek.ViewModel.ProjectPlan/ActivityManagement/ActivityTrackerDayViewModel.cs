using ReactiveUI;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// A single editable day cell of an activity's progress tracking. The
    /// cell holds no state of its own: the percentage values live in the
    /// owning tracker set's lookup, and this is the stable per-position
    /// endpoint that grid cell bindings index into.
    /// </summary>
    public class ActivityTrackerDayViewModel
        : ViewModelBase, IActivityTrackerDayViewModel
    {
        #region Fields

        private readonly ActivityTrackerSetViewModel m_TrackerSet;

        #endregion

        #region Ctors

        public ActivityTrackerDayViewModel(
            ActivityTrackerSetViewModel trackerSet,
            int dayOffset)
        {
            ArgumentNullException.ThrowIfNull(trackerSet);
            m_TrackerSet = trackerSet;
            DayOffset = dayOffset;
        }

        #endregion

        #region IActivityTrackerDayViewModel Members

        public int DayOffset { get; }

        public int? PercentageCompleted
        {
            get => m_TrackerSet.GetDayPercentageCompleted(DayOffset);
            set
            {
                m_TrackerSet.SetDayPercentageCompleted(DayOffset, value);
                this.RaisePropertyChanged();
                m_TrackerSet.RefreshIndex();
            }
        }

        #endregion

        #region Public Members

        /// <summary>
        /// Re-raises the cell value after external changes (window moves,
        /// tracker revisions) without going through the write path.
        /// </summary>
        public void RefreshValue()
        {
            this.RaisePropertyChanged(nameof(PercentageCompleted));
        }

        #endregion
    }
}
