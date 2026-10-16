namespace Zametek.Engine.ProjectPlan
{
    // Why a job could not do what its request asked of the simulation.
    public enum SimulationFailure
    {
        // The request asked for the outputs of a simulation (its chart or its CSV files), the scenario holds no result, and the
        // request did not ask to run the simulation.
        NoResult,

        // The request asked to run the simulation, and the scenario has more iterations than a run may have here.
        IterationsAboveLimit,

        // The request asked to run the simulation, and the settings that the scenario has are not ones that a run can use: its
        // iterations are not from 1 to the most there is, or its shape value is not above 0.
        InvalidSettings,
    }
}
