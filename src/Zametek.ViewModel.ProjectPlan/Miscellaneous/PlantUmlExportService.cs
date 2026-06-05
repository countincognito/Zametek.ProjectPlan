using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public static class PlantUmlExportService
    {
        private const string EmptyDiagram =
            "@startuml\ntop to bottom direction\nskinparam nodesep 25\nskinparam ranksep 45\ncircle \"Project Start\" as start #Black\ncircle \"Project Finish\" as end #Black\nstart --> end\n@enduml";

        public static string GeneratePlantUml(
            IReadOnlyList<DependentActivityModel> dependentActivities,
            WorkStreamSettingsModel? workStreamSettings = null)
        {
            if (dependentActivities is null || dependentActivities.Count == 0)
            {
                return EmptyDiagram;
            }

            Dictionary<int, DependentActivityModel> activitiesById =
                dependentActivities.ToDictionary(a => a.Activity.Id);

            List<int> sortedIds = TopologicalSort(dependentActivities);

            HashSet<int> noIncomingIds = BuildNoIncomingIds(dependentActivities);
            HashSet<int> noOutgoingIds = BuildNoOutgoingIds(dependentActivities);

            Dictionary<int, ColorFormatModel>? workStreamColors = null;
            if (workStreamSettings is not null)
            {
                workStreamColors = workStreamSettings.WorkStreams
                    .ToDictionary(ws => ws.Id, ws => ws.ColorFormat);
            }

            return BuildDiagram(activitiesById, sortedIds, noIncomingIds, noOutgoingIds, workStreamColors);
        }

        private static List<int> TopologicalSort(IReadOnlyList<DependentActivityModel> dependentActivities)
        {
            Dictionary<int, int> inDegree =
                dependentActivities.ToDictionary(a => a.Activity.Id, _ => 0);
            Dictionary<int, List<int>> successors =
                dependentActivities.ToDictionary(a => a.Activity.Id, _ => new List<int>());

            foreach (DependentActivityModel activity in dependentActivities)
            {
                foreach (int dependencyId in activity.Dependencies)
                {
                    if (successors.ContainsKey(dependencyId))
                    {
                        successors[dependencyId].Add(activity.Activity.Id);
                    }
                    if (inDegree.ContainsKey(activity.Activity.Id))
                    {
                        inDegree[activity.Activity.Id]++;
                    }
                }
            }

            Queue<int> queue = new Queue<int>();
            foreach (int id in inDegree.Keys.OrderBy(x => x))
            {
                if (inDegree[id] == 0)
                {
                    queue.Enqueue(id);
                }
            }

            List<int> sortedIds = new List<int>();
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                sortedIds.Add(current);
                foreach (int successorId in successors[current].OrderBy(x => x))
                {
                    inDegree[successorId]--;
                    if (inDegree[successorId] == 0)
                    {
                        queue.Enqueue(successorId);
                    }
                }
            }

            return sortedIds;
        }

        private static HashSet<int> BuildNoIncomingIds(IReadOnlyList<DependentActivityModel> dependentActivities)
        {
            return new HashSet<int>(
                dependentActivities
                    .Where(a => a.Dependencies.Count == 0)
                    .Select(a => a.Activity.Id));
        }

        private static HashSet<int> BuildNoOutgoingIds(IReadOnlyList<DependentActivityModel> dependentActivities)
        {
            HashSet<int> allIds =
                new HashSet<int>(dependentActivities.Select(a => a.Activity.Id));
            HashSet<int> idsReferencedAsDependency =
                new HashSet<int>(dependentActivities.SelectMany(a => a.Dependencies));

            return new HashSet<int>(allIds.Where(id => !idsReferencedAsDependency.Contains(id)));
        }

        private static string BuildDiagram(
            Dictionary<int, DependentActivityModel> activitiesById,
            List<int> sortedIds,
            HashSet<int> noIncomingIds,
            HashSet<int> noOutgoingIds,
            Dictionary<int, ColorFormatModel>? workStreamColors)
        {
            var sb = new StringBuilder();
            sb.Append("@startuml\n");
            sb.Append("top to bottom direction\n");
            sb.Append("skinparam nodesep 25\n");
            sb.Append("skinparam ranksep 45\n");
            sb.Append("circle \"Project Finish\" as start #Black\n");
            sb.Append("circle \"Project Start\" as end #Black\n");

            AppendActivityDeclarations(sb, activitiesById, sortedIds, workStreamColors);
            AppendDependencyArrows(sb, activitiesById, sortedIds);
            AppendStartArrows(sb, sortedIds, noOutgoingIds);
            AppendEndArrows(sb, sortedIds, noIncomingIds);

            sb.Append("@enduml");
            return sb.ToString();
        }

        private static string? ResolveColor(
            ActivityModel activity,
            int duration,
            Dictionary<int, ColorFormatModel>? workStreamColors)
        {
            if (duration == 0)
            {
                return "#cccccc";
            }

            string name = activity.Name;
            if (name.Contains("Engine", StringComparison.OrdinalIgnoreCase)) return "#ffca08";
            if (name.Contains("Resource", StringComparison.OrdinalIgnoreCase)) return "#73cee1";
            if (name.Contains("Access", StringComparison.OrdinalIgnoreCase)) return "#bcbdc1";
            if (name.Contains("Utility", StringComparison.OrdinalIgnoreCase)) return "#de9dc7";
            if (name.Contains("Manager", StringComparison.OrdinalIgnoreCase)) return "#fef200";
            if (name.Contains("Gateway", StringComparison.OrdinalIgnoreCase)) return "#fa5252";
            if (name.Contains("Client", StringComparison.OrdinalIgnoreCase) || name.Contains("UI", StringComparison.OrdinalIgnoreCase)) return "#a6ce39";

            if (workStreamColors is null || activity.TargetWorkStreams.Count == 0)
            {
                return null;
            }

            int firstId = activity.TargetWorkStreams[0];
            if (!workStreamColors.TryGetValue(firstId, out ColorFormatModel? colorFormat))
            {
                return null;
            }

            return $"#{colorFormat.R:x2}{colorFormat.G:x2}{colorFormat.B:x2}";
        }

        private static void AppendActivityDeclarations(
            StringBuilder sb,
            Dictionary<int, DependentActivityModel> activitiesById,
            List<int> sortedIds,
            Dictionary<int, ColorFormatModel>? workStreamColors)
        {
            foreach (int id in sortedIds)
            {
                DependentActivityModel activity = activitiesById[id];
                string name = activity.Activity.Name.Replace("\"", "\\\"");
                int duration = activity.Activity.Duration;
                string? color = ResolveColor(activity.Activity, duration, workStreamColors);
                string colorSuffix = color is not null ? $" {color}" : string.Empty;

                if (duration == 0)
                {
                    sb.Append($"circle \"{name}\" as ID{id}{colorSuffix}\n");
                }
                else
                {
                    sb.Append($"rectangle \"{name}\\n({duration}d)\" as ID{id}{colorSuffix}\n");
                }
            }
        }

        private static void AppendDependencyArrows(
            StringBuilder sb,
            Dictionary<int, DependentActivityModel> activitiesById,
            List<int> sortedIds)
        {
            foreach (int id in sortedIds)
            {
                DependentActivityModel activity = activitiesById[id];
                foreach (int dependenciesId in activity.Dependencies)
                {
                    sb.Append($"ID{id} --> ID{dependenciesId}\n");
                }
            }
        }

        private static void AppendStartArrows(
            StringBuilder sb,
            List<int> sortedIds,
            HashSet<int> noIncomingIds)
        {
            foreach (int id in sortedIds.Where(id => noIncomingIds.Contains(id)))
            {
                sb.Append($"start --> ID{id}\n");
            }
        }

        private static void AppendEndArrows(
            StringBuilder sb,
            List<int> sortedIds,
            HashSet<int> noOutgoingIds)
        {
            foreach (int id in sortedIds.Where(id => noOutgoingIds.Contains(id)))
            {
                sb.Append($"ID{id} --> end\n");
            }
        }
    }
}
