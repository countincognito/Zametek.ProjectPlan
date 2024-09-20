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

        #region Private Members

        private int TrackerIndex => m_CoreViewModel.TrackerIndex;

        private IResourceActivitySelectorViewModel GetResourceActivitySelector(int index)
        {
            lock (m_Lock)
            {
                int indexOffset = index + TrackerIndex;
                if (m_ResourceActivitySelectorLookup.TryGetValue(indexOffset, out IResourceActivitySelectorViewModel? selector))
                {
                    return selector;
                }




                // TODO
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

        #region IResourceTrackerViewModel Members

        //public List<ResourceTrackerModel> Trackers => [.. m_ResourceTrackerLookup.Values.OrderBy(x => x.Time)];

        public int ResourceId { get; }

        public IResourceActivitySelectorViewModel Day00
        {
            get => GetResourceActivitySelector(0);
        }

        public IResourceActivitySelectorViewModel Day01
        {
            get => GetResourceActivitySelector(1);
        }

        public IResourceActivitySelectorViewModel Day02
        {
            get => GetResourceActivitySelector(2);
        }

        public IResourceActivitySelectorViewModel Day03
        {
            get => GetResourceActivitySelector(3);
        }

        public IResourceActivitySelectorViewModel Day04
        {
            get => GetResourceActivitySelector(4);
        }

        public IResourceActivitySelectorViewModel Day05
        {
            get => GetResourceActivitySelector(5);
        }

        public IResourceActivitySelectorViewModel Day06
        {
            get => GetResourceActivitySelector(6);
        }

        public IResourceActivitySelectorViewModel Day07
        {
            get => GetResourceActivitySelector(7);
        }

        public IResourceActivitySelectorViewModel Day08
        {
            get => GetResourceActivitySelector(8);
        }

        public IResourceActivitySelectorViewModel Day09
        {
            get => GetResourceActivitySelector(9);
        }

        public IResourceActivitySelectorViewModel Day10
        {
            get => GetResourceActivitySelector(10);
        }

        public IResourceActivitySelectorViewModel Day11
        {
            get => GetResourceActivitySelector(11);
        }

        public IResourceActivitySelectorViewModel Day12
        {
            get => GetResourceActivitySelector(12);
        }

        public IResourceActivitySelectorViewModel Day13
        {
            get => GetResourceActivitySelector(13);
        }

        public IResourceActivitySelectorViewModel Day14
        {
            get => GetResourceActivitySelector(14);
        }

        public IResourceActivitySelectorViewModel Day15
        {
            get => GetResourceActivitySelector(15);
        }

        public IResourceActivitySelectorViewModel Day16
        {
            get => GetResourceActivitySelector(16);
        }

        public IResourceActivitySelectorViewModel Day17
        {
            get => GetResourceActivitySelector(17);
        }

        public IResourceActivitySelectorViewModel Day18
        {
            get => GetResourceActivitySelector(18);
        }

        public IResourceActivitySelectorViewModel Day19
        {
            get => GetResourceActivitySelector(19);
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
                foreach (IResourceActivitySelectorViewModel selector in m_ResourceActivitySelectorLookup.Values)
                {
                    selector.Dispose();
                }
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
