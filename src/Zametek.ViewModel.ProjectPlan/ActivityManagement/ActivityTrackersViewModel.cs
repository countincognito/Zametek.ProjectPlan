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
                if (tracker.ActivityId == ActivityId)
                {
                    m_ActivityTrackerLookup.TryAdd(tracker.Time, tracker);
                }
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

        private void SetDayPercentageCompleted(
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
            this.RaisePropertyChanged(nameof(Day05));
            this.RaisePropertyChanged(nameof(Day06));
            this.RaisePropertyChanged(nameof(Day07));
            this.RaisePropertyChanged(nameof(Day08));
            this.RaisePropertyChanged(nameof(Day09));
            this.RaisePropertyChanged(nameof(Day10));
            this.RaisePropertyChanged(nameof(Day11));
            this.RaisePropertyChanged(nameof(Day12));
            this.RaisePropertyChanged(nameof(Day13));
            this.RaisePropertyChanged(nameof(Day14));
            this.RaisePropertyChanged(nameof(Day15));
            this.RaisePropertyChanged(nameof(Day16));
            this.RaisePropertyChanged(nameof(Day17));
            this.RaisePropertyChanged(nameof(Day18));
            this.RaisePropertyChanged(nameof(Day19));
        }

        #endregion

        #region ITrackerViewModel Members

        public List<ActivityTrackerModel> Trackers => [.. m_ActivityTrackerLookup.Values.OrderBy(x => x.Time)];

        public int ActivityId { get; }

        public int? Day00
        {
            get => GetDayPercentageCompleted(0);
            set
            {
                SetDayPercentageCompleted(0, value);
                this.RaisePropertyChanged();
            }
        }

        public int? Day01
        {
            get => GetDayPercentageCompleted(1);
            set
            {
                SetDayPercentageCompleted(1, value);
                this.RaisePropertyChanged();
            }
        }

        public int? Day02
        {
            get => GetDayPercentageCompleted(2);
            set
            {
                SetDayPercentageCompleted(2, value);
                this.RaisePropertyChanged();
            }
        }

        public int? Day03
        {
            get => GetDayPercentageCompleted(3);
            set
            {
                SetDayPercentageCompleted(3, value);
                this.RaisePropertyChanged();
            }
        }

        public int? Day04
        {
            get => GetDayPercentageCompleted(4);
            set
            {
                SetDayPercentageCompleted(4, value);
                this.RaisePropertyChanged();
            }
        }

        public int? Day05
        {
            get => GetDayPercentageCompleted(5);
            set
            {
                SetDayPercentageCompleted(5, value);
                this.RaisePropertyChanged();
            }
        }

        public int? Day06
        {
            get => GetDayPercentageCompleted(6);
            set
            {
                SetDayPercentageCompleted(6, value);
                this.RaisePropertyChanged();
            }
        }

        public int? Day07
        {
            get => GetDayPercentageCompleted(7);
            set
            {
                SetDayPercentageCompleted(7, value);
                this.RaisePropertyChanged();
            }
        }

        public int? Day08
        {
            get => GetDayPercentageCompleted(8);
            set
            {
                SetDayPercentageCompleted(8, value);
                this.RaisePropertyChanged();
            }
        }

        public int? Day09
        {
            get => GetDayPercentageCompleted(9);
            set
            {
                SetDayPercentageCompleted(9, value);
                this.RaisePropertyChanged();
            }
        }

        public int? Day10
        {
            get => GetDayPercentageCompleted(10);
            set
            {
                SetDayPercentageCompleted(10, value);
                this.RaisePropertyChanged();
            }
        }

        public int? Day11
        {
            get => GetDayPercentageCompleted(11);
            set
            {
                SetDayPercentageCompleted(11, value);
                this.RaisePropertyChanged();
            }
        }

        public int? Day12
        {
            get => GetDayPercentageCompleted(12);
            set
            {
                SetDayPercentageCompleted(12, value);
                this.RaisePropertyChanged();
            }
        }

        public int? Day13
        {
            get => GetDayPercentageCompleted(13);
            set
            {
                SetDayPercentageCompleted(13, value);
                this.RaisePropertyChanged();
            }
        }

        public int? Day14
        {
            get => GetDayPercentageCompleted(14);
            set
            {
                SetDayPercentageCompleted(14, value);
                this.RaisePropertyChanged();
            }
        }

        public int? Day15
        {
            get => GetDayPercentageCompleted(15);
            set
            {
                SetDayPercentageCompleted(15, value);
                this.RaisePropertyChanged();
            }
        }

        public int? Day16
        {
            get => GetDayPercentageCompleted(16);
            set
            {
                SetDayPercentageCompleted(16, value);
                this.RaisePropertyChanged();
            }
        }

        public int? Day17
        {
            get => GetDayPercentageCompleted(17);
            set
            {
                SetDayPercentageCompleted(17, value);
                this.RaisePropertyChanged();
            }
        }

        public int? Day18
        {
            get => GetDayPercentageCompleted(18);
            set
            {
                SetDayPercentageCompleted(18, value);
                this.RaisePropertyChanged();
            }
        }

        public int? Day19
        {
            get => GetDayPercentageCompleted(19);
            set
            {
                SetDayPercentageCompleted(19, value);
                this.RaisePropertyChanged();
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
