using System.Windows.Input;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IActivityTrackerSetViewModel
        : IDisposable
    {
        List<ActivityTrackerModel> Trackers { get; }

        int ActivityId { get; }

        int? LastTrackerIndex { get; }

        int? LastTrackerValue { get; }

        ICommand SetTrackerIndexCommand { get; }

        string SearchSymbol { get; }

        void RefreshIndex();

        List<ActivityTrackerModel> CloneTrackers();

        /// <summary>
        /// One stable day view model per visible day column (sized by
        /// TimesheetHelper.DayCount); grid cell bindings index into it
        /// (Days[n].PercentageCompleted) and reads/writes route through this
        /// tracker set, so the window follows the current tracker index.
        /// </summary>
        IReadOnlyList<IActivityTrackerDayViewModel> Days { get; }
    }
}
