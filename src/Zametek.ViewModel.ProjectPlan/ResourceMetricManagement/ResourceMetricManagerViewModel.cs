using ReactiveUI;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    // A display-only panel: it consumes the per-resource metrics that the core
    // view model rebuilds as part of the compile cascade, and feeds nothing
    // back. The rows arrive pre-sorted in resource settings display order, so
    // the view simply renders the list as-is.
    public class ResourceMetricManagerViewModel
        : ToolViewModelBase, IResourceMetricManagerViewModel
    {
        #region Fields

        private readonly ICoreViewModel m_CoreViewModel;

        #endregion

        #region Ctors

        public ResourceMetricManagerViewModel(ICoreViewModel coreViewModel)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            m_CoreViewModel = coreViewModel;

            m_IsBusy = this
                .WhenAnyValue(rm => rm.m_CoreViewModel.IsBusy)
                .ToProperty(this, rm => rm.IsBusy);

            m_HasStaleOutputs = this
                .WhenAnyValue(rm => rm.m_CoreViewModel.HasStaleOutputs)
                .ToProperty(this, rm => rm.HasStaleOutputs);

            m_HasCompilationErrors = this
                .WhenAnyValue(rm => rm.m_CoreViewModel.HasCompilationErrors)
                .ToProperty(this, rm => rm.HasCompilationErrors);

            m_HideCost = this
                .WhenAnyValue(rm => rm.m_CoreViewModel.DisplaySettingsViewModel.HideCost)
                .ToProperty(this, rm => rm.HideCost);

            m_HideBilling = this
                .WhenAnyValue(rm => rm.m_CoreViewModel.DisplaySettingsViewModel.HideBilling)
                .ToProperty(this, rm => rm.HideBilling);

            m_ResourceMetrics = this
                .WhenAnyValue(rm => rm.m_CoreViewModel.ResourceMetrics)
                .ToProperty(this, rm => rm.ResourceMetrics);

            Id = Resource.ProjectPlan.Titles.Title_ResourceMetrics;
            Title = Resource.ProjectPlan.Titles.Title_ResourceMetrics;
        }

        #endregion

        #region IResourceMetricManagerViewModel Members

        private readonly ObservableAsPropertyHelper<bool> m_IsBusy;
        public bool IsBusy => m_IsBusy.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasStaleOutputs;
        public bool HasStaleOutputs => m_HasStaleOutputs.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasCompilationErrors;
        public bool HasCompilationErrors => m_HasCompilationErrors.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HideCost;
        public bool HideCost => m_HideCost.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HideBilling;
        public bool HideBilling => m_HideBilling.Value;

        private readonly ObservableAsPropertyHelper<List<ResourceMetricsModel>> m_ResourceMetrics;
        public List<ResourceMetricsModel> ResourceMetrics => m_ResourceMetrics.Value;

        #endregion

        #region IKillSubscriptions Members

        public void KillSubscriptions()
        {
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
                KillSubscriptions();
                m_IsBusy?.Dispose();
                m_HasStaleOutputs?.Dispose();
                m_HasCompilationErrors?.Dispose();
                m_HideCost?.Dispose();
                m_HideBilling?.Dispose();
                m_ResourceMetrics?.Dispose();
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
