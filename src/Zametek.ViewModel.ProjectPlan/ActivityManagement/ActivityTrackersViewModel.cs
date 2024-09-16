using ReactiveUI;
using System.Reactive.Linq;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ActivityTrackersViewModel
        : ViewModelBase, IActivityTrackersViewModel
    {
        #region Fields

        private readonly object m_Lock;
        private readonly ICoreViewModel m_CoreViewModel;
        private readonly Dictionary<int, ActivityTrackerModel> m_ActivityTrackerLookup;

        private readonly IDisposable? m_DaysSub;

        #endregion

        #region Ctors

        public ActivityTrackersViewModel(
            ICoreViewModel coreViewModel,
            int activityId,
            IEnumerable<ActivityTrackerModel> trackers)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            m_Lock = new object();
            m_CoreViewModel = coreViewModel;
            ActivityId = activityId;
            m_ActivityTrackerLookup = [];

            foreach (ActivityTrackerModel tracker in trackers)
            {
                m_ActivityTrackerLookup.TryAdd(tracker.Time, tracker);
            }

            m_DaysSub = this
                .WhenAnyValue(x => x.m_CoreViewModel.TrackerIndex)
                .ObserveOn(RxApp.TaskpoolScheduler) // TODO check this is good
                .Subscribe(_ => RefreshDays());
        }

        #endregion

        #region Private Members

        private int TrackerIndex => m_CoreViewModel.TrackerIndex;

        private int? GetDayPercentageCompleted(int index)
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

        private void SetDayPercentageCompleted(int index, int? value)
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
                    m_ActivityTrackerLookup.TryAdd(indexOffset, tracker);
                }
            }
        }

        private void RefreshDays()
        {
            this.RaisePropertyChanged(nameof(Day00));
            this.RaisePropertyChanged(nameof(Day01));
            this.RaisePropertyChanged(nameof(Day02));
            this.RaisePropertyChanged(nameof(Day03));
            this.RaisePropertyChanged(nameof(Day04));
        }

        #endregion

        #region ITrackerViewModel Members

        public int ActivityId { get; }

        public int? Day00
        {
            get
            {
                lock (m_Lock)
                {
                    return GetDayPercentageCompleted(0);
                }
            }
            set
            {
                lock (m_Lock)
                {
                    SetDayPercentageCompleted(0, value);
                    this.RaisePropertyChanged();
                }
            }
        }

        public int? Day01
        {
            get
            {
                lock (m_Lock)
                {
                    return GetDayPercentageCompleted(1);
                }
            }
            set
            {
                lock (m_Lock)
                {
                    SetDayPercentageCompleted(1, value);
                    this.RaisePropertyChanged();
                }
            }
        }

        public int? Day02
        {
            get
            {
                lock (m_Lock)
                {
                    return GetDayPercentageCompleted(2);
                }
            }
            set
            {
                lock (m_Lock)
                {
                    SetDayPercentageCompleted(2, value);
                    this.RaisePropertyChanged();
                }
            }
        }

        public int? Day03
        {
            get
            {
                lock (m_Lock)
                {
                    return GetDayPercentageCompleted(3);
                }
            }
            set
            {
                lock (m_Lock)
                {
                    SetDayPercentageCompleted(3, value);
                    this.RaisePropertyChanged();
                }
            }
        }

        public int? Day04
        {
            get
            {
                lock (m_Lock)
                {
                    return GetDayPercentageCompleted(4);
                }
            }
            set
            {
                lock (m_Lock)
                {
                    SetDayPercentageCompleted(4, value);
                    this.RaisePropertyChanged();
                }
            }
        }

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
                // TODO: dispose managed state (managed objects).
                m_DaysSub?.Dispose();
            }

            // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
            // TODO: set large fields to null.

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
