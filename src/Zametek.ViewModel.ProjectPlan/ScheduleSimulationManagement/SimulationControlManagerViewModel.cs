using ReactiveUI;
using System.Reactive.Linq;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// The dock "Simulation". It has no logic of its own for the simulation: the settings, the buttons, the progress and
    /// the status all belong to the shared <see cref="IScheduleSimulationManagerViewModel"/>, and the view binds to them
    /// through <see cref="Simulation"/>. What it adds is the name of the scenario that a run simulates.
    /// </summary>
    public class SimulationControlManagerViewModel
        : ToolViewModelBase, ISimulationControlManagerViewModel
    {
        #region Fields

        private readonly IProjectScenarioManagerViewModel m_ProjectScenarioManagerViewModel;
        private readonly ISettingService m_SettingService;

        // The reactive pipeline, made by StartSubscriptions and not by the constructor: see IStartSubscriptions.
        private readonly SubscriptionLifetime m_SubscriptionLifetime = new();
        private IDisposable? m_ScenarioTitleSub;

        #endregion

        #region Ctors

        public SimulationControlManagerViewModel(
            IScheduleSimulationManagerViewModel scheduleSimulationManagerViewModel,
            IProjectScenarioManagerViewModel projectScenarioManagerViewModel,
            ISettingService settingService)
        {
            ArgumentNullException.ThrowIfNull(scheduleSimulationManagerViewModel);
            ArgumentNullException.ThrowIfNull(projectScenarioManagerViewModel);
            ArgumentNullException.ThrowIfNull(settingService);
            Simulation = scheduleSimulationManagerViewModel;
            m_ProjectScenarioManagerViewModel = projectScenarioManagerViewModel;
            m_SettingService = settingService;
            m_ScenarioTitle = settingService.ScenarioTitle;

            Id = Resource.ProjectPlan.Titles.Title_Simulation;
            Title = Resource.ProjectPlan.Titles.Title_Simulation;
        }

        #endregion

        #region ISimulationControlManagerViewModel Members

        public IScheduleSimulationManagerViewModel Simulation { get; }

        private string m_ScenarioTitle;
        public string ScenarioTitle
        {
            get => m_ScenarioTitle;
            private set
            {
                if (m_ScenarioTitle == value)
                {
                    return;
                }

                m_ScenarioTitle = value;
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(HasScenarioTitle));
            }
        }

        public bool HasScenarioTitle => !string.IsNullOrWhiteSpace(m_ScenarioTitle);

        #endregion

        #region IStartSubscriptions Members

        public void StartSubscriptions()
        {
            if (!m_SubscriptionLifetime.TryStart())
            {
                return;
            }

            // The name of the scenario is a setting that has no event of its own. The title bar needs the same name, and
            // the scenario manager arms it, after the settings are changed, each time a scenario loads, closes or is
            // renamed. So the name is read from the settings when the title is armed, as the title bar reads it.
            m_ScenarioTitleSub = this
                .WhenAnyValue(sc => sc.m_ProjectScenarioManagerViewModel.TitleRevision)
                .Select(_ => m_SettingService.ScenarioTitle)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(title => ScenarioTitle = title);
        }

        #endregion

        #region IKillSubscriptions Members

        public void KillSubscriptions()
        {
            m_SubscriptionLifetime.Kill();
            m_ScenarioTitleSub?.Dispose();
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
