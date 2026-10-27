using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// The input half of a compilation: a copy of the plan, taken in one short step, with what is needed
    /// to tell afterwards whether the results can still be used.
    /// </summary>
    /// <param name="Activities">The copies of the activities, in the order of the live graph.</param>
    /// <param name="Sources">The live activity that each copy was made from.</param>
    /// <param name="Resources">The resources that the compilation schedules.</param>
    /// <param name="WorkStreams">The work streams that the compilation uses.</param>
    /// <param name="InputRevision">The revision of what a compilation reads, when the copies were taken.</param>
    /// <param name="ScenarioRevision">The scenario that the copies belong to.</param>
    /// <param name="Ticket">Puts this snapshot in order among the others.</param>
    internal sealed record CompilationSnapshot(
        List<IDependentActivity> Activities,
        IReadOnlyDictionary<int, IManagedActivityViewModel> Sources,
        List<IResource<int, int>> Resources,
        List<IWorkStream<int>> WorkStreams,
        int InputRevision,
        int ScenarioRevision,
        int Ticket);
}
