using System.ComponentModel;
using System.Windows.Input;

namespace Zametek.Contract.ProjectPlan
{
    /// <summary>
    /// One row in a resource's timesheet section: the bookings for a single
    /// activity across the visible days.
    /// </summary>
    public interface IResourceTimesheetRowViewModel
        : INotifyPropertyChanged
    {
        int ActivityId { get; }

        string ActivityName { get; }

        string ActivityLabel { get; }

        IReadOnlyList<ITimesheetCellViewModel> Cells { get; }

        /// <summary>
        /// The last day (absolute tracker index) on which this activity has a
        /// booking for the owning resource, or null when it has none.
        /// </summary>
        int? LastTrackerIndex { get; }

        string SearchSymbol { get; }

        ICommand SetTrackerIndexCommand { get; }
    }
}
