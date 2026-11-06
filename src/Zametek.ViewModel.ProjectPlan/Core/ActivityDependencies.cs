using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// The dependencies that the user entered for an activity: those that it waits for, and those of the planning kind. The
    /// dependencies that a resource gives are not here, because a compilation makes them again.
    /// </summary>
    /// <param name="Dependencies">The activities that this one waits for.</param>
    /// <param name="PlanningDependencies">The activities that this one waits for in the plan, and that a compilation does not add to.</param>
    internal sealed record ActivityDependencies(
        HashSet<int> Dependencies,
        HashSet<int> PlanningDependencies)
    {
        /// <summary>
        /// Takes the dependencies that an activity holds now, as a copy that nothing else changes.
        /// </summary>
        public static ActivityDependencies Of(IDependentActivity activity) =>
            new([.. activity.Dependencies], [.. activity.PlanningDependencies]);

        /// <summary>
        /// Whether the two hold the same dependencies. A record compares its sets by reference, and so this does not use
        /// the equality of the record.
        /// </summary>
        public bool IsSameAs(ActivityDependencies other)
        {
            ArgumentNullException.ThrowIfNull(other);

            return Dependencies.SetEquals(other.Dependencies)
                && PlanningDependencies.SetEquals(other.PlanningDependencies);
        }
    }
}
