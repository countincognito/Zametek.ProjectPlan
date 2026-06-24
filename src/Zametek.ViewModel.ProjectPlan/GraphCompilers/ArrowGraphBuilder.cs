using NPOI.SS.Formula.Functions;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ArrowGraphBuilder
        : ArrowGraphBuilder<int, int, int, IDependentActivity>
    {
        #region Ctors

        public ArrowGraphBuilder(
            IIdGenerator<int> edgeIdGenerator,
            IIdGenerator<int> nodeIdGenerator)
            : base(
                  edgeIdGenerator,
                  nodeIdGenerator,
                  new DummyActivityGenerator(),
                  new EventGenerator<int>(),
                  new ArrowTarjanStronglyConnectedComponentsFinder<int, int, int, IDependentActivity>(),
                  new ArrowCriticalPathEngine<int, int, int, IDependentActivity>(),
                  new PriorityListResourceScheduler<int, int, int>())
        {
        }

        public ArrowGraphBuilder(
            Graph<int, IDependentActivity, IEvent<int>> graph,
            IIdGenerator<int> edgeIdGenerator,
            IIdGenerator<int> nodeIdGenerator)
            : base(
                  graph,
                  edgeIdGenerator,
                  nodeIdGenerator,
                  new DummyActivityGenerator(),
                  new EventGenerator<int>(),
                  new ArrowTarjanStronglyConnectedComponentsFinder<int, int, int, IDependentActivity>(),
                  new ArrowCriticalPathEngine<int, int, int, IDependentActivity>(),
                  new PriorityListResourceScheduler<int, int, int>())
        {
        }

        #endregion

        public static ArrowGraphBuilder CreateDependentActivityArrowGraphBuilder()
        {
            int edgeId = default;
            int nodeId = default;
            return new ArrowGraphBuilder(
                new PreviousIdGenerator<int>(edgeId),
                new PreviousIdGenerator<int>(nodeId));
        }

        public static ArrowGraphBuilder CreateDependentActivityArrowGraphBuilder(Graph<int, IDependentActivity, IEvent<int>> arrowGraph)
        {
            int edgeId = arrowGraph.Edges.Select(x => x.Id).DefaultIfEmpty().Min();
            int nodeId = arrowGraph.Nodes.Select(x => x.Id).DefaultIfEmpty().Min();
            return new ArrowGraphBuilder(
                arrowGraph,
                new PreviousIdGenerator<int>(edgeId),
                new PreviousIdGenerator<int>(nodeId));
        }

        #region Overrides

        public override object CloneObject()
        {
            Graph<int, IDependentActivity, IEvent<int>> arrowGraphCopy = ToGraph();
            return CreateDependentActivityArrowGraphBuilder(arrowGraphCopy);
        }

        #endregion
    }




    public class DummyActivityGenerator
        : IActivityGenerator<int, int, int, IDependentActivity>
    {
        public IDependentActivity Generate(int id)
        {
            return new DependentActivity(id, 0, canBeRemoved: true);
        }
    }
}
