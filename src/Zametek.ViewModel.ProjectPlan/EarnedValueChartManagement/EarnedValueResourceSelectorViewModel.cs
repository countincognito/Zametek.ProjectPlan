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
    /// <remarks>
    /// This deliberately mirrors <see cref="GanttActivitySelectorViewModel"/>
    /// and the two are meant to behave identically. In both, the persisted
    /// filter is applied by an explicit call to the Set-selected method and is
    /// never left to the Set-target method, because the latter only selects
    /// entries that are NEW to the selector - so a filter naming entries it
    /// already knows about would be silently dropped. That is precisely what
    /// used to happen here: every scenario in a file usually carries the same
    /// resource ids, so switching scenarios added and removed nothing, the
    /// select-on-add branch never ran, and the persisted filter was applied
    /// only on the very first load (when every resource was new).
    /// <para>
    /// The one structural difference between the two selectors is forced by
    /// where each collection lives on the core view model, and is not a
    /// difference in intent. Resources arrive as a whole ResourceSettings
    /// record whose setter raises, so a single WhenAnyValue subscription
    /// marshalled to the UI thread is sufficient, and it runs after
    /// DisplaySettingsViewModel.SetValues has installed the incoming filter.
    /// Activities live in a DynamicData SourceList that raises nothing when its
    /// contents change, so the Gantt selector needs the explicit
    /// IsReadyToReviseTrackers signal, which is observed on
    /// Scheduler.CurrentThread by design (tracker updates must complete before
    /// a compilation starts) and therefore runs DURING a scenario load, before
    /// SetValues. Its tracker-driven revise must consequently preserve the
    /// current selection rather than read a filter that still belongs to the
    /// outgoing scenario, and the authoritative seed is deferred to the
    /// connections signal that arrives afterwards. Here there is only one
    /// revise path, but it is structured the same way so that the seeding stays
    /// correct regardless of when, or on which thread, a revision arrives.
    /// </para>
    /// </remarks>
    public class EarnedValueResourceSelectorViewModel
        : ResourceSelectorViewModel, IKillSubscriptions, IDisposable
    {
        #region Fields

        private readonly ICoreViewModel m_CoreViewModel;

        // Non-zero only while a machine-driven revision is in flight - that is,
        // while this selector is being brought into line with state that has
        // already changed elsewhere (a scenario load, a resource settings edit,
        // or the initial seed). It does NOT suppress user edits: when the user
        // changes the selection in the chart's filter the count is zero, so the
        // write-through below both persists the new filter and marks the
        // project scenario as updated, which is exactly what should happen.
        //
        // Read it as an authority switch between the two copies of this state.
        // While revising, the display settings are authoritative and the
        // selection follows them; at all other times the selection is
        // authoritative and the display settings follow it. Without the guard a
        // revision would be mistaken for a user edit, and because
        // SetTargetResources mutates the selection incrementally, the
        // intermediate states would be written back too - wiping a freshly
        // remapped filter during a resource renumber (see
        // ProjectScenarioHelper), where the selector momentarily empties out.
        //
        // Ref-counted rather than a bool because a shared bool let one
        // reviser's finally clear another's guard mid-revision across threads,
        // leaking a machine-driven change out as a user edit - dump-proven in
        // the Gantt selector, 2026-08-17. Every reviser here runs on the UI
        // thread today, but the shape is kept uniform with that fix.
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

            SelectedTargetResources.CollectionChanged += SelectedTargetResources_CollectionChanged;

            // Initial set up. The seed is explicit rather than left to
            // SetTargetResources so that it still works when this selector is
            // constructed after a project scenario has already been loaded, in
            // which case the revise signal below has been consumed already and
            // will never fire.
            try
            {
                Interlocked.Increment(ref m_RevisingCount);
                ReviseResources();
                SetSelectedTargetResources(
                    [.. m_CoreViewModel.DisplaySettingsViewModel.EarnedValueShowResources]);
            }
            finally
            {
                Interlocked.Decrement(ref m_RevisingCount);
            }

            m_ResourceSettingsSub = this
                .WhenAnyValue(rs => rs.m_CoreViewModel.ResourceSettings)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    try
                    {
                        // The resource set changed, not the filter, so refresh
                        // the target list and keep the current selection (less
                        // any resources that no longer exist). The persisted
                        // filter is left alone: it tracks the selection, so it
                        // is already in step with it.
                        Interlocked.Increment(ref m_RevisingCount);
                        ReviseResources();
                    }
                    finally
                    {
                        Interlocked.Decrement(ref m_RevisingCount);
                    }
                });

            m_ShowResourcesSub = this
                .WhenAnyValue(rs => rs.m_CoreViewModel.DisplaySettingsViewModel.IsReadyToReviseEarnedValueShowResources)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(isReadyToRevise =>
                {
                    if (isReadyToRevise == ReadyToRevise.Yes)
                    {
                        try
                        {
                            // The persisted filter changed - a scenario was
                            // loaded, or its resource ids were remapped - so it
                            // is the authority here: refresh the target list,
                            // then apply the filter explicitly. Leaving this to
                            // SetTargetResources is not enough, because it only
                            // selects resources that are new to the selector.
                            Interlocked.Increment(ref m_RevisingCount);
                            ReviseResources();
                            SetSelectedTargetResources(
                                [.. m_CoreViewModel.DisplaySettingsViewModel.EarnedValueShowResources]);
                            m_CoreViewModel.DisplaySettingsViewModel.IsReadyToReviseEarnedValueShowResources = ReadyToRevise.No;
                        }
                        finally
                        {
                            Interlocked.Decrement(ref m_RevisingCount);
                        }
                    }
                });
        }

        #endregion

        #region Private Members

        private void SelectedTargetResources_CollectionChanged(
            object? sender,
            NotifyCollectionChangedEventArgs e)
        {
            // A user edit of the filter: persist it with the project scenario
            // and mark the scenario as updated, which is how a filter change
            // comes to be saved in the file at all. Machine-driven revisions
            // take the other branch and change nothing here, because the
            // display settings are the authority in that direction - see the
            // note on m_RevisingCount for why, and for what goes wrong without
            // it. This is the same shape as the Gantt selector's write-through
            // of GanttChartShowConnections.
            if (m_RevisingCount == 0)
            {
                m_CoreViewModel.DisplaySettingsViewModel.EarnedValueShowResources.Clear();
                m_CoreViewModel.DisplaySettingsViewModel.EarnedValueShowResources.AddRange(SelectedResourceIds);
                m_CoreViewModel.DisplaySettingsViewModel.SetIsProjectScenarioUpdated(true);
            }
        }

        // Brings the target list into line with the current resource settings,
        // preserving the current selection. It deliberately does NOT seed from
        // the persisted filter: callers that know the filter is the authority
        // apply it themselves, with SetSelectedTargetResources, which is the
        // only operation that can select a resource the selector already knows
        // about. Every caller must hold the revising guard, since this mutates
        // the selection and would otherwise be taken for a user edit.
        private void ReviseResources()
        {
            IEnumerable<TargetResourceModel> targetResources = m_CoreViewModel
                .ResourceSettings
                .Resources
                .Select(x => new TargetResourceModel
                {
                    Id = x.Id,
                    Name = x.Name,
                });

            SetTargetResources(targetResources, [.. SelectedResourceIds]);
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
