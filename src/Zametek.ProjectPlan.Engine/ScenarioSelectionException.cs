namespace Zametek.ProjectPlan.Engine
{
    /// <summary>
    /// Thrown when a job cannot select the scenario its request named. The message says what went wrong in the
    /// engine's own words; the selector, the reason and the number of matches are here as well, so that a host can say
    /// it in its own - zpp, for one, points at its --list-scenarios option.
    /// </summary>
    public class ScenarioSelectionException
        : Exception
    {
        public ScenarioSelectionException()
        {
        }

        public ScenarioSelectionException(string message)
            : base(message)
        {
        }

        public ScenarioSelectionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public ScenarioSelectionException(
            string message,
            string selector,
            ScenarioSelectionFailure failure,
            int matchCount)
            : base(message)
        {
            Selector = selector;
            Failure = failure;
            MatchCount = matchCount;
        }

        /// <summary>
        /// The name, id or id prefix the request gave.
        /// </summary>
        public string Selector { get; init; } = string.Empty;

        public ScenarioSelectionFailure Failure { get; init; }

        /// <summary>
        /// How many scenarios the selector matched.
        /// </summary>
        public int MatchCount { get; init; }
    }
}
