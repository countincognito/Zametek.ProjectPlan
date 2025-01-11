using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    public class VertexGraphCompiler
        : VertexGraphCompilerBase<int, int, int, IDependentActivity, IActivity<int, int, int>, IEvent<int>>
    {
        #region Fields

        private readonly VertexGraphBuilder m_VertexGraphBuilder;

        #endregion

        #region Ctors

        protected VertexGraphCompiler(VertexGraphBuilder vertexGraphBuilder)
            : base(vertexGraphBuilder)
        {
            ArgumentNullException.ThrowIfNull(nameof(vertexGraphBuilder));
            m_VertexGraphBuilder = vertexGraphBuilder;


        }

        public VertexGraphCompiler()
            : this(CreateDependentActivityVertexGraphBuilder())
        {
        }

        #endregion

        #region Properties

        public bool IsIsolated(int activityId)
        {
            if (m_VertexGraphBuilder.NodeLookupById.TryGetValue(activityId, out Node<int, IDependentActivity>? node))
            {
                if (node.NodeType == NodeType.Isolated)
                {
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region Private Methods

        private static VertexGraphBuilder CreateDependentActivityVertexGraphBuilder()
        {
            int edgeId = default;
            int nodeId = default;
            return new VertexGraphBuilder(
                () => edgeId = edgeId - 1,
                () => nodeId = nodeId - 1);
        }

        #endregion
    }
}
