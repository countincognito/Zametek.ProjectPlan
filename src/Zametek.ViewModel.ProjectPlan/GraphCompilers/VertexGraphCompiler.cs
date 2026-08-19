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
    }
}
