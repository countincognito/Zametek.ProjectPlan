using ReactiveUI;
using System.Reactive.Linq;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ActivityTrackerSetViewModel
        : ViewModelBase, IActivityTrackerSetViewModel
    {
        #region Fields

        private readonly Lock m_Lock;
        private readonly ICoreViewModel m_CoreViewModel;
        private readonly Dictionary<int, ActivityTrackerModel> m_ActivityTrackerLookup;
        private readonly List<ActivityTrackerDayViewModel> m_Days;

        private ActivityTrackerModel? m_LastTracker;

        private readonly IDisposable? m_DaysSub;

        #endregion

        #region Ctors

        public ActivityTrackerSetViewModel(
            ICoreViewModel coreViewModel,
            int activityId,
            IEnumerable<ActivityTrackerModel> trackers)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            m_Lock = new();
            m_CoreViewModel = coreViewModel;
            ActivityId = activityId;
            m_ActivityTrackerLookup = [];

            foreach (ActivityTrackerModel tracker in trackers)
            {
                if (tracker.ActivityId == ActivityId)
                {
                    m_ActivityTrackerLookup[tracker.Time] = tracker;
                }
            }

            m_Days = [.. Enumerable.Range(0, TimesheetHelper.DayCount)
                .Select(dayOffset => new ActivityTrackerDayViewModel(this, dayOffset))];

            SetLastTracker();

            SetTrackerIndexCommand = ReactiveCommand.Create<int?>(SetTrackerIndex);

            m_DaysSub = this
                .WhenAnyValue(
                    x => x.m_CoreViewModel.TrackerIndex,
                    x => x.m_CoreViewModel.IsReadyToReviseTrackers)
                // The pre-compile Yes raise happens before any tracker has
                // changed, so only the post-compile No transition (and window
                // moves, which occur while No) can alter what the day cells
                // show. Skipping Yes halves the per-edit binding fan-out.
                .Where(x => x.Item2 == ReadyToRevise.No)
                .ObserveOn(RxSchedulers.TaskpoolScheduler)
                .Subscribe(_ => RefreshDays());
        }

        #endregion

        #region Private Members

        private int TrackerIndex => m_CoreViewModel.TrackerIndex;

        internal int? GetDayPercentageCompleted(int index)
        {
            lock (m_Lock)
            {
                int indexOffset = index + TrackerIndex;
                if (m_ActivityTrackerLookup.TryGetValue(indexOffset, out ActivityTrackerModel? tracker))
                {
                    return tracker.PercentageComplete;
                }
                return null;
            }
        }

        internal void SetDayPercentageCompleted(
            int index,
            int? value)
        {
            lock (m_Lock)
            {
                int indexOffset = index + TrackerIndex;
                m_ActivityTrackerLookup.Remove(indexOffset);
                if (value is not null
                    && value > 0)
                {
                    ActivityTrackerModel tracker = new()
                    {
                        Time = indexOffset,
                        ActivityId = ActivityId,
                        PercentageComplete = value.GetValueOrDefault(),
                    };
                    m_ActivityTrackerLookup[indexOffset] = tracker;
                }
            }
        }

        private void SetLastTracker()
        {
            lock (m_Lock)
            {
                if (m_ActivityTrackerLookup.Count == 0)
                {
                    m_LastTracker = null;
                }
                else
                {
                    m_LastTracker = m_ActivityTrackerLookup.MaxBy(kvp => kvp.Key).Value;
                }
            }
        }

        private void SetTrackerIndex(int? trackerIndex)
        {
            lock (m_Lock)
            {
                if (trackerIndex is not null)
                {
                    m_CoreViewModel.TrackerIndex = trackerIndex.GetValueOrDefault();
                }
            }
        }

        private void RefreshDays()
        {
            RefreshIndex();

            foreach (ActivityTrackerDayViewModel day in m_Days)
            {
                day.RefreshValue();
            }
        }

        #endregion

        #region IActivityTrackerViewModel Members

        public List<ActivityTrackerModel> Trackers => [.. m_ActivityTrackerLookup.Values.OrderBy(x => x.Time)];

        public int ActivityId { get; }

        public int? LastTrackerIndex
        {
            get
            {
                lock (m_Lock)
                {
                    if (m_LastTracker is null)
                    {
                        return null;
                    }
                    return m_LastTracker.Time;
                }
            }
        }

        public int? LastTrackerValue
        {
            get
            {
                lock (m_Lock)
                {
                    if (m_LastTracker is null)
                    {
                        return null;
                    }
                    return m_LastTracker.PercentageComplete;
                }
            }
        }

        public ICommand SetTrackerIndexCommand { get; }

        public string SearchSymbol
        {
            get
            {
                lock (m_Lock)
                {
                    int? lastTrackerIndex = LastTrackerIndex;
                    int trackerIndex = TrackerIndex;
                    if (lastTrackerIndex is null)
                    {
                        return Resource.ProjectPlan.Symbols.Symbol_Nowhere;
                    }
                    if (lastTrackerIndex > trackerIndex)
                    {
                        return Resource.ProjectPlan.Symbols.Symbol_Forwards;
                    }
                    if (lastTrackerIndex < trackerIndex)
                    {
                        return Resource.ProjectPlan.Symbols.Symbol_Backwards;
                    }
                    return Resource.ProjectPlan.Symbols.Symbol_InPlace;
                }
            }
        }

        public void RefreshIndex()
        {
            SetLastTracker();
            this.RaisePropertyChanged(nameof(LastTrackerIndex));
            this.RaisePropertyChanged(nameof(LastTrackerValue));
            this.RaisePropertyChanged(nameof(SearchSymbol));
        }

        public List<ActivityTrackerModel> CloneTrackers()
        {
            lock (m_Lock)
            {
                return [.. m_ActivityTrackerLookup.Values
                    .OrderBy(x => x.Time)
                    .Select(selector => selector with { })];
            }
        }

        public IReadOnlyList<IActivityTrackerDayViewModel> Days => m_Days;

        #endregion

        #region IDisposable Members

        private bool m_Disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (m_Disposed)
            {
                return;
            }

            if (disposing)
            {
                m_DaysSub?.Dispose();
            }

            m_Disposed = true;
        }

        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
