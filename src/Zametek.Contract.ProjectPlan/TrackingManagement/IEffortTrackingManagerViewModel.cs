namespace Zametek.Contract.ProjectPlan
{
    public interface IEffortTrackingManagerViewModel
        : ITrackingManagerViewModel
    {
        /// <summary>
        /// One collapsible timesheet section per resource, in display (drag)
        /// order.
        /// </summary>
        IReadOnlyList<IResourceTimesheetViewModel> TimesheetSections { get; }

        /// <summary>
        /// The shared width of the timesheet grids' activity-name columns, so
        /// every section resizes in step (effort tab only).
        /// </summary>
        double NameColumnWidth { get; set; }
    }
}
