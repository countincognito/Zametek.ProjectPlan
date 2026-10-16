namespace Zametek.Contract.ProjectPlan
{
    /// <summary>
    /// The dock "Simulation Metrics": the tables of the result (the statistics, the critical paths and the criticalities).
    /// It shows what <see cref="IScheduleSimulationManagerViewModel"/> holds, and it feeds nothing back.
    /// </summary>
    public interface ISimulationMetricManagerViewModel
        : IKillSubscriptions, IDisposable
    {
        IScheduleSimulationManagerViewModel Simulation { get; }

        Task ReportErrorAsync(string message);
    }
}
