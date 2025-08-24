using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Contract.ProjectPlan
{
    public interface IDependentActivity
        : IDependentActivity<int, int, int>
    {
        bool HasNoRisk { get; set; }
        List<ActivityTrackerModel> Trackers { get; }
    }
}
