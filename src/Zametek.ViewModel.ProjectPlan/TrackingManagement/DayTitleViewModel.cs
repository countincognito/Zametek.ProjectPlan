using ReactiveUI;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// A single day column title. The cell holds no state of its own: the
    /// title text lives with the owning tracking manager, and this is the
    /// stable per-position endpoint that grid header bindings index into.
    /// </summary>
    public class DayTitleViewModel
        : ViewModelBase, IDayTitleViewModel
    {
        #region Fields

        private readonly TrackingManagerViewModel m_Manager;

        #endregion

        #region Ctors

        public DayTitleViewModel(
            TrackingManagerViewModel manager,
            int dayOffset)
        {
            ArgumentNullException.ThrowIfNull(manager);
            m_Manager = manager;
            DayOffset = dayOffset;
        }

        #endregion

        #region IDayTitleViewModel Members

        public int DayOffset { get; }

        public string Title => m_Manager.GetDayTitle(DayOffset);

        #endregion

        #region Public Members

        /// <summary>
        /// Re-raises the title after external changes (window moves, date
        /// display toggles, etc.).
        /// </summary>
        public void RefreshTitle()
        {
            this.RaisePropertyChanged(nameof(Title));
        }

        #endregion
    }
}
