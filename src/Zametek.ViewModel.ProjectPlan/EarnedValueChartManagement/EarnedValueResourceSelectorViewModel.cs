using ReactiveUI;
using System.Collections.Specialized;
using System.Reactive.Linq;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// A resource selector for the earned value chart that keeps itself
    /// aligned with the core resource settings and the resource filter
    /// persisted in the project scenario display settings, and writes user
    /// edits of the selection back through to the display settings.
    /// </summary>
    public class EarnedValueResourceSelectorViewModel
        : ResourceSelectorViewModel, IKillSubscriptions, IDisposable
    {
        #region Fields

        private readonly ICoreViewModel m_CoreViewModel;

        // Distinguishes machine-driven selection changes (settings-driven
        // refreshes and seeding from the persisted filter) from genuine
        // user edits. Ref-counted like the Gantt selector's guard so that
        // overlapping revisions can never clear one another mid-flight
        // (today every reviser here runs on the UI thread, but the shape is
        // kept uniform with the dump-proven Gantt fix of 2026-08-17).
        private int m_RevisingCount;

        private readonly IDisposable? m_ResourceSettingsSub;
        private readonly IDisposable? m_ShowResourcesSub;

        #endregion

        #region Ctors

        public EarnedValueResourceSelectorViewModel(ICoreViewModel coreViewModel)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            m_CoreViewModel = coreViewModel;
            m_RevisingCount = 0;

            // Initial set up.
            ReviseResources();

            SelectedTargetResources.CollectionChanged += SelectedTargetResources_CollectionChanged;

            m_ResourceSettingsSub = this
                .WhenAnyValue(rs => rs.m_CoreViewModel.ResourceSettings)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(_ => ReviseResources());

            m_ShowResourcesSub = this
                .WhenAnyValue(rs => rs.m_CoreViewModel.DisplaySettingsViewModel.IsReadyToReviseEarnedValueShowResources)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(isReadyToRevise =>
                {
                    if (isReadyToRevise == ReadyToRevise.Yes)
                    {
                        ReviseResources();
                        m_CoreViewModel.DisplaySettingsViewModel.IsReadyToReviseEarnedValueShowResources = ReadyToRevise.No;
                    }
                });
        }

        #endregion

        #region Private Members

        private void SelectedTargetResources_CollectionChanged(
            object? sender,
            NotifyCollectionChangedEventArgs e)
        {
            // Write the selection through to the display settings so the
            // resource filter persists with the project scenario - but only
            // for genuine user edits. While revising (settings-driven
            // refreshes and seeding) the persisted list is the authority:
            // writing back the transient selection state would clobber it
            // (e.g. wiping a freshly remapped filter during a resource
            // renumber, where the selector may briefly empty out).
            if (m_RevisingCount == 0)
            {
                m_CoreViewModel.DisplaySettingsViewModel.EarnedValueShowResources.Clear();
                m_CoreViewModel.DisplaySettingsViewModel.EarnedValueShowResources.AddRange(SelectedResourceIds);
                m_CoreViewModel.DisplaySettingsViewModel.SetIsProjectScenarioUpdated(true);
            }
        }

        private void ReviseResources()
        {
            try
            {
                // Guard the revision so the resulting selection changes are
                // not mistaken for user edits: they must not overwrite the
                // persisted filter, nor mark the project scenario as updated.
                Interlocked.Increment(ref m_RevisingCount);

                var selectedTargetResources = new HashSet<int>(
                    m_CoreViewModel.DisplaySettingsViewModel.EarnedValueShowResources);

                IEnumerable<TargetResourceModel> targetResources = m_CoreViewModel
                    .ResourceSettings
                    .Resources
                    .Select(x => new TargetResourceModel
                    {
                        Id = x.Id,
                        Name = x.Name,
                    });

                SetTargetResources(targetResources, selectedTargetResources);
            }
            finally
            {
                Interlocked.Decrement(ref m_RevisingCount);
            }
        }

        #endregion

        #region IKillSubscriptions Members

        public void KillSubscriptions()
        {
            m_ResourceSettingsSub?.Dispose();
            m_ShowResourcesSub?.Dispose();
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
                SelectedTargetResources.CollectionChanged -= SelectedTargetResources_CollectionChanged;
                KillSubscriptions();
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
