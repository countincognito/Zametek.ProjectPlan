using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Contract.ProjectPlan
{
    public interface IGraphCompilationService
    {
        ArrowGraphModel BuildArrowGraph(
            IEnumerable<IDependentActivity> dependentActivities);

        VertexGraphModel BuildVertexGraph(
            IEnumerable<IDependentActivity> dependentActivities,
            IEnumerable<ResourceModel> resources,
            bool resourcesAreDisabled,
            IEnumerable<WorkStreamModel> workStreams);
    }
}
