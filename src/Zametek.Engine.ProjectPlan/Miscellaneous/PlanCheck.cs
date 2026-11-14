using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Engine.ProjectPlan
{
    /// <summary>
    /// Checks a project before a job loads it (docs/PROJECT-FILE.md, "What zpp checks before the load"). The load cannot get
    /// past some plans: a scenario with no project start, a date that the calendar cannot count to, a choice that does not
    /// exist, or two resources with one id. The load failed with an exception, and the server answered 500. Other plans
    /// loaded, and gave a result that the plan does not say: two activities with one id, or no data for the scenario that the
    /// load loads. The check finds these first. It also counts what the compiler limits, so that a plan out of the limits gets
    /// P0080 without a load that can take seconds.
    /// </summary>
    internal static class PlanCheck
    {
        // The codes of the problems, as the API names them (docs/API.md, "Codes").
        public const string Required = @"required";
        public const string OutOfRange = @"outOfRange";
        public const string NotAllowed = @"notAllowed";
        public const string DuplicateId = @"duplicateId";
        public const string HasNoData = @"hasNoData";
        public const string NotFound = @"notFound";

        // The dates on which a project can start. The calendar counts the days of a schedule from its start, up to the limit
        // of a time, and these dates leave room for them inside the dates of .NET.
        public static readonly DateTime FirstProjectStart = new(1900, 1, 1);
        public static readonly DateTime LastProjectStart = new(2999, 12, 31);

        private const string c_Id = @"Id";

        // How deep the search for choices goes. The models of a plan are trees of a few levels.
        private const int c_MaximumDepth = 32;

        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> s_Properties = new();

        #region Public Members

        /// <summary>
        /// The item of Files that a load of the project loads, as ProjectScenarioManagerViewModel.ProcessProject chooses it:
        /// the item of Current, else the item of the last node. Null when the project has no nodes, which gives a new
        /// scenario, or when Files has no item for the node.
        /// </summary>
        public static int? IndexOfScenarioToLoad(ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);

            if (project.Nodes.Count == 0)
            {
                return null;
            }

            int current = LastIndexOfFile(project, project.Current);

            if (current >= 0)
            {
                return current;
            }

            int last = project.Nodes[^1] is ProjectScenarioNodeModel node ? LastIndexOfFile(project, node.Id) : -1;
            return last >= 0 ? last : null;
        }

        /// <summary>
        /// The problems of the project around its scenarios: a choice of its nodes or of its display settings that does not
        /// exist, and no data for the scenario that the load loads.
        /// </summary>
        public static List<PlanProblem> FindProjectProblems(ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(project);
            var problems = new List<PlanProblem>();

            FindChoices(project.Nodes, $@"/{nameof(ProjectModel.Nodes)}", problems, 0);
            FindChoices(project.Tags, $@"/{nameof(ProjectModel.Tags)}", problems, 0);
            FindChoices(project.DisplaySettings, $@"/{nameof(ProjectModel.DisplaySettings)}", problems, 0);

            if (project.Nodes.Count > 0
                && IndexOfScenarioToLoad(project) is null)
            {
                ProjectScenarioNodeModel? node = project.Nodes.FirstOrDefault(x => x?.Id == project.Current) ?? project.Nodes[^1];
                problems.Add(new PlanProblem(
                    $@"/{nameof(ProjectModel.Files)}",
                    HasNoData,
                    Say(Resource.ProjectPlan.Messages.Message_PlanHasNoData, node?.Name ?? string.Empty)));
            }

            return problems;
        }

        /// <summary>
        /// The problems of the scenario of an item of Files, each with its place under /Files/{index}/Scenario.
        /// </summary>
        public static List<PlanProblem> FindScenarioProblems(ProjectModel project, int fileIndex)
        {
            ArgumentNullException.ThrowIfNull(project);
            string root = $@"/{nameof(ProjectModel.Files)}/{fileIndex}/{nameof(ProjectScenarioFileModel.Scenario)}";
            var problems = new List<PlanProblem>();
            ProjectScenarioModel? scenario = project.Files[fileIndex]?.Scenario;

            if (scenario is null)
            {
                problems.Add(new PlanProblem(root, Required, Resource.ProjectPlan.Messages.Message_ServeErrorRequired));
                return problems;
            }

            // The start, from which the calendar counts each date of the plan.
            string startPlace = $@"{root}/{nameof(ProjectScenarioModel.ProjectStart)}";
            bool hasStart = false;

            if (scenario.ProjectStart == default)
            {
                problems.Add(new PlanProblem(startPlace, Required, Resource.ProjectPlan.Messages.Message_ServeErrorRequired));
            }
            else if (scenario.ProjectStart.Date < FirstProjectStart
                || scenario.ProjectStart.Date > LastProjectStart)
            {
                problems.Add(new PlanProblem(
                    startPlace,
                    OutOfRange,
                    Say(Resource.ProjectPlan.Messages.Message_ServeErrorDurationOutOfRange, Date(FirstProjectStart), Date(LastProjectStart))));
            }
            else
            {
                hasStart = true;
            }

            // The activities: an id of their own, and constraint dates that the calendar can count to.
            List<DependentActivityModel>? activities = scenario.DependentActivities;

            if (activities is not null)
            {
                var ids = new Dictionary<int, int>();

                for (int index = 0; index < activities.Count; index++)
                {
                    string place = $@"{root}/{nameof(ProjectScenarioModel.DependentActivities)}/{index}/{nameof(DependentActivityModel.Activity)}";
                    ActivityModel? activity = activities[index]?.Activity;

                    if (activity is null)
                    {
                        problems.Add(new PlanProblem(place, Required, Resource.ProjectPlan.Messages.Message_ServeErrorRequired));
                        continue;
                    }

                    AddIfDuplicate(ids, activity.Id, index, $@"{place}/{c_Id}", problems);

                    if (hasStart)
                    {
                        AddIfFar(scenario.ProjectStart, activity.MinimumEarliestStartDateTime, $@"{place}/{nameof(ActivityModel.MinimumEarliestStartDateTime)}", problems);
                        AddIfFar(scenario.ProjectStart, activity.MaximumLatestFinishDateTime, $@"{place}/{nameof(ActivityModel.MaximumLatestFinishDateTime)}", problems);
                    }
                }
            }

            // The resources and the work streams: an id of their own.
            AddDuplicates(
                scenario.ResourceSettings?.Resources,
                x => x.Id,
                $@"{root}/{nameof(ProjectScenarioModel.ResourceSettings)}/{nameof(ResourceSettingsModel.Resources)}",
                problems);
            AddDuplicates(
                scenario.WorkStreamSettings?.WorkStreams,
                x => x.Id,
                $@"{root}/{nameof(ProjectScenarioModel.WorkStreamSettings)}/{nameof(WorkStreamSettingsModel.WorkStreams)}",
                problems);

            // The choices.
            FindChoices(scenario, root, problems, 0);

            return problems;
        }

        /// <summary>
        /// The ids of a scenario that match nothing, as warnings with the code notFound: a target resource that no resource has
        /// (the load drops it, and the activity can then go to any resource), and a target work stream or a phase of a resource
        /// that no work stream has. A plan with such an id loads.
        /// </summary>
        public static List<PlanProblem> FindScenarioWarnings(ProjectModel project, int fileIndex)
        {
            ArgumentNullException.ThrowIfNull(project);
            string root = $@"/{nameof(ProjectModel.Files)}/{fileIndex}/{nameof(ProjectScenarioFileModel.Scenario)}";
            var warnings = new List<PlanProblem>();
            ProjectScenarioModel? scenario = project.Files[fileIndex]?.Scenario;

            if (scenario is null)
            {
                return warnings;
            }

            List<ResourceModel> resources = scenario.ResourceSettings?.Resources ?? [];
            HashSet<int> resourceIds = [.. resources.Where(x => x is not null).Select(x => x.Id)];
            HashSet<int> workStreamIds = [.. (scenario.WorkStreamSettings?.WorkStreams ?? []).Where(x => x is not null).Select(x => x.Id)];
            List<DependentActivityModel> activities = scenario.DependentActivities ?? [];

            for (int index = 0; index < activities.Count; index++)
            {
                if (activities[index]?.Activity is ActivityModel activity)
                {
                    string place = $@"{root}/{nameof(ProjectScenarioModel.DependentActivities)}/{index}/{nameof(DependentActivityModel.Activity)}";
                    AddUnknown(activity.TargetResources, resourceIds, $@"{place}/{nameof(ActivityModel.TargetResources)}", Resource.ProjectPlan.Messages.Message_PlanNotAResource, warnings);
                    AddUnknown(activity.TargetWorkStreams, workStreamIds, $@"{place}/{nameof(ActivityModel.TargetWorkStreams)}", Resource.ProjectPlan.Messages.Message_PlanNotAWorkStream, warnings);
                }
            }

            for (int index = 0; index < resources.Count; index++)
            {
                if (resources[index] is ResourceModel resource)
                {
                    string place = $@"{root}/{nameof(ProjectScenarioModel.ResourceSettings)}/{nameof(ResourceSettingsModel.Resources)}/{index}/{nameof(ResourceModel.InterActivityPhases)}";
                    AddUnknown(resource.InterActivityPhases, workStreamIds, place, Resource.ProjectPlan.Messages.Message_PlanNotAWorkStream, warnings);
                }
            }

            return warnings;
        }

        /// <summary>
        /// The counts of a scenario that are above the limits of the compiler, in the words of the compiler (P0080). The
        /// compiler counts the active resources only, and none when the resources are disabled.
        /// </summary>
        public static List<string> FindLimitViolations(ProjectScenarioModel scenario)
        {
            ArgumentNullException.ThrowIfNull(scenario);
            var violations = new List<string>();
            ResourceSettingsModel? resources = scenario.ResourceSettings;
            int resourceCount = resources is null || resources.AreDisabled
                ? 0
                : resources.Resources?.Count(x => x is not null && !x.IsInactive) ?? 0;

            AddCount(violations, Resource.ProjectPlan.Messages.Message_PlanActivities, scenario.DependentActivities?.Count ?? 0, GraphLimits.MaximumActivityCount);
            AddCount(violations, Resource.ProjectPlan.Messages.Message_PlanResources, resourceCount, GraphLimits.MaximumResourceCount);
            AddCount(violations, Resource.ProjectPlan.Messages.Message_PlanWorkStreams, scenario.WorkStreamSettings?.WorkStreams?.Count ?? 0, GraphLimits.MaximumWorkStreamCount);

            return violations;
        }

        /// <summary>
        /// The message of P0080 for counts above the limits, as the compiler gives it.
        /// </summary>
        public static string BuildLimitsMessage(IReadOnlyList<string> violations)
        {
            ArgumentNullException.ThrowIfNull(violations);
            var output = new StringBuilder();
            output.AppendLine(Resource.ProjectPlan.Messages.Message_PlanLimitsExceeded);

            foreach (string violation in violations)
            {
                output.AppendLine(violation);
            }

            return output.ToString();
        }

        #endregion

        #region Private Members

        private static int LastIndexOfFile(ProjectModel project, Guid nodeId)
        {
            // The load keeps the last item of a node, when Files has more than one.
            for (int index = project.Files.Count - 1; index >= 0; index--)
            {
                if (project.Files[index] is ProjectScenarioFileModel file
                    && file.NodeId == nodeId)
                {
                    return index;
                }
            }

            return -1;
        }

        private static void AddIfDuplicate(
            Dictionary<int, int> ids,
            int id,
            int index,
            string place,
            List<PlanProblem> problems)
        {
            if (ids.TryGetValue(id, out int first))
            {
                problems.Add(new PlanProblem(place, DuplicateId, Say(Resource.ProjectPlan.Messages.Message_PlanDuplicateId, first)));
            }
            else
            {
                ids[id] = index;
            }
        }

        private static void AddDuplicates<T>(
            List<T>? items,
            Func<T, int> idOf,
            string place,
            List<PlanProblem> problems)
            where T : class
        {
            if (items is null)
            {
                return;
            }

            var ids = new Dictionary<int, int>();

            for (int index = 0; index < items.Count; index++)
            {
                if (items[index] is T item)
                {
                    AddIfDuplicate(ids, idOf(item), index, $@"{place}/{index}/{c_Id}", problems);
                }
            }
        }

        private static void AddUnknown(
            List<int>? ids,
            HashSet<int> known,
            string place,
            string detail,
            List<PlanProblem> warnings)
        {
            if (ids is null)
            {
                return;
            }

            for (int index = 0; index < ids.Count; index++)
            {
                if (!known.Contains(ids[index]))
                {
                    warnings.Add(new PlanProblem($@"{place}/{index}", NotFound, detail));
                }
            }
        }

        private static void AddIfFar(
            DateTimeOffset start,
            DateTimeOffset? date,
            string place,
            List<PlanProblem> problems)
        {
            // A date further from the start than the limit of a time cannot be a time of the plan, and the calendar would
            // count to it day by day, or past the end of the dates of .NET.
            if (date is DateTimeOffset value
                && Math.Abs((value.Date - start.Date).TotalDays) > GraphLimits.MaximumTimeValue)
            {
                problems.Add(new PlanProblem(
                    place,
                    OutOfRange,
                    Say(Resource.ProjectPlan.Messages.Message_PlanDateTooFarFromStart, GraphLimits.MaximumTimeValue)));
            }
        }

        private static void AddCount(
            List<string> violations,
            string items,
            int count,
            int maximum)
        {
            if (count > maximum)
            {
                violations.Add(Say(Resource.ProjectPlan.Messages.Message_PlanCountExceedsMaximum, items, count, maximum));
            }
        }

        // Each value of an enumeration in the model, at any depth, that is not a value of the enumeration. Newtonsoft.Json
        // reads any number into an enumeration, and the load then fails, or does what no value says.
        private static void FindChoices(
            object? value,
            string place,
            List<PlanProblem> problems,
            int depth)
        {
            if (value is null
                || depth > c_MaximumDepth)
            {
                return;
            }

            Type type = value.GetType();

            if (type.IsEnum)
            {
                if (!IsChoice(type, value))
                {
                    problems.Add(new PlanProblem(place, NotAllowed, Say(Resource.ProjectPlan.Messages.Message_ServeErrorMustBeOneOf, Choices(type))));
                }

                return;
            }

            if (value is string
                || type.IsValueType)
            {
                return;
            }

            if (value is IEnumerable items)
            {
                int index = 0;

                foreach (object? item in items)
                {
                    FindChoices(item, $@"{place}/{index}", problems, depth + 1);
                    index++;
                }

                return;
            }

            if (type.Namespace?.StartsWith(@"Zametek.", StringComparison.Ordinal) != true)
            {
                return;
            }

            foreach (PropertyInfo property in s_Properties.GetOrAdd(type, Readable))
            {
                FindChoices(property.GetValue(value), $@"{place}/{property.Name}", problems, depth + 1);
            }
        }

        private static PropertyInfo[] Readable(Type type) =>
            [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => x.GetMethod is not null && x.GetIndexParameters().Length == 0)];

        private static bool IsChoice(Type type, object value)
        {
            if (!type.IsDefined(typeof(FlagsAttribute), inherit: false))
            {
                return Enum.IsDefined(type, value);
            }

            ulong all = 0;

            foreach (object defined in Enum.GetValues(type))
            {
                all |= Bits(defined);
            }

            return (Bits(value) & ~all) == 0;
        }

        private static ulong Bits(object value) => unchecked((ulong)Convert.ToInt64(value, CultureInfo.InvariantCulture));

        // The values of an enumeration as a plan writes them, with their names: 0 (None), 1 (Weekends), 2 (CustomCalendar).
        private static string Choices(Type type) =>
            string.Join(
                @", ",
                Enum.GetValues(type).Cast<object>().Select(x => $@"{Convert.ToInt64(x, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)} ({x})"));

        private static string Date(DateTime date) => date.ToString(@"yyyy-MM-dd", CultureInfo.InvariantCulture);

        private static string Say(string template, params object[] arguments) =>
            string.Format(CultureInfo.CurrentCulture, template, arguments);

        #endregion
    }
}
