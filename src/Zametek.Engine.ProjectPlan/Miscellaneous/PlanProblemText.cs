using System.Globalization;

namespace Zametek.Engine.ProjectPlan
{
    /// <summary>
    /// The words of the problems that the check of a project found before its load, for a host to show: a sentence that says
    /// that the plan was not loaded, and one line for each problem.
    /// </summary>
    public static class PlanProblemText
    {
        /// <summary>
        /// The sentence that opens the message: the plan was not loaded, and how many problems there are.
        /// </summary>
        public static string BuildHeader(int count) =>
            string.Format(CultureInfo.CurrentCulture, Resource.ProjectPlan.Messages.Message_PlanProblemsFound, count);

        /// <summary>
        /// The first problems, one in each line, as the place and the phrase that continues from it
        /// (/Files/0/Scenario/ProjectStart: is required), and how many more there are.
        /// </summary>
        public static IReadOnlyList<string> ListLines(
            IReadOnlyList<PlanProblem> problems,
            int limit = int.MaxValue)
        {
            ArgumentNullException.ThrowIfNull(problems);
            int shownCount = Math.Min(problems.Count, limit);
            List<string> lines = [.. problems.Take(shownCount).Select(x => $@"{x.Pointer}: {x.Detail}")];

            if (problems.Count > shownCount)
            {
                lines.Add(string.Format(CultureInfo.CurrentCulture, Resource.ProjectPlan.Messages.Message_ImportMoreProblems, problems.Count - shownCount));
            }

            return lines;
        }
    }
}
