using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    public sealed class VertexGraphBuilder
        : VertexGraphBuilderBase<int, int, int, IDependentActivity, IEvent<int>>
    {
        #region Fields

        private static readonly Func<int, IEvent<int>> s_EventGenerator = (id) =>
        {
            var output = new Event<int>(id);
            output.SetAsRemovable();
            return output;
        };

        #endregion

        #region Ctors

        public VertexGraphBuilder(
            Func<int> edgeIdGenerator,
            Func<int> nodeIdGenerator)
            : base(edgeIdGenerator, nodeIdGenerator, s_EventGenerator)
        {
        }

        public VertexGraphBuilder(
            Graph<int, IEvent<int>, IDependentActivity> graph,
            Func<int> edgeIdGenerator,
            Func<int> nodeIdGenerator)
            : base(
                  graph,
                  edgeIdGenerator,
                  nodeIdGenerator,
                  s_EventGenerator)
        {
            if (NormalNodes.Any())
            {
                // Check Start and End nodes.
                if (!StartNodes.Any())
                {
                    throw new ArgumentException(Resource.ProjectPlan.Messages.Message_VertexGraphCannotContainNormalNodesWithoutAnyStartNodes);
                }
                if (!EndNodes.Any())
                {
                    throw new ArgumentException(Resource.ProjectPlan.Messages.Message_VertexGraphCannotContainNormalNodesWithoutAnyEndNodes);
                }
            }
        }

        #endregion

        #region Properties

        public IDictionary<int, Node<int, IDependentActivity>> NodeLookupById => NodeLookup;

        #endregion

        #region Overrides

        public override object CloneObject()
        {
            Graph<int, IEvent<int>, IDependentActivity> vertexGraphCopy = ToGraph();
            int minNodeId = vertexGraphCopy.Nodes.Select(x => x.Id).DefaultIfEmpty().Min();
            minNodeId = minNodeId - 1;
            int minEdgeId = vertexGraphCopy.Edges.Select(x => x.Id).DefaultIfEmpty().Min();
            minEdgeId = minEdgeId - 1;
            return new VertexGraphBuilder<int, int, int, IDependentActivity>(
                vertexGraphCopy,
                () => minEdgeId = minEdgeId - 1,
                () => minNodeId = minNodeId - 1);
        }

        #endregion
    }
}
