namespace Zametek.Engine.ProjectPlan
{
    /// <summary>
    /// Thrown when a job cannot do what its request asked of the simulation. The settings of a run come from the scenario
    /// that the job loads, and a result that the outputs are made from can be the one that the scenario holds, so what is
    /// wrong is found only after the job has read its plan. The message says what went wrong in the engine's own words; the
    /// reason, and the numbers that it is about, are here as well, so that a host can say it in its own.
    /// </summary>
    public class SimulationException
        : Exception
    {
        public SimulationException()
        {
        }

        public SimulationException(string message)
            : base(message)
        {
        }

        public SimulationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public SimulationException(
            string message,
            SimulationFailure failure)
            : base(message)
        {
            Failure = failure;
        }

        public SimulationFailure Failure { get; init; }
    }
}
