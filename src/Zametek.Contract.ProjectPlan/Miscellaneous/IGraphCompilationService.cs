using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Contract.ProjectPlan
{
    /// <summary>
    /// Builds the two graphs that show a compiled plan. Each build takes the activities as the compilation of the plan
    /// gave them, and its graph shows the times of that compilation. Neither build schedules the resources: the
    /// compilation of the plan did that, and a second schedule can be a different one.
    /// </summary>
    public interface IGraphCompilationService
    {
        ArrowGraphModel BuildArrowGraph(
            IEnumerable<IDependentActivity> dependentActivities);

        /// <summary>
        /// Builds the vertex graph: a node for each activity, with the times that the compilation gave it, and a link for
        /// each dependency, also for a dependency that a resource gave.
        /// </summary>
        /// <remarks>
        /// The method does not take the resources or the work streams. The activities hold the schedule already, and the
        /// build must not make another one.
        /// </remarks>
        VertexGraphModel BuildVertexGraph(
            IEnumerable<IDependentActivity> dependentActivities);
    }
}
