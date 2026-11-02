using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// What the outputs of a cascade read, copied in one step so that all of them are built
    /// from the same moment. The compilation and the settings are shared, not copied, because
    /// they are replaced as a whole and never changed in place.
    /// </summary>
    internal sealed record CascadeInputs(
        IGraphCompilation<int, int, int, IDependentActivity> GraphCompilation,
        bool HasCompilationErrors,
        ResourceSettingsModel ResourceSettings,
        WorkStreamSettingsModel WorkStreamSettings,
        IReadOnlyList<ActivitySeverityModel> ActivitySeverities,
        DateTimeOffset ProjectStart,
        int? StartTime,
        int? FinishTime,
        bool HasResources,
        IList<ActivityModel> ActivityModels,
        int ScenarioRevision);
}
