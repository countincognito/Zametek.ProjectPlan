using Zametek.Common.ProjectPlan;

namespace Zametek.Engine.ProjectPlan
{
    // Finds a project's scenarios the way a person names them: by name, by id, or by the start of an id.
    internal static class ScenarioSelector
    {
        // Git's abbreviation floor: an id prefix shorter than this is never
        // treated as an id, it just falls through to the not-found error.
        private const int c_MinimumScenarioIdPrefixLength = 4;

        // An id prefix is matched against the id's 32 hex digits, without the
        // hyphens that separate their groups in the listing.
        private const string c_ScenarioIdDigitsFormat = @"N";
        private const string c_ScenarioIdGroupSeparator = @"-";

        public static Guid ResolveScenarioId(
            ProjectModel projectModel,
            string selector)
        {
            List<ProjectScenarioNodeModel> scenarios = [.. projectModel.Nodes.Where(x => x.NodeType == ProjectScenarioNodeType.File)];

            List<ProjectScenarioNodeModel> matches;

            if (Guid.TryParse(selector, out Guid id))
            {
                matches = [.. scenarios.Where(x => x.Id == id)];
            }
            else
            {
                // Exact (case-insensitive) name matches win over id prefixes, the
                // same way git resolves a ref before an abbreviated object id.
                matches = [.. scenarios.Where(x => string.Equals(x.Name, selector, StringComparison.OrdinalIgnoreCase))];

                if (matches.Count == 0)
                {
                    matches = [.. MatchScenarioIdPrefix(scenarios, selector)];
                }
            }

            if (matches.Count == 0)
            {
                throw new ScenarioSelectionException(
                    string.Format(Resource.ProjectPlan.Messages.Message_NoScenarioMatchesSelector, selector),
                    selector,
                    ScenarioSelectionFailure.NoMatch,
                    matches.Count);
            }
            if (matches.Count > 1)
            {
                throw new ScenarioSelectionException(
                    string.Format(Resource.ProjectPlan.Messages.Message_SeveralScenariosMatchSelector, selector, matches.Count),
                    selector,
                    ScenarioSelectionFailure.SeveralMatches,
                    matches.Count);
            }

            Guid scenarioId = matches[0].Id;

            if (!projectModel.Files.Any(x => x.NodeId == scenarioId))
            {
                throw new ScenarioSelectionException(
                    string.Format(Resource.ProjectPlan.Messages.Message_ScenarioHasNoScenarioData, selector),
                    selector,
                    ScenarioSelectionFailure.NoScenarioData,
                    matches.Count);
            }

            return scenarioId;
        }

        public static IReadOnlyList<ScenarioSummary> ListScenarios(ProjectModel projectModel)
        {
            Dictionary<Guid, ProjectScenarioNodeModel> nodeLookup = projectModel.Nodes.ToDictionary(x => x.Id);

            return [.. projectModel.Nodes
                .Where(x => x.NodeType == ProjectScenarioNodeType.File)
                .Select(x => new ScenarioSummary(
                    BuildNodePath(projectModel, nodeLookup, x),
                    x.Id,
                    x.IsTracked,
                    x.Id == projectModel.Current))];
        }

        public static string BuildNodePath(
            ProjectModel projectModel,
            IReadOnlyDictionary<Guid, ProjectScenarioNodeModel> nodeLookup,
            ProjectScenarioNodeModel node)
        {
            // Folder names are prefixed so that scenarios with the same name in
            // different folders stay distinguishable in the listing. The visited
            // set guards against a malformed file with a parent cycle.
            var names = new List<string> { node.Name };
            var visited = new HashSet<Guid> { node.Id };
            Guid parentId = node.ParentId;

            while (parentId != projectModel.Root
                && visited.Add(parentId)
                && nodeLookup.TryGetValue(parentId, out ProjectScenarioNodeModel? parent))
            {
                names.Insert(0, parent.Name);
                parentId = parent.ParentId;
            }

            return string.Join(Resource.ProjectPlan.Symbols.Symbol_PathSeparator, names);
        }

        private static IEnumerable<ProjectScenarioNodeModel> MatchScenarioIdPrefix(
            IEnumerable<ProjectScenarioNodeModel> scenarios,
            string selector)
        {
            // Git-style abbreviation: hyphens are ignored and hex digits are
            // matched case-insensitively against the start of the id, so any
            // portion copied out of a scenario listing works. The caller treats
            // multiple matches as ambiguous, so a prefix resolves only when it is
            // long enough to be unique.
            string prefix = selector.Replace(c_ScenarioIdGroupSeparator, string.Empty).ToLowerInvariant();

            if (prefix.Length < c_MinimumScenarioIdPrefixLength
                || !prefix.All(char.IsAsciiHexDigit))
            {
                return [];
            }

            return scenarios.Where(x => x.Id.ToString(c_ScenarioIdDigitsFormat).StartsWith(prefix, StringComparison.Ordinal));
        }
    }
}
