using NPOI.SS.Formula.Functions;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    public sealed class VertexGraphBuilder
        : VertexGraphBuilder<int, int, int, IDependentActivity>
    {
        #region Ctors

        public VertexGraphBuilder(IIdGenerator<int> edgeIdGenerator)
            : base(
                  edgeIdGenerator,
                  new RemovableEventGenerator<int>(),
                  new VertexTarjanStronglyConnectedComponentsFinder<int, int, int, IDependentActivity>(),
                  new VertexCriticalPathEngine<int, int, int, IDependentActivity>(),
                  new PriorityListResourceScheduler<int, int, int>())
        {
        }

        public VertexGraphBuilder(
            Graph<int, IEvent<int>, IDependentActivity> graph,
            IIdGenerator<int> edgeIdGenerator)
            : base(
                  graph,
                  edgeIdGenerator,
                  new RemovableEventGenerator<int>(),
                  new VertexTarjanStronglyConnectedComponentsFinder<int, int, int, IDependentActivity>(),
                  new VertexCriticalPathEngine<int, int, int, IDependentActivity>(),
                  new PriorityListResourceScheduler<int, int, int>())
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

        public static VertexGraphBuilder CreateDependentActivityVertexGraphBuilder()
        {
            int edgeId = default;
            return new VertexGraphBuilder(new PreviousIdGenerator<int>(edgeId));
        }

        public static VertexGraphBuilder CreateDependentActivityVertexGraphBuilder(Graph<int, IEvent<int>, IDependentActivity> vertexGraph)
        {
            int edgeId = vertexGraph.Edges.Select(x => x.Id).DefaultIfEmpty().Min();
            return new VertexGraphBuilder(vertexGraph, new PreviousIdGenerator<int>(edgeId));
        }

        #region Overrides

        public override object CloneObject()
        {
            Graph<int, IEvent<int>, IDependentActivity> vertexGraphCopy = ToGraph();
            return CreateDependentActivityVertexGraphBuilder(vertexGraphCopy);
        }

        #endregion
    }
}
