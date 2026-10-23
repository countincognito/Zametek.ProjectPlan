using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ArrowGraphBuilder
        : ArrowGraphBuilder<int, int, int, IDependentActivity>
    {
        #region Ctors

        public ArrowGraphBuilder(
            IIdGeneratorFactory<int> edgeIdGeneratorFactory,
            IIdGeneratorFactory<int> nodeIdGeneratorFactory)
            : base(
                  edgeIdGeneratorFactory,
                  nodeIdGeneratorFactory,
                  new DummyActivityGenerator(),
                  new EventGenerator<int>(),
                  new ArrowTarjanStronglyConnectedComponentsFinder<int, int, int, IDependentActivity>(),
                  new ArrowCriticalPathEngine<int, int, int, IDependentActivity>(),
                  new PriorityListResourceScheduler<int, int, int>())
        {
        }

        public ArrowGraphBuilder(
            Graph<int, IDependentActivity, IEvent<int>> graph,
            IIdGeneratorFactory<int> edgeIdGeneratorFactory,
            IIdGeneratorFactory<int> nodeIdGeneratorFactory)
            : base(
                  graph,
                  edgeIdGeneratorFactory,
                  nodeIdGeneratorFactory,
                  new DummyActivityGenerator(),
                  new EventGenerator<int>(),
                  new ArrowTarjanStronglyConnectedComponentsFinder<int, int, int, IDependentActivity>(),
                  new ArrowCriticalPathEngine<int, int, int, IDependentActivity>(),
                  new PriorityListResourceScheduler<int, int, int>())
        {
        }

        #endregion

        // The IDs of the dummy activities and of the nodes count down from -1. When a builder loads a graph, the
        // factories start them under the lowest ID that the graph holds, so the IDs of a clone follow the IDs of its
        // original without a gap.
        public static ArrowGraphBuilder CreateDependentActivityArrowGraphBuilder()
        {
            return new ArrowGraphBuilder(
                new PreviousIdGeneratorFactory<int>(),
                new PreviousIdGeneratorFactory<int>());
        }

        public static ArrowGraphBuilder CreateDependentActivityArrowGraphBuilder(Graph<int, IDependentActivity, IEvent<int>> arrowGraph)
        {
            return new ArrowGraphBuilder(
                arrowGraph,
                new PreviousIdGeneratorFactory<int>(),
                new PreviousIdGeneratorFactory<int>());
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
