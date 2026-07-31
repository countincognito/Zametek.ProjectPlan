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

        string Day00Title { get; }
        string Day01Title { get; }
        string Day02Title { get; }
        string Day03Title { get; }
        string Day04Title { get; }
        string Day05Title { get; }
        string Day06Title { get; }
        string Day07Title { get; }
        string Day08Title { get; }
        string Day09Title { get; }
        string Day10Title { get; }
        string Day11Title { get; }
        string Day12Title { get; }
        string Day13Title { get; }
        string Day14Title { get; }

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
