using System.Windows.Input;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IResourceTrackerSetViewModel
        : IDisposable
    {
        int ResourceId { get; }

        int? LastTrackerIndex { get; }

        ICommand SetTrackerIndexCommand { get; }

        string SearchSymbol { get; }

        void RefreshIndex();

        List<ResourceTrackerModel> CloneTrackers();

        /// <summary>
        /// Returns the activity selector for the day at the given offset from
        /// the current tracker index - the same selectors the Day00-Day14
        /// properties expose, addressable by index. While the owning resource
        /// is being edited, a selector is created on demand for days that have
        /// no bookings yet; otherwise an inert empty selector is returned.
        /// </summary>
        IResourceActivitySelectorViewModel GetDay(int dayOffset);

        /// <summary>
        /// The last day (absolute tracker index) on which the given activity
        /// has a booking for this resource, or null when it has none.
        /// </summary>
        int? GetLastTrackerIndex(int activityId);

        /// <summary>
        /// The Find symbol for the given activity, relative to the current
        /// tracker index.
        /// </summary>
        string GetSearchSymbol(int activityId);
    }
}
