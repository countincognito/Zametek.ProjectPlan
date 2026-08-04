using ReactiveUI;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// A collapsible timesheet section for one resource. Rows are the union of
    /// the activities the resource has bookings for in the visible days, plus
    /// any rows added manually this session. Each added-but-empty row is
    /// anchored to the window position where it was added (re-anchored on
    /// every edit), so it survives data refreshes and day-by-day navigation
    /// nearby; it is only cleared once the user has paged well away from its
    /// anchor and nothing is booked in the grid's leading days. The section is
    /// passive: the effort tracking manager drives every refresh on the UI
    /// thread.
    /// </summary>
    public class ResourceTimesheetViewModel
        : ViewModelBase, IResourceTimesheetViewModel
    {
        #region Fields

        // How far (in days) the window may drift from an added row's anchor
        // before the row becomes a candidate for clearing, and how many of
        // the leading (leftmost) visible days are inspected for bookings that
        // keep the section's added rows alive regardless of drift.
        // Deliberately a rough heuristic - refine later if needed.
        private const int c_LeadingDayCount = 5;

        private readonly IEffortTrackingManagerViewModel m_Manager;
        private readonly IManagedResourceViewModel m_Resource;
        private readonly int m_DayCount;
        private readonly Action<int, bool> m_OnIsExpandedChanged;

        // Session-added activity ids, each mapped to the tracker index the
        // window was at when the row was added or last edited (its anchor).
        private readonly Dictionary<int, int> m_SessionActivityIds;

        private readonly List<TimesheetDayTotalViewModel> m_DayTotals;

        private List<TimesheetRowViewModel> m_Rows;
        private IReadOnlyList<IManagedActivityViewModel> m_LastActivities;

        #endregion

        #region Ctors

        public ResourceTimesheetViewModel(
            IEffortTrackingManagerViewModel manager,
            IManagedResourceViewModel resource,
            int dayCount,
            bool isExpanded,
            Action<int, bool> onIsExpandedChanged)
        {
            ArgumentNullException.ThrowIfNull(manager);
            ArgumentNullException.ThrowIfNull(resource);
            ArgumentNullException.ThrowIfNull(onIsExpandedChanged);
            m_Manager = manager;
            m_Resource = resource;
            m_DayCount = dayCount;
            m_IsExpanded = isExpanded;
            m_OnIsExpandedChanged = onIsExpandedChanged;
            m_SessionActivityIds = [];
            m_DayTotals = [.. Enumerable.Range(0, dayCount).Select(dayOffset => new TimesheetDayTotalViewModel(dayOffset))];
            m_Rows = [];
            m_LastActivities = [];
        }

        #endregion

        #region Private Members

        private string BuildName(int activityId)
        {
            IManagedActivityViewModel? activity = m_LastActivities.FirstOrDefault(x => x.Id == activityId);
            return activity?.Name ?? string.Empty;
        }

        private string BuildLabel(int activityId)
        {
            IManagedActivityViewModel? activity = m_LastActivities.FirstOrDefault(x => x.Id == activityId);
            return TimesheetHelper.BuildActivityLabel(activityId, activity?.Name);
        }

        private bool HasBookingsInLeadingDays()
        {
            int leadingDayCount = Math.Min(c_LeadingDayCount, m_DayCount);

            for (int dayOffset = 0; dayOffset < leadingDayCount; dayOffset++)
            {
                if (m_Resource.TrackerSet.GetDay(dayOffset).SelectedResourceActivityIds.Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshDayTotals()
        {
            for (int dayOffset = 0; dayOffset < m_DayCount; dayOffset++)
            {
                bool hasData = false;
                int total = 0;

                foreach (TimesheetRowViewModel row in m_Rows)
                {
                    int? value = row.Cells[dayOffset].PercentageWorked;
                    if (value is not null)
                    {
                        hasData = true;
                        total += value.GetValueOrDefault();
                    }
                }

                m_DayTotals[dayOffset].SetTotal(hasData ? total : null);
            }
        }

        private void RefreshCandidates()
        {
            HashSet<int> rowIds = [.. m_Rows.Select(row => row.ActivityId)];

            // The activities arrive in display (drag) order - preserve it.
            m_CandidateActivities = [.. m_LastActivities
                .Where(activity => !rowIds.Contains(activity.Id))
                .Select(activity => new TimesheetCandidateActivityViewModel(
                    activity.Id,
                    TimesheetHelper.BuildActivityLabel(activity.Id, activity.Name)))];

            this.RaisePropertyChanged(nameof(CandidateActivities));
        }

        // Invoked by a cell after it writes a value. Keeps rows that were just
        // emptied alive for the rest of the session (re-anchored to the
        // current window, until the clearing rule catches up with them) and
        // brings the day totals back in line.
        private void OnCellChanged()
        {
            foreach (TimesheetRowViewModel row in m_Rows)
            {
                if (row.HasData)
                {
                    m_SessionActivityIds.Remove(row.ActivityId);
                }
                else
                {
                    m_SessionActivityIds[row.ActivityId] = m_Manager.TrackerIndex;
                }
                row.RefreshSearch();
            }

            RefreshDayTotals();
        }

        #endregion

        #region IResourceTimesheetViewModel Members

        public IManagedResourceViewModel Resource => m_Resource;

        public int ResourceId => m_Resource.Id;

        public string ResourceName => m_Resource.Name;

        private bool m_IsExpanded;
        public bool IsExpanded
        {
            get => m_IsExpanded;
            set
            {
                this.RaiseAndSetIfChanged(ref m_IsExpanded, value);
                m_OnIsExpandedChanged(ResourceId, value);
            }
        }

        public double NameColumnWidth
        {
            get => m_Manager.NameColumnWidth;
            set => m_Manager.NameColumnWidth = value;
        }

        public IReadOnlyList<IDayTitleViewModel> DayTitles => m_Manager.DayTitles;

        public IReadOnlyList<IResourceTimesheetRowViewModel> Rows => m_Rows;

        public IReadOnlyList<ITimesheetDayTotalViewModel> DayTotals => m_DayTotals;

        private IReadOnlyList<ITimesheetCandidateActivityViewModel> m_CandidateActivities = [];
        public IReadOnlyList<ITimesheetCandidateActivityViewModel> CandidateActivities => m_CandidateActivities;

        private ITimesheetCandidateActivityViewModel? m_SelectedCandidateActivity;
        public ITimesheetCandidateActivityViewModel? SelectedCandidateActivity
        {
            get => m_SelectedCandidateActivity;
            set
            {
                m_SelectedCandidateActivity = null;

                if (value is not null)
                {
                    // Picking a candidate adds an empty session row for it,
                    // anchored to the current window, and resets the picker so
                    // it acts as an add button.
                    m_SessionActivityIds[value.Id] = m_Manager.TrackerIndex;
                    Refresh(m_LastActivities, windowMoved: false);
                }

                this.RaisePropertyChanged();
            }
        }

        #endregion

        #region Public Members

        /// <summary>
        /// Rebuilds the section from live tracker state. Rows are only
        /// replaced when the set of activities actually changes, so routine
        /// data refreshes do not disturb cell focus while the user is typing.
        /// </summary>
        public void Refresh(
            IReadOnlyList<IManagedActivityViewModel> activities,
            bool windowMoved)
        {
            ArgumentNullException.ThrowIfNull(activities);
            m_LastActivities = activities;

            Dictionary<int, int> displayIndexById = [];

            for (int i = 0; i < activities.Count; i++)
            {
                displayIndexById[activities[i].Id] = i;
            }

            HashSet<int> bookedActivityIds = [];

            for (int dayOffset = 0; dayOffset < m_DayCount; dayOffset++)
            {
                bookedActivityIds.UnionWith(m_Resource.TrackerSet.GetDay(dayOffset).SelectedResourceActivityIds);
            }

            if (windowMoved)
            {
                // A row whose bookings have just scrolled out of the window
                // becomes an anchored session row (provided the activity still
                // exists in the plan), so a line the user is actively extending
                // follows them past the window edge instead of vanishing.
                foreach (TimesheetRowViewModel row in m_Rows)
                {
                    if (!bookedActivityIds.Contains(row.ActivityId)
                        && !m_SessionActivityIds.ContainsKey(row.ActivityId)
                        && displayIndexById.ContainsKey(row.ActivityId))
                    {
                        m_SessionActivityIds[row.ActivityId] = m_Manager.TrackerIndex;
                    }
                }

                // Session rows follow the user while they navigate day by day.
                // A row is only cleared once the window has drifted well away
                // from the row's anchor AND this grid shows no bookings in its
                // leading days - i.e. the user has left the region they were
                // actively filling in.
                if (!HasBookingsInLeadingDays())
                {
                    int trackerIndex = m_Manager.TrackerIndex;

                    List<int> staleActivityIds = [.. m_SessionActivityIds
                        .Where(kvp => Math.Abs(trackerIndex - kvp.Value) > c_LeadingDayCount)
                        .Select(kvp => kvp.Key)];

                    foreach (int staleActivityId in staleActivityIds)
                    {
                        m_SessionActivityIds.Remove(staleActivityId);
                    }
                }
            }

            HashSet<int> rowIds = [.. bookedActivityIds];
            rowIds.UnionWith(m_SessionActivityIds.Keys);

            // Rows follow the activities' display (drag) order; bookings for
            // activities no longer in the plan sort last, by id.

            List<int> orderedIds = [.. rowIds
                .OrderBy(id => displayIndexById.TryGetValue(id, out int index) ? index : int.MaxValue)
                .ThenBy(id => id)];

            if (!orderedIds.SequenceEqual(m_Rows.Select(row => row.ActivityId)))
            {
                m_Rows = [.. orderedIds.Select(activityId => new TimesheetRowViewModel(
                    m_Resource,
                    activityId,
                    BuildName(activityId),
                    BuildLabel(activityId),
                    m_DayCount,
                    OnCellChanged))];
                this.RaisePropertyChanged(nameof(Rows));
            }
            else
            {
                foreach (TimesheetRowViewModel row in m_Rows)
                {
                    row.ActivityName = BuildName(row.ActivityId);
                    row.ActivityLabel = BuildLabel(row.ActivityId);
                }
            }

            foreach (TimesheetRowViewModel row in m_Rows)
            {
                row.RefreshCells();
            }

            RefreshDayTotals();
            RefreshCandidates();

            // Renumbering mutates the resource's id in place (the section is
            // not rebuilt), so both header fields must be re-raised.
            this.RaisePropertyChanged(nameof(ResourceId));
            this.RaisePropertyChanged(nameof(ResourceName));
        }

        /// <summary>
        /// Invoked by the effort tracking manager when the shared name-column
        /// width changes, so every section's grid follows the resize.
        /// </summary>
        public void RaiseNameColumnWidthChanged()
        {
            this.RaisePropertyChanged(nameof(NameColumnWidth));
        }

        #endregion
    }
}
