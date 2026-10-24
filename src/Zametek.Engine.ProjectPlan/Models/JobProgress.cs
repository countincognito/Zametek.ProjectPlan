namespace Zametek.Engine.ProjectPlan
{
    // How far a job has got in the one step that takes long enough to report it: the simulation, which has run Done of its
    // Total iterations. A report comes at most about ten times a second, from any thread, and the last one has Done equal to
    // Total.
    public record JobProgress(
        int Done,
        int Total);
}
