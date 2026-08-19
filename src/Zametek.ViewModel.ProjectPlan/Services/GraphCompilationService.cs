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
        private readonly ISettingService m_SettingService;

        #endregion

        #region Ctors

        public GraphCompilationService(
            ProjectPlanMapper mapper,
            ISettingService settingService)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(settingService);
            m_Mapper = mapper;
            m_SettingService = settingService;
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

            // These display graphs are built from an already compiled plan, but the
            // build runs its own compilation, so it gets the same watchdog as the
            // main compile - see CompilationTimeoutHelper.
            int timeoutMilliseconds = m_SettingService.CompilationTimeoutMilliseconds;
            using (CancellationTokenSource? timeoutSource = CompilationTimeoutHelper.CreateTimeoutSource(timeoutMilliseconds))
            {
                try
                {
                    arrowGraphCompiler.Compile(CompilationTimeoutHelper.TokenOrNone(timeoutSource));
                }
                catch (OperationCanceledException ex) when (timeoutSource is not null && timeoutSource.IsCancellationRequested)
                {
                    throw CompilationTimeoutHelper.TimedOut(timeoutMilliseconds, ex);
                }
            }

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

            int timeoutMilliseconds = m_SettingService.CompilationTimeoutMilliseconds;
            using (CancellationTokenSource? timeoutSource = CompilationTimeoutHelper.CreateTimeoutSource(timeoutMilliseconds))
            {
                try
                {
                    vertexGraphCompiler.Compile(availableResources, workStreamList, CompilationTimeoutHelper.TokenOrNone(timeoutSource));
                }
                catch (OperationCanceledException ex) when (timeoutSource is not null && timeoutSource.IsCancellationRequested)
                {
                    throw CompilationTimeoutHelper.TimedOut(timeoutMilliseconds, ex);
                }
            }

            Graph<int, IEvent<int>, IDependentActivity>? vertexGraph =
                vertexGraphCompiler.ToGraph() ?? throw new InvalidOperationException(Resource.ProjectPlan.Messages.Message_CannotBuildArrowGraph);
            return m_Mapper.ToVertexGraphModel(vertexGraph);
        }

        #endregion
    }
}
