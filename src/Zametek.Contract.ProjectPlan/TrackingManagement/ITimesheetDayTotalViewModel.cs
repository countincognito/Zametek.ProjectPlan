using System.ComponentModel;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    /// <summary>
    /// The total booked percentage across all of a resource's rows for one
    /// visible day, classified for display (under/full/over booking).
    /// </summary>
    public interface ITimesheetDayTotalViewModel
        : INotifyPropertyChanged
    {
        int DayOffset { get; }

        int? Total { get; }

        TimesheetDayLoad Load { get; }

        string TotalDisplay { get; }
    }
}
