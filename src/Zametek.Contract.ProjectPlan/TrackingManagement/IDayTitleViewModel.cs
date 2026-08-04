using System.ComponentModel;

namespace Zametek.Contract.ProjectPlan
{
    /// <summary>
    /// One day column title in the tracking window, addressable by position
    /// (DayTitles[n].Title). The instance is stable so bindings can index
    /// into the collection; the title text follows the tracker index and the
    /// date display settings, re-raising as the window moves.
    /// </summary>
    public interface IDayTitleViewModel
        : INotifyPropertyChanged
    {
        int DayOffset { get; }

        string Title { get; }
    }
}
