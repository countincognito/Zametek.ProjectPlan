namespace Zametek.Common.ProjectPlan
{
    /// <summary>
    /// One line of the statistics that the simulation panel shows: a name and its value, as text.
    /// </summary>
    [Serializable]
    public record SimulationStatisticModel
    {
        public string Name { get; init; } = string.Empty;

        public string Value { get; init; } = string.Empty;
    }
}
