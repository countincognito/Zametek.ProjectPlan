using ReactiveUI;
using System.Reactive.Linq;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ResourceTrackersViewModel
        : ViewModelBase, IResourceTrackersViewModel
    {
        #region Fields

        private readonly object m_Lock;
        private readonly ICoreViewModel m_CoreViewModel;
        private readonly Dictionary<int, IResourceActivitySelectorViewModel> m_ResourceActivitySelectorLookup;

        private readonly IDisposable? m_DaysSub;

        private static readonly IResourceActivitySelectorViewModel s_Empty = null;

        #endregion

        #region Ctors

        public ResourceTrackersViewModel(
            ICoreViewModel coreViewModel,
            int resourceId,
            IEnumerable<ResourceTrackerModel> trackers)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            m_Lock = new object();
            m_CoreViewModel = coreViewModel;
            ResourceId = resourceId;
            m_ResourceActivitySelectorLookup = [];

            foreach (ResourceTrackerModel tracker in trackers)
            {
                if (tracker.ResourceId == ResourceId)
                {
                    var selector = new ResourceActivitySelectorViewModel(m_CoreViewModel, tracker);
                    m_ResourceActivitySelectorLookup.TryAdd(tracker.Time, selector);
                }
            }

            m_DaysSub = this
                .WhenAnyValue(x => x.m_CoreViewModel.TrackerIndex)
                .ObserveOn(RxApp.TaskpoolScheduler) // TODO check this is good
                .Subscribe(_ => RefreshDays());
        }

        #endregion










        public IResourceActivitySelectorViewModel? Day
        {
            get
            {
                lock (m_Lock)
                {
                    int indexOffset = TrackerIndex;
                    if (m_ResourceActivitySelectorLookup.TryGetValue(indexOffset, out IResourceActivitySelectorViewModel? selector))
                    {
                        return selector;
                    }
                    var a = new ResourceActivitySelectorViewModel(
                        m_CoreViewModel,
                        new ResourceTrackerModel
                        {
                            Time = indexOffset,
                            ResourceId = ResourceId,
                            ActivityTrackers = []
                        });
                    m_ResourceActivitySelectorLookup.Add(indexOffset, a); // TODO clean up empty selectors at compile time.
                    return a;
                }
            }
        }






















        #region Private Members

        private int TrackerIndex => m_CoreViewModel.TrackerIndex;

        //private List<ResourceActivityTrackerModel> GetDayActivityTrackers(int index)
        //{
        //    lock (m_Lock)
        //    {
        //        int indexOffset = index + TrackerIndex;
        //        if (m_ResourceTrackerLookup.TryGetValue(indexOffset, out ResourceTrackerModel? tracker))
        //        {
        //            return tracker.ActivityTrackers;
        //        }
        //        return [];
        //    }
        //}

        //private void SetDayActivityTrackers(
        //    int index,
        //    List<ResourceActivityTrackerModel> value)
        //{
        //    lock (m_Lock)
        //    {
        //        int indexOffset = index + TrackerIndex;
        //        m_ResourceTrackerLookup.Remove(indexOffset);
        //        if (value is not null
        //            && value.Count > 0)
        //        {
        //            ResourceTrackerModel tracker = new()
        //            {
        //                Time = indexOffset,
        //                ResourceId = ResourceId,
        //                ActivityTrackers = value,
        //            };
        //            m_ResourceTrackerLookup.TryAdd(indexOffset, tracker);
        //        }
        //    }
        //}

        private void RefreshDays()
        {
            //this.RaisePropertyChanged(nameof(Day00));
            //this.RaisePropertyChanged(nameof(Day01));
            //this.RaisePropertyChanged(nameof(Day02));
            //this.RaisePropertyChanged(nameof(Day03));
            //this.RaisePropertyChanged(nameof(Day04));
            //this.RaisePropertyChanged(nameof(Day05));
            //this.RaisePropertyChanged(nameof(Day06));
            //this.RaisePropertyChanged(nameof(Day07));
            //this.RaisePropertyChanged(nameof(Day08));
            //this.RaisePropertyChanged(nameof(Day09));
            //this.RaisePropertyChanged(nameof(Day10));
            //this.RaisePropertyChanged(nameof(Day11));
            //this.RaisePropertyChanged(nameof(Day12));
            //this.RaisePropertyChanged(nameof(Day13));
            //this.RaisePropertyChanged(nameof(Day14));
            //this.RaisePropertyChanged(nameof(Day15));
            //this.RaisePropertyChanged(nameof(Day16));
            //this.RaisePropertyChanged(nameof(Day17));
            //this.RaisePropertyChanged(nameof(Day18));
            //this.RaisePropertyChanged(nameof(Day19));
        }

        #endregion

        #region IResourceTrackerViewModel Members

        //public List<ResourceTrackerModel> Trackers => [.. m_ResourceTrackerLookup.Values.OrderBy(x => x.Time)];

        public int ResourceId { get; }

        //public List<ResourceActivityTrackerModel> Day00
        //{
        //    get => GetDayActivityTrackers(0);
        //    set
        //    {
        //        SetDayActivityTrackers(0, value);
        //        this.RaisePropertyChanged();
        //    }
        //}

        //public List<ResourceActivityTrackerModel> Day01
        //{
        //    get => GetDayActivityTrackers(1);
        //    set
        //    {
        //        SetDayActivityTrackers(1, value);
        //        this.RaisePropertyChanged();
        //    }
        //}

        //public List<ResourceActivityTrackerModel> Day02
        //{
        //    get => GetDayActivityTrackers(2);
        //    set
        //    {
        //        SetDayActivityTrackers(2, value);
        //        this.RaisePropertyChanged();
        //    }
        //}

        //public List<ResourceActivityTrackerModel> Day03
        //{
        //    get => GetDayActivityTrackers(3);
        //    set
        //    {
        //        SetDayActivityTrackers(3, value);
        //        this.RaisePropertyChanged();
        //    }
        //}

        //public List<ResourceActivityTrackerModel> Day04
        //{
        //    get => GetDayActivityTrackers(4);
        //    set
        //    {
        //        SetDayActivityTrackers(4, value);
        //        this.RaisePropertyChanged();
        //    }
        //}

        //public List<ResourceActivityTrackerModel> Day05
        //{
        //    get => GetDayActivityTrackers(5);
        //    set
        //    {
        //        SetDayActivityTrackers(5, value);
        //        this.RaisePropertyChanged();
        //    }
        //}

        //public List<ResourceActivityTrackerModel> Day06
        //{
        //    get => GetDayActivityTrackers(6);
        //    set
        //    {
        //        SetDayActivityTrackers(6, value);
        //        this.RaisePropertyChanged();
        //    }
        //}

        //public List<ResourceActivityTrackerModel> Day07
        //{
        //    get => GetDayActivityTrackers(7);
        //    set
        //    {
        //        SetDayActivityTrackers(7, value);
        //        this.RaisePropertyChanged();
        //    }
        //}

        //public List<ResourceActivityTrackerModel> Day08
        //{
        //    get => GetDayActivityTrackers(8);
        //    set
        //    {
        //        SetDayActivityTrackers(8, value);
        //        this.RaisePropertyChanged();
        //    }
        //}

        //public List<ResourceActivityTrackerModel> Day09
        //{
        //    get => GetDayActivityTrackers(9);
        //    set
        //    {
        //        SetDayActivityTrackers(9, value);
        //        this.RaisePropertyChanged();
        //    }
        //}

        //public List<ResourceActivityTrackerModel> Day10
        //{
        //    get => GetDayActivityTrackers(10);
        //    set
        //    {
        //        SetDayActivityTrackers(10, value);
        //        this.RaisePropertyChanged();
        //    }
        //}

        //public List<ResourceActivityTrackerModel> Day11
        //{
        //    get => GetDayActivityTrackers(11);
        //    set
        //    {
        //        SetDayActivityTrackers(11, value);
        //        this.RaisePropertyChanged();
        //    }
        //}

        //public List<ResourceActivityTrackerModel> Day12
        //{
        //    get => GetDayActivityTrackers(12);
        //    set
        //    {
        //        SetDayActivityTrackers(12, value);
        //        this.RaisePropertyChanged();
        //    }
        //}

        //public List<ResourceActivityTrackerModel> Day13
        //{
        //    get => GetDayActivityTrackers(13);
        //    set
        //    {
        //        SetDayActivityTrackers(13, value);
        //        this.RaisePropertyChanged();
        //    }
        //}

        //public List<ResourceActivityTrackerModel> Day14
        //{
        //    get => GetDayActivityTrackers(14);
        //    set
        //    {
        //        SetDayActivityTrackers(14, value);
        //        this.RaisePropertyChanged();
        //    }
        //}

        //public List<ResourceActivityTrackerModel> Day15
        //{
        //    get => GetDayActivityTrackers(15);
        //    set
        //    {
        //        SetDayActivityTrackers(15, value);
        //        this.RaisePropertyChanged();
        //    }
        //}

        //public List<ResourceActivityTrackerModel> Day16
        //{
        //    get => GetDayActivityTrackers(16);
        //    set
        //    {
        //        SetDayActivityTrackers(16, value);
        //        this.RaisePropertyChanged();
        //    }
        //}

        //public List<ResourceActivityTrackerModel> Day17
        //{
        //    get => GetDayActivityTrackers(17);
        //    set
        //    {
        //        SetDayActivityTrackers(17, value);
        //        this.RaisePropertyChanged();
        //    }
        //}

        //public List<ResourceActivityTrackerModel> Day18
        //{
        //    get => GetDayActivityTrackers(18);
        //    set
        //    {
        //        SetDayActivityTrackers(18, value);
        //        this.RaisePropertyChanged();
        //    }
        //}

        //public List<ResourceActivityTrackerModel> Day19
        //{
        //    get => GetDayActivityTrackers(19);
        //    set
        //    {
        //        SetDayActivityTrackers(19, value);
        //        this.RaisePropertyChanged();
        //    }
        //}

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
