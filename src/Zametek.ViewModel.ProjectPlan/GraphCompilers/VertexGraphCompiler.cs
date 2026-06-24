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
