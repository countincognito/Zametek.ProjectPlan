using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ArrowGraphCompiler
        : ArrowGraphCompilerBase<int, int, int, IDependentActivity, IActivity<int, int, int>, IEvent<int>>
    {
        #region Ctors

        protected ArrowGraphCompiler(ArrowGraphBuilderBase<int, int, int, IDependentActivity, IEvent<int>> arrowGraphBuilder)
            : base(arrowGraphBuilder)
        {
        }

        public ArrowGraphCompiler()
            : this(CreateDependentActivityArrowGraphBuilder())
        {
        }

        #endregion

        #region Private Methods

        private static DependentActivityArrowGraphBuilder CreateDependentActivityArrowGraphBuilder()
        {
            int edgeId = default;
            int nodeId = default;
            return new DependentActivityArrowGraphBuilder(
                () => edgeId = edgeId - 1,
                () => nodeId = nodeId - 1);
        }

        #endregion

        #region Private Types

        private class DependentActivityArrowGraphBuilder
            : ArrowGraphBuilderBase<int, int, int, IDependentActivity, IEvent<int>>
        {
            #region Fields

            private static readonly Func<int, IEvent<int>> s_EventGenerator = (id) => new Event<int>(id);
            private static readonly Func<int, int?, int?, IEvent<int>> s_EventGeneratorEventWithTimes = (id, earliestFinishTime, latestFinishTime) => new Event<int>(id, earliestFinishTime, latestFinishTime);
            private static readonly Func<int, IDependentActivity> s_DummyActivityGenerator = (id) => new DependentActivity(id, 0, canBeRemoved: true);

            #endregion

            #region Ctors

            public DependentActivityArrowGraphBuilder(
                Func<int> edgeIdGenerator,
                Func<int> nodeIdGenerator)
                : base(
                      edgeIdGenerator,
                      nodeIdGenerator,
                      s_EventGenerator,
                      s_EventGeneratorEventWithTimes,
                      s_DummyActivityGenerator)
            {
            }

            public DependentActivityArrowGraphBuilder(
                Graph<int, IDependentActivity, IEvent<int>> graph,
                Func<int> edgeIdGenerator,
                Func<int> nodeIdGenerator)
                : base(
                      graph,
                      edgeIdGenerator,
                      nodeIdGenerator,
                      s_EventGenerator)
            {
            }

            #endregion

            #region Overrides

            public override object CloneObject()
            {
                Graph<int, IDependentActivity, IEvent<int>> arrowGraphCopy = ToGraph();

                int minNodeId = arrowGraphCopy.Nodes.Select(x => x.Id).DefaultIfEmpty().Min();
                minNodeId = minNodeId - 1;

                int minEdgeId = arrowGraphCopy.Edges.Select(x => x.Id).DefaultIfEmpty().Min();
                minEdgeId = minEdgeId - 1;

                return new DependentActivityArrowGraphBuilder(
                    arrowGraphCopy,
                    () => minEdgeId = minEdgeId - 1,
                    () => minNodeId = minNodeId - 1);
            }

            #endregion
        }

        #endregion
    }
}
