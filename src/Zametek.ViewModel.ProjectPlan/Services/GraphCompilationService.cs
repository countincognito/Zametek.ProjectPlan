using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    public class GraphCompilationService
        : IGraphCompilationService
    {
        #region Fields

        private readonly ProjectPlanMapper m_Mapper;

        #endregion

        #region Ctors

        public GraphCompilationService(ProjectPlanMapper mapper)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            m_Mapper = mapper;
        }

        #endregion

        #region IGraphCompilationService Members

        public ArrowGraphModel BuildArrowGraph(
            IEnumerable<IDependentActivity> dependentActivities)
        {
            ArgumentNullException.ThrowIfNull(dependentActivities);

            IEnumerable<IDependentActivity> dependentActivitiesCopy =
                dependentActivities.Select(x => (IDependentActivity)x.CloneObject());

            if (!dependentActivitiesCopy.Any())
            {
                return new ArrowGraphModel();
            }

            var arrowGraphCompiler = new ArrowGraphCompiler();
            foreach (IDependentActivity dependentActivity in dependentActivitiesCopy)
            {
                dependentActivity.Dependencies.UnionWith(dependentActivity.ResourceDependencies);
                dependentActivity.ResourceDependencies.Clear();
                arrowGraphCompiler.AddActivity(dependentActivity);
            }

            arrowGraphCompiler.Compile();
            Graph<int, IDependentActivity, IEvent<int>>? arrowGraph =
                arrowGraphCompiler.ToGraph() ?? throw new InvalidOperationException(Resource.ProjectPlan.Messages.Message_CannotBuildArrowGraph);
            return m_Mapper.ToArrowGraphModel(arrowGraph);
        }

        public VertexGraphModel BuildVertexGraph(
            IEnumerable<IDependentActivity> dependentActivities,
            IEnumerable<ResourceModel> resources,
            bool resourcesAreDisabled,
            IEnumerable<WorkStreamModel> workStreams)
        {
            ArgumentNullException.ThrowIfNull(dependentActivities);
            ArgumentNullException.ThrowIfNull(resources);
            ArgumentNullException.ThrowIfNull(workStreams);

            IEnumerable<IDependentActivity> dependentActivitiesCopy =
                dependentActivities.Select(x => (IDependentActivity)x.CloneObject());

            if (!dependentActivitiesCopy.Any())
            {
                return new VertexGraphModel();
            }

            var availableResources = new List<IResource<int, int>>();
            if (!resourcesAreDisabled)
            {
                availableResources.AddRange(resources.OrderBy(x => x.Id).Select(m_Mapper.ToResource));
            }

            List<IWorkStream<int>> workStreamList =
                [.. workStreams.Select(m_Mapper.ToWorkStream)];

            var vertexGraphCompiler = new VertexGraphCompiler();
            foreach (IDependentActivity dependentActivity in dependentActivitiesCopy)
            {
                dependentActivity.Dependencies.UnionWith(dependentActivity.ResourceDependencies);
                dependentActivity.ResourceDependencies.Clear();
                vertexGraphCompiler.AddActivity(dependentActivity);
            }

            vertexGraphCompiler.TransitiveReduction();
            vertexGraphCompiler.Compile(availableResources, workStreamList);

            Graph<int, IEvent<int>, IDependentActivity>? vertexGraph =
                vertexGraphCompiler.ToGraph() ?? throw new InvalidOperationException(Resource.ProjectPlan.Messages.Message_CannotBuildArrowGraph);
            return m_Mapper.ToVertexGraphModel(vertexGraph);
        }

        #endregion
    }
}
