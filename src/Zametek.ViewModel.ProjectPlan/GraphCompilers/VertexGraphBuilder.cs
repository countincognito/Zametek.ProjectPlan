using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    public sealed class VertexGraphBuilder
        : VertexGraphBuilder<int, int, int, IDependentActivity>
    {
        #region Ctors

        public VertexGraphBuilder(IIdGeneratorFactory<int> edgeIdGeneratorFactory)
            : base(
                  edgeIdGeneratorFactory,
                  new RemovableEventGenerator<int>(),
                  new VertexTarjanStronglyConnectedComponentsFinder<int, int, int, IDependentActivity>(),
                  new VertexCriticalPathEngine<int, int, int, IDependentActivity>(),
                  new PriorityListResourceScheduler<int, int, int>())
        {
        }

        public VertexGraphBuilder(
            Graph<int, IEvent<int>, IDependentActivity> graph,
            IIdGeneratorFactory<int> edgeIdGeneratorFactory)
            : base(
                  graph,
                  edgeIdGeneratorFactory,
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

        // The IDs of the events count down from -1. When a builder loads a graph, the factory starts them under the
        // lowest ID that the graph holds, so the IDs of a clone follow the IDs of its original without a gap.
        public static VertexGraphBuilder CreateDependentActivityVertexGraphBuilder()
        {
            return new VertexGraphBuilder(new PreviousIdGeneratorFactory<int>());
        }

        public static VertexGraphBuilder CreateDependentActivityVertexGraphBuilder(Graph<int, IEvent<int>, IDependentActivity> vertexGraph)
        {
            return new VertexGraphBuilder(vertexGraph, new PreviousIdGeneratorFactory<int>());
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
