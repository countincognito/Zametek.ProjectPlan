using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Contract.ProjectPlan
{
    public interface IDependentActivity
        : IDependentActivity<int, int, int>
    {
        List<ActivityTrackerModel> Trackers { get; }
    }
}
