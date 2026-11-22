using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    public class VertexGraphCompiler
        : VertexGraphCompiler<int, int, int, IDependentActivity>
    {
        #region Fields

        private readonly VertexGraphBuilder m_VertexGraphBuilder;

        #endregion

        #region Ctors

        protected VertexGraphCompiler(VertexGraphBuilder vertexGraphBuilder)
            : base(vertexGraphBuilder)
        {
            m_VertexGraphBuilder = vertexGraphBuilder;
        }

        public VertexGraphCompiler()
            : this(VertexGraphBuilder.CreateDependentActivityVertexGraphBuilder())
        {
        }

        #endregion

        /// <summary>
        /// The ids of the registered activities, in the order the graph holds them.
        /// </summary>
        /// <remarks>
        /// A compilation snapshots the activities and compiles a throwaway copy of the
        /// graph, and that copy has to be built in this order: the priority list keeps
        /// the first of any activities that tie on slack, so the order in which the
        /// activities are held decides which of them a tie goes to. Rebuilding in any
        /// other order would schedule a tie differently from the graph held here.
        /// </remarks>
        public IEnumerable<int> ActivityIds => m_VertexGraphBuilder.ActivityIds;

        public bool IsIsolated(int activityId)
        {
            Node<int, IDependentActivity>? node = m_VertexGraphBuilder.Node(activityId);

            if (node is not null && node.NodeType == NodeType.Isolated)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Calculates the times and the slack of each activity from the links that the graph holds, and schedules no
        /// resource. Call it on a compiler that only one thread can reach.
        /// </summary>
        /// <remarks>
        /// <c>Compile</c> of the library always schedules. With resources, it puts the activities on them, and it gives
        /// resource dependencies, which can change the times. With no resources, it puts the activities on lanes of its
        /// own. Each schedule also clears the resources of each activity and gives them again. Thus, <c>Compile</c> cannot
        /// give the times of a graph whose links and resources must stay as they are, and this method can. It does the
        /// two steps of <c>Compile</c> that give the times, and no other step: the critical path, and then the values of
        /// the activities that have no link.
        /// <para>
        /// The vertex graph of a compiled plan is such a graph (refer to <see cref="GraphCompilationService.BuildVertexGraph"/>).
        /// Its links hold the schedule that the compilation of the plan made, and a second schedule can be a different one.
        /// </para>
        /// <para>
        /// The method uses the builder outside the lock of the compiler, because that lock is private to the library
        /// (<see cref="SetUserDependencies"/> does the same). It changes nothing in the activities but their times and
        /// their slack: the resources of an activity and its dependencies stay as they are.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// The calculation is not possible: the links make a circle, or an activity has a constraint that its links do not
        /// let it meet. A plan that compiled with no errors has neither.
        /// </exception>
        public void CalculateCriticalPath()
        {
            m_VertexGraphBuilder.CalculateCriticalPath();

            // The critical path does not reach an activity that has no link. Compile fills in its values with this step,
            // and treats a refusal as a failure.
            if (!m_VertexGraphBuilder.BackFillIsolatedNodes())
            {
                throw new InvalidOperationException(Resource.ProjectPlan.Messages.Message_CannotBuildVertexGraph);
            }
        }

        /// <summary>
        /// Sets the dependencies and the planning dependencies that the user entered for an activity, and leaves no link in
        /// the graph that only a resource gave. Call it with the leaf lock of the plan held.
        /// </summary>
        /// <remarks>
        /// <c>SetActivityDependencies</c> makes the links of the activity from its dependencies, its planning dependencies
        /// <i>and</i> its resource dependencies. It reads the last ones from the activity itself, and a compilation of the
        /// plan writes them there, as a result. The graph of a plan has to hold the links of the user alone. A compilation
        /// that ran on the graph itself took the links of the resources out again at its end, and a compilation of a copy
        /// does not. Without this method, an activity that only a resource links to another one would not be isolated, and
        /// a dependency that the user takes away would stay as a link.
        /// <para>
        /// The builder takes the links out, outside the lock of the compiler, because that lock is private to the library.
        /// Thus, the caller holds the leaf lock, and so does every other write to the graph.
        /// </para>
        /// </remarks>
        public bool SetUserDependencies(int activityId, HashSet<int> dependencies, HashSet<int> planningDependencies)
        {
            bool isSet = SetActivityDependencies(activityId, dependencies, planningDependencies);

            Node<int, IDependentActivity>? node = m_VertexGraphBuilder.Node(activityId);

            if (node is not null)
            {
                m_VertexGraphBuilder.RemoveResourceOnlyDependencies([node.Content]);
            }

            return isSet;
        }
    }
}
