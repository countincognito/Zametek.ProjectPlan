using ReactiveUI;
using System.ComponentModel;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// A single editable day cell in a resource's timesheet row. The cell
    /// holds no state of its own: it reads and writes the resource's live day
    /// selectors by activity id, driving the exact IEditableObject lifecycle
    /// the resource tracking grid uses (BeginEdit enables on-demand selector
    /// creation; EndEdit prunes empty selectors and marks the resource
    /// settings updated, which triggers the revise/compile cascade).
    /// </summary>
    public class TimesheetCellViewModel
        : ViewModelBase, ITimesheetCellViewModel
    {
        #region Fields

        private readonly IManagedResourceViewModel m_Resource;
        private readonly int m_ActivityId;
        private readonly Action m_OnCellChanged;

        #endregion

        #region Ctors

        public TimesheetCellViewModel(
            IManagedResourceViewModel resource,
            int activityId,
            int dayOffset,
            Action onCellChanged)
        {
            ArgumentNullException.ThrowIfNull(resource);
            ArgumentNullException.ThrowIfNull(onCellChanged);
            m_Resource = resource;
            m_ActivityId = activityId;
            DayOffset = dayOffset;
            m_OnCellChanged = onCellChanged;
        }

        #endregion

        #region Private Members

        private int? ReadValue()
        {
            return m_Resource.TrackerSet
                .GetDay(DayOffset)
                .SelectedTargetResourceActivities
                .FirstOrDefault(x => x.Id == m_ActivityId)?
                .PercentageWorked;
        }

        #endregion

        #region ITimesheetCellViewModel Members

        public int DayOffset { get; }

        public int? PercentageWorked
        {
            get => ReadValue();
            set
            {
                if (ReadValue() == value)
                {
                    return;
                }
                if (m_Resource is not IEditableObject editable)
                {
                    return;
                }

                // BeginEdit marks the resource as editing, which allows the
                // tracker set to create a selector for a day that has no
                // bookings yet.
                editable.BeginEdit();

                IResourceActivitySelectorViewModel selector = m_Resource.TrackerSet.GetDay(DayOffset);
                ISelectableResourceActivityViewModel? target = selector.TargetResourceActivities
                    .FirstOrDefault(x => x.Id == m_ActivityId);

                if (target is null)
                {
                    // The activity is not selectable (e.g. it no longer exists
                    // in the plan) - abandon the edit.
                    editable.CancelEdit();
                    return;
                }

                if (value is null)
                {
                    // Clearing the cell removes the booking entirely.
                    ISelectableResourceActivityViewModel? selected = selector.SelectedTargetResourceActivities
                        .FirstOrDefault(x => x.Id == m_ActivityId);

                    if (selected is not null)
                    {
                        selector.SelectedTargetResourceActivities.Remove(selected);
                    }
                }
                else
                {
                    if (!selector.SelectedResourceActivityIds.Contains(m_ActivityId))
                    {
                        selector.SelectedTargetResourceActivities.Add(target);
                    }
                    target.PercentageWorked = value.GetValueOrDefault();
                }

                selector.RaiseTargetResourceActivitiesPropertiesChanged();

                // EndEdit prunes selectors left with no selections and marks
                // the resource settings updated, triggering the revise cycle.
                editable.EndEdit();

                this.RaisePropertyChanged();
                m_OnCellChanged();
            }
        }

        #endregion

        #region Public Members

        /// <summary>
        /// Re-raises the cell value after external changes (revise cycles,
        /// window moves) without touching the edit lifecycle.
        /// </summary>
        public void RefreshValue()
        {
            this.RaisePropertyChanged(nameof(PercentageWorked));
        }

        #endregion
    }
}
