namespace Zametek.Contract.ProjectPlan
{
    /// <summary>
    /// The dock "Simulation": the settings of a run, the buttons that start and stop it, the progress of the run, and the
    /// name of the scenario that it simulates. The simulation itself belongs to <see cref="IScheduleSimulationManagerViewModel"/>,
    /// which three docks share (this one, the chart and the metrics).
    /// </summary>
    public interface ISimulationControlManagerViewModel
        : IStartSubscriptions, IKillSubscriptions, IDisposable
    {
        IScheduleSimulationManagerViewModel Simulation { get; }

        // The name of the scenario that is open, and thus the one that a run simulates. It is empty when no scenario is open.
        string ScenarioTitle { get; }

        bool HasScenarioTitle { get; }
    }
}
