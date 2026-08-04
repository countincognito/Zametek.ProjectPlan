using System.ComponentModel;

namespace Zametek.Contract.ProjectPlan
{
    /// <summary>
    /// A collapsible timesheet section for one resource: rows are the
    /// activities the resource has bookings for in the visible days (plus any
    /// rows added manually this session), columns are the visible days.
    /// </summary>
    public interface IResourceTimesheetViewModel
        : INotifyPropertyChanged
    {
        /// <summary>
        /// The live resource this section fronts. Bind display fields (name,
        /// id) through this so renames and renumbering show in real time.
        /// </summary>
        IManagedResourceViewModel Resource { get; }

        int ResourceId { get; }

        string ResourceName { get; }

        bool IsExpanded { get; set; }

        /// <summary>
        /// The shared activity-name column width, forwarded to the effort
        /// tracking manager so every section's grid resizes in step.
        /// </summary>
        double NameColumnWidth { get; set; }

        /// <summary>
        /// The shared day column titles, re-exposed from the effort tracking
        /// manager (the same instances for every section), so the grid
        /// headers can index into them (DayTitles[n].Title).
        /// </summary>
        IReadOnlyList<IDayTitleViewModel> DayTitles { get; }

        IReadOnlyList<IResourceTimesheetRowViewModel> Rows { get; }

        IReadOnlyList<ITimesheetDayTotalViewModel> DayTotals { get; }

        IReadOnlyList<ITimesheetCandidateActivityViewModel> CandidateActivities { get; }

        /// <summary>
        /// Setting a candidate adds an empty row for that activity and then
        /// resets back to null, so the picker acts as an add button.
        /// </summary>
        ITimesheetCandidateActivityViewModel? SelectedCandidateActivity { get; set; }
    }
}
