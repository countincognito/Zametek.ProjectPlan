using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// What the transitive reduction of a copy of the plan gave: the compilation that checked the copy before it, and the
    /// dependencies of the user that each activity had before the reduction and has after it.
    /// </summary>
    /// <param name="Compilation">
    /// The compilation of the copies with infinite resources. When it has errors the copies were not reduced, and so
    /// <paramref name="After"/> holds what <paramref name="Before"/> holds.
    /// </param>
    /// <param name="Before">The dependencies of each activity, as they were copied.</param>
    /// <param name="After">The dependencies of each activity, when the dependencies that others imply are away.</param>
    internal sealed record TransitiveReduction(
        IGraphCompilation<int, int, int, IDependentActivity> Compilation,
        IReadOnlyDictionary<int, ActivityDependencies> Before,
        IReadOnlyDictionary<int, ActivityDependencies> After);
}
