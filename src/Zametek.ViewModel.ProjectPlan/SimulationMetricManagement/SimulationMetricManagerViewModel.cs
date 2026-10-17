using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// The dock "Simulation Metrics". It is a display-only panel: the tables of the result belong to the shared
    /// <see cref="ISimulationChartManagerViewModel"/>, and the view binds to them through <see cref="Simulation"/>.
    /// </summary>
    public class SimulationMetricManagerViewModel
        : ToolViewModelBase, ISimulationMetricManagerViewModel
    {
        #region Fields

        private readonly IDialogService m_DialogService;

        #endregion

        #region Ctors

        public SimulationMetricManagerViewModel(
            ISimulationChartManagerViewModel simulationChartManagerViewModel,
            IDialogService dialogService)
        {
            ArgumentNullException.ThrowIfNull(simulationChartManagerViewModel);
            ArgumentNullException.ThrowIfNull(dialogService);
            Simulation = simulationChartManagerViewModel;
            m_DialogService = dialogService;

            Id = Resource.ProjectPlan.Titles.Title_SimulationMetrics;
            Title = Resource.ProjectPlan.Titles.Title_SimulationMetrics;
        }

        #endregion

        #region ISimulationMetricManagerViewModel Members

        public ISimulationChartManagerViewModel Simulation { get; }

        public Task ReportErrorAsync(string message)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
            return m_DialogService.ShowErrorAsync(
                Resource.ProjectPlan.Titles.Title_Error,
                string.Empty,
                message);
        }

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
