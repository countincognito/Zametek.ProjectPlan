using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// Builds the arrow graph and the vertex graph of a plan from the activities that its compilation gave.
    /// </summary>
    /// <remarks>
    /// The two graphs show the result of the compilation of the plan, and that compilation scheduled the resources.
    /// Thus, neither build schedules a resource. Each build takes copies of the compiled activities, makes each dependency
    /// that a resource gave a dependency of the copy, and calculates the critical path of those links. The same links
    /// give the same times. So the two graphs agree with each other, and with the list of the activities.
    /// </remarks>
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

            // The arrow graph is built from an already compiled plan, but the arrow graph
            // compiler runs a compilation of its own (it schedules no resource), so it gets
            // the same watchdog as the main compile - see CompilationTimeoutHelper.
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

        /// <summary>
        /// Builds the vertex graph of a compiled plan. Each node is an activity, with the times that the compilation of
        /// the plan gave it. Each link is a dependency, and a dependency that a resource gave is a link also.
        /// </summary>
        /// <remarks>
        /// The method takes the compiled activities and nothing else. It does not take the resources, because it must not
        /// schedule them a second time. The comment in the method says why.
        /// </remarks>
        /// <param name="dependentActivities">The activities as the compilation of the plan gave them: with their times, their resources and their resource dependencies.</param>
        public VertexGraphModel BuildVertexGraph(
            IEnumerable<IDependentActivity> dependentActivities)
        {
            ArgumentNullException.ThrowIfNull(dependentActivities);

            IEnumerable<IDependentActivity> dependentActivitiesCopy =
                dependentActivities.Select(x => (IDependentActivity)x.CloneObject());

            if (!dependentActivitiesCopy.Any())
            {
                return new VertexGraphModel();
            }

            var vertexGraphCompiler = new VertexGraphCompiler();
            foreach (IDependentActivity dependentActivity in dependentActivitiesCopy)
            {
                // A resource dependency says that this activity follows another activity on the same resource. The
                // compilation of the plan gave it, together with the schedule of the resources. The graph draws it as
                // a link, in the same way as a dependency that the user gave. Thus, it becomes a dependency of the copy.
                dependentActivity.Dependencies.UnionWith(dependentActivity.ResourceDependencies);
                dependentActivity.ResourceDependencies.Clear();
                vertexGraphCompiler.AddActivity(dependentActivity);
            }

            // Remove each link that other links imply, so that the graph draws no more lines than are necessary. This
            // changes no time.
            vertexGraphCompiler.TransitiveReduction();

            // Calculate the times from the links, and schedule no resource. Do not call Compile here.
            //
            // The links of this graph hold the schedule of the plan already: the resource dependencies give the order of
            // the activities on each resource. The critical path of these links gives each activity the same times as
            // the compilation of the plan gave it.
            //
            // Compile does more. It schedules the resources, and the scheduler takes the activities in the order of their
            // slack. Here the resource dependencies are dependencies, and so the slack of a copy is not the slack that
            // the activity had when the plan was scheduled. Thus, the scheduler can put the copies on the resources in a
            // different order. The result is a correct schedule of the copies, but it is not the schedule of the plan: it
            // has other times, and it can finish later. This method called Compile with the resources from 0.9.3 to
            // 0.11.0-beta.1. For some plans, the vertex graph then showed other times than the list of the activities
            // and the arrow graph showed.
            //
            // Compile with no resources is not the answer. The compiler then puts the copies on lanes of its own, and
            // each copy gets the number of its lane in place of the resources that the plan gave it. Up to version 4.1.1
            // of the library, a copy also got a dependency on the copy before it on its lane, and such a dependency could
            // lower the slack of an activity. This method called Compile with no resources before 0.9.3.
            //
            // The calculation is fast (two passes along the links), and it takes no cancellation token. Thus, it has no
            // watchdog, and BuildArrowGraph has one.
            vertexGraphCompiler.CalculateCriticalPath();

            Graph<int, IEvent<int>, IDependentActivity>? vertexGraph =
                vertexGraphCompiler.ToGraph() ?? throw new InvalidOperationException(Resource.ProjectPlan.Messages.Message_CannotBuildVertexGraph);
            return m_Mapper.ToVertexGraphModel(vertexGraph);
        }

        #endregion
    }
}
