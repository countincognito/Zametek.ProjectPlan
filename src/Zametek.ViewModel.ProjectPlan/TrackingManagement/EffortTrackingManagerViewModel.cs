using ReactiveUI;
using System.Collections.Specialized;
using System.Reactive;
using System.Reactive.Linq;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    // A dock tab can only be occupied by one dockable, so each tracking tab
    // gets a thin dockable host while all tracking state and behavior stay
    // in the shared ITrackingManagerViewModel. The effort host additionally
    // owns the timesheet sections (one per resource), which are specific to
    // the effort tab.
    public class EffortTrackingManagerViewModel
        : TrackingManagerViewModel, IEffortTrackingManagerViewModel
    {
        #region Fields

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IResourceSettingsManagerViewModel m_ResourceSettingsManagerViewModel;

        // Remembers each resource section's expanded state across rebuilds.
        private readonly Dictionary<int, bool> m_ExpandedLookup;
        private int m_LastWindowIndex;

        private readonly IDisposable? m_TimesheetSub;

        #endregion

        #region Ctors

        public EffortTrackingManagerViewModel(
            ICoreViewModel coreViewModel,
            IResourceSettingsManagerViewModel resourceSettingsManagerViewModel,
            IDateTimeCalculator dateTimeCalculator)
            : base(coreViewModel, resourceSettingsManagerViewModel, dateTimeCalculator)
        {
            m_CoreViewModel = coreViewModel;
            m_ResourceSettingsManagerViewModel = resourceSettingsManagerViewModel;
            m_ExpandedLookup = [];
            m_LastWindowIndex = 0;
            m_TimesheetSections = [];
            m_NameColumnWidth = 150;

            // Rebuild the timesheet sections whenever the visible window
            // moves, the trackers are revised, or the resource/activity
            // collections change. The orderable collections are observed so
            // drag-and-drop reordering in the settings grids repositions the
            // sections and rows immediately. Everything runs on the UI thread
            // because the sections read and write view models that are bound
            // to the view.
            m_TimesheetSub = Observable.Merge(
                    this.WhenAnyValue(tm => tm.m_CoreViewModel.TrackerIndex)
                        .Select(_ => Unit.Default),
                    this.WhenAnyValue(tm => tm.m_CoreViewModel.IsReadyToReviseTrackers)
                        .Select(_ => Unit.Default),
                    Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                            handler => ((INotifyCollectionChanged)m_ResourceSettingsManagerViewModel.OrderableResources).CollectionChanged += handler,
                            handler => ((INotifyCollectionChanged)m_ResourceSettingsManagerViewModel.OrderableResources).CollectionChanged -= handler)
                        .Select(_ => Unit.Default),
                    Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                            handler => ((INotifyCollectionChanged)m_CoreViewModel.OrderableActivities).CollectionChanged += handler,
                            handler => ((INotifyCollectionChanged)m_CoreViewModel.OrderableActivities).CollectionChanged -= handler)
                        .Select(_ => Unit.Default))
                .MuteWhile(this.WhenAnyValue(tm => tm.m_CoreViewModel.IsBulkUpdating)) // Conflate redundant notifications while a project scenario is loaded/reset.
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(_ => RefreshTimesheet());

            Id = Resource.ProjectPlan.Titles.Title_EffortTrackingView;
            Title = Resource.ProjectPlan.Titles.Title_EffortTrackingView;
        }

        #endregion

        #region Private Members

        private void RefreshTimesheet()
        {
            CascadeDiagnostics.RecordBuild($@"{nameof(EffortTrackingManagerViewModel)}.{nameof(RefreshTimesheet)}");

            // Session rows only survive navigation while the leading-days
            // rule in each section says the user is still nearby.
            bool windowMoved = m_LastWindowIndex != TrackerIndex;
            m_LastWindowIndex = TrackerIndex;

            // Snapshots of the orderable collections, so sections and rows
            // follow the settings grids' display (drag) order.
            IReadOnlyList<IManagedActivityViewModel> activities = [.. m_CoreViewModel.OrderableActivities];
            List<IManagedResourceViewModel> resources = [.. m_ResourceSettingsManagerViewModel.OrderableResources];

            // Compare the live resource INSTANCES, not just their ids: a
            // scenario switch rebuilds the managed resources with the same
            // ids, and sections must not keep driving stale, disposed ones.
            bool structureChanged =
                m_TimesheetSections.Count != resources.Count
                || !m_TimesheetSections.Select(section => section.Resource).SequenceEqual(resources);

            if (structureChanged)
            {
                m_TimesheetSections = [.. resources.Select(resource => new ResourceTimesheetViewModel(
                    this,
                    resource,
                    TrackingHelper.DayCount,
                    !m_ExpandedLookup.TryGetValue(resource.Id, out bool isExpanded) || isExpanded,
                    (resourceId, expanded) => m_ExpandedLookup[resourceId] = expanded))];
                this.RaisePropertyChanged(nameof(TimesheetSections));
            }

            foreach (ResourceTimesheetViewModel section in m_TimesheetSections)
            {
                section.Refresh(activities, windowMoved);
            }
        }

        #endregion

        #region IEffortTrackingManagerViewModel Members

        private List<ResourceTimesheetViewModel> m_TimesheetSections;
        public IReadOnlyList<IResourceTimesheetViewModel> TimesheetSections => m_TimesheetSections;

        private double m_NameColumnWidth;
        public double NameColumnWidth
        {
            get => m_NameColumnWidth;
            set
            {
                if (m_NameColumnWidth.Equals(value))
                {
                    return;
                }

                m_NameColumnWidth = value;
                this.RaisePropertyChanged();

                // Every timesheet section forwards this width to its own grid,
                // so a resize in any one grid has to be re-raised on the rest.
                List<ResourceTimesheetViewModel> sections = m_TimesheetSections;

                foreach (ResourceTimesheetViewModel section in sections)
                {
                    section.RaiseNameColumnWidthChanged();
                }
            }
        }

        #endregion

        #region IDisposable Members

        private bool m_HostDisposed = false;

        protected override void Dispose(bool disposing)
        {
            if (m_HostDisposed)
            {
                base.Dispose(disposing);
                return;
            }

            if (disposing)
            {
                m_TimesheetSub?.Dispose();
            }

            m_HostDisposed = true;
            base.Dispose(disposing);
        }

        #endregion
    }
}
