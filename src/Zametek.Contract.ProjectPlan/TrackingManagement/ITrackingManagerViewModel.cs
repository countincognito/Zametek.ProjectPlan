using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Zametek.Contract.ProjectPlan
{
    public interface ITrackingManagerViewModel
        : IKillSubscriptions, IDisposable
    {
        bool IsBusy { get; }

        bool HasActivities { get; }

        bool HasResources { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        DateTimeOffset ProjectStart { get; }

        bool ShowDates { get; }

        IReadOnlyList<IManagedActivityViewModel> RawActivities { get; }

        ReadOnlyObservableCollection<IManagedActivityViewModel> Activities { get; }

        ObservableCollection<IManagedActivityViewModel> OrderableActivities { get; }

        IReadOnlyList<IManagedResourceViewModel> RawResources { get; }

        ReadOnlyObservableCollection<IManagedResourceViewModel> Resources { get; }

        ObservableCollection<IManagedResourceViewModel> OrderableResources { get; }

        IDateTimeCalculator DateTimeCalculator { get; }

        int TrackerIndex { get; set; }

        int? PageIndex { get; set; }

        /// <summary>
        /// One stable title view model per visible day column (sized by
        /// TrackingHelper.DayCount); grid header bindings index into it
        /// (DayTitles[n].Title) and the elements re-raise as the window
        /// moves.
        /// </summary>
        IReadOnlyList<IDayTitleViewModel> DayTitles { get; }

        ICommand SyncTodayCommand { get; }
    }
}
