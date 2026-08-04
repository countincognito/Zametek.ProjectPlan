using System.ComponentModel;

namespace Zametek.Contract.ProjectPlan
{
    /// <summary>
    /// One visible day of an activity's progress tracking, addressable by
    /// position (Days[n].PercentageCompleted). The instance is stable so
    /// bindings can index into the collection; reads and writes route through
    /// the owning tracker set, so the window follows the current tracker
    /// index.
    /// </summary>
    public interface IActivityTrackerDayViewModel
        : INotifyPropertyChanged
    {
        int DayOffset { get; }

        int? PercentageCompleted { get; set; }
    }
}
