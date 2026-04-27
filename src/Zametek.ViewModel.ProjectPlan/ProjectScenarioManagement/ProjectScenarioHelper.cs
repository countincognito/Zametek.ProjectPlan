using System.Diagnostics;
using Zametek.Common.ProjectPlan;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public static class ProjectScenarioHelper
    {
        private static readonly IComparer<IdMap> s_IdMapComparer =
            new LambdaComparer<IdMap>((x, y) => x.FromId.CompareTo(y.FromId));

        private class IdMap
        {
            public int FromId { get; set; }
            public int ToId { get; set; }
            public bool Locked { get; set; } = false;
        }

        // For each ID update, move the existing IDs around so that enough
        // space exists for the new IDs. But with the minimum changes necessary
        // to achieve the final IDs specified in the ID updates.
        //
        // Make sure to preserve the relative ordering of the old IDs and ensure
        // that the updates from old to new IDs remains as requested (i.e. they 
        // are not changed when the existing IDs are moved around to make space
        // for the new IDs).
        //
        // Any of the original IDs that are being updated
        // should be locked in place so that they are not moved around when
        // making space for the new IDs.
        //
        // For example, ten old IDs numbered 1 to 10. Old ID 3 is mapped to new
        // ID 4; old ID 4 is mapped to new ID 9; and old ID 5 is mapped to new
        // ID 8. Whatever happens, those new mappings must be preserved.
        //
        // First map old ID 4 to new ID 9, which requires moving old ID 9 to
        // new ID 10 and old ID 10 to new ID 11. Then map old ID 5 to new ID 8.
        // But since new ID 9 needs to be preserved, this requires mapping old ID
        // 8 to new ID 10. However, with this change, old ID 9 should now be mapped
        // to new ID 11 instead of new ID 10, and old ID 10 should now be mapped
        // to new ID 12 instead of new ID 11. Finally, map old ID 3 to new ID 4,
        // which requires no changes to the other mappings since the old ID 4 is
        // be updated to new ID 9.
        public static List<(int FromId, int ToId)> UpdateIds(
            List<int> originalIds,
            List<(int FromId, int ToId)> idUpdates)
        {
            ArgumentNullException.ThrowIfNull(originalIds);
            ArgumentNullException.ThrowIfNull(idUpdates);
            CheckIds(originalIds);
            CheckIds(idUpdates);

            Dictionary<int, int> idUpdateLookup = idUpdates.ToDictionary(x => x.FromId, x => x.ToId);

            // Check all the old IDs in the ID updates exist in the original IDs.
            foreach (var (fromId, targetToId) in idUpdates)
            {
                Debug.Assert(originalIds.Contains(fromId));
            }

            List<int> mapIds = [.. originalIds.Union(idUpdates.Select(x => x.FromId)).Distinct().Order()];
            List<IdMap> maps = [];

            // Mark a mapping as locked if the old ID is being updated to a new ID.
            // This means that when an old ID is being updated to a new ID, the
            // mapping for that old ID should be locked in place and not changed by
            // subsequent mappings.
            foreach (int id in mapIds)
            {
                int fromId = id;
                int toId = id;
                bool locked = false;
                if (idUpdateLookup.TryGetValue(id, out int mappedId))
                {
                    toId = mappedId;
                    locked = true;
                }
                maps.Add(new IdMap { FromId = fromId, ToId = toId, Locked = locked });
            }

            // Here we have to apply the mappings and see what the final mappings look
            // like after all the necessary changes are made. This includes moving the
            // existing IDs around to make space for the new IDs, while preserving the
            // relative ordering of the old IDs and ensuring that the updates from old
            // to new IDs remains as requested.
            ApplyMaps(maps);

            return [.. maps.OrderBy(m => m.FromId).Select(x => (x.FromId, x.ToId))];
        }

        private static void ApplyMaps(List<IdMap> maps)
        {
            // Make sure the mappings are ordered by the old IDs.
            maps.Sort(s_IdMapComparer);

            // Find the locked mappings and order them from the highest new ID to the lowest new ID.
            List<IdMap> lockedMaps = [.. maps.Where(m => m.Locked).OrderByDescending(m => m.ToId)];

            HashSet<int> lockedTargetToIds = [.. lockedMaps.Select(m => m.ToId)];
            int mapSize = maps.Count;

            foreach (IdMap lockedMap in lockedMaps)
            {
                // If the old ID is being mapped to the same new ID,
                // then there is no need to move any of the other IDs
                // around.
                if (lockedMap.FromId == lockedMap.ToId)
                {
                    continue;
                }

                // Find the index of the target mapping in the maps list.
                int targetIndex = maps.FindIndex(m => m.FromId == lockedMap.ToId);

                // If the targetIndex is -1, then the new ID that the old ID is being
                // mapped to is not being used as an old ID in any of the mappings,
                // so there is no need to move any of the other IDs around.
                if (targetIndex < 0)
                {
                    continue;
                }

                int currentMaxToId = lockedMap.ToId;

                for (int i = targetIndex; i < mapSize; i++)
                {
                    IdMap targetMap = maps[i];
                    if (targetMap.Locked)
                    {
                        continue;
                    }

                    while (targetMap.ToId <= currentMaxToId
                        || lockedTargetToIds.Contains(targetMap.ToId))
                    {
                        targetMap.ToId++;
                    }

                    currentMaxToId = targetMap.ToId;
                }
            }
        }

        private static void CheckIds(List<(int FromId, int ToId)> idUpdates)
        {
            // Check that all the old IDs are unique and all the
            // new IDs are unique.
            int fromIds = idUpdates.Select(x => x.FromId).Count();
            int toIds = idUpdates.Select(x => x.ToId).Count();

            Debug.Assert(fromIds == idUpdates.Select(x => x.FromId).Distinct().Count());
            Debug.Assert(toIds == idUpdates.Select(x => x.ToId).Distinct().Count());
            Debug.Assert(fromIds == toIds);

            int negativeFromIds = idUpdates.Select(x => x.FromId).Count(x => x <= 0);
            int negativeToIds = idUpdates.Select(x => x.ToId).Count(x => x <= 0);

            Debug.Assert(negativeFromIds == 0);
            Debug.Assert(negativeToIds == 0);
        }

        private static void CheckIds(List<int> ids)
        {
            Debug.Assert(ids.Count == ids.Distinct().Count());

            int negativeIds = ids.Count(x => x <= 0);

            Debug.Assert(negativeIds == 0);
        }

        public static ProjectScenarioModel UpdateActivityIds(
            ProjectScenarioModel projectScenarioModel,
            int fromId,
            int toId)
        {
            return UpdateActivityIds(projectScenarioModel, [(fromId, toId)]);
        }

        public static ProjectScenarioModel UpdateActivityIds(
            ProjectScenarioModel projectScenarioModel,
            List<(int FromId, int ToId)> idUpdates)
        {
            ArgumentNullException.ThrowIfNull(projectScenarioModel);
            ArgumentNullException.ThrowIfNull(idUpdates);
            CheckIds(idUpdates);

            List<int> originalIds = [.. projectScenarioModel
                .DependentActivities
                .OrderBy(x => x.Activity.DisplayOrder)
                .ThenBy(x => x.Activity.Id)
                .Select(x => x.Activity.Id)
                .Distinct()];

            // Update the ID mappings to make sure they are consistent and make logical sense.
            idUpdates = UpdateIds(originalIds, idUpdates);

            Dictionary<int, int> idUpdatesLookup = idUpdates.ToDictionary(x => x.FromId, x => x.ToId);

            // DependencyActivities.

            List<DependentActivityModel> newDependentActivities = [.. projectScenarioModel.DependentActivities.Select(dependentActivity =>
            {
                // ActivityModel -> ID
                int oldActivityId = dependentActivity.Activity.Id;
                int newActivityId = idUpdatesLookup.TryGetValue(oldActivityId, out int mappedNewActivityId) ? mappedNewActivityId : oldActivityId;

                List<ActivityTrackerModel> newTrackers = [.. dependentActivity.Activity.Trackers.Select(activityTracker =>
                {
                    // ActivityModel -> Trackers -> ActivityId
                    int oldTrackerActivityId = activityTracker.ActivityId;
                    int newTrackerActivityId = idUpdatesLookup.TryGetValue(oldTrackerActivityId, out int mappedNewTrackerActivityId) ? mappedNewTrackerActivityId : oldTrackerActivityId;
                    return activityTracker with { ActivityId = newTrackerActivityId };
                })];

                List<int> newDependencies = [.. dependentActivity.Dependencies.Select(dependencyId =>
                {
                    // Dependencies
                    return idUpdatesLookup.TryGetValue(dependencyId, out int mappedNewDependencyId) ? mappedNewDependencyId : dependencyId;
                })];

                List<int> newPlanningDependencies = [.. dependentActivity.PlanningDependencies.Select(planningDependencyId =>
                {
                    // PlanningDependencies
                    return idUpdatesLookup.TryGetValue(planningDependencyId, out int mappedNewPlanningDependencyId) ? mappedNewPlanningDependencyId : planningDependencyId;
                })];

                List<int> newResourceDependencies = [.. dependentActivity.ResourceDependencies.Select(resourceDependencyId =>
                {
                    // ResourceDependencies
                    return idUpdatesLookup.TryGetValue(resourceDependencyId, out int mappedNewResourceDependencyId) ? mappedNewResourceDependencyId : resourceDependencyId;
                })];

                List<int> newSuccessors = [.. dependentActivity.Successors.Select(successorId =>
                {
                    // Successors
                    return idUpdatesLookup.TryGetValue(successorId, out int mappedNewSuccessorId) ? mappedNewSuccessorId : successorId;
                })];

                return dependentActivity with
                {
                    Activity = dependentActivity.Activity with { Id = newActivityId, Trackers = newTrackers },
                    Dependencies = newDependencies,
                    PlanningDependencies = newPlanningDependencies,
                    ResourceDependencies = newResourceDependencies,
                    Successors = newSuccessors
                };
            })];

            // ResourceSettings -> Resources -> Trackers -> ActivityTrackers -> ActivityId

            List<ResourceModel> oldResources = projectScenarioModel.ResourceSettings.Resources;

            List<ResourceModel> newResources = [.. oldResources.Select(resource =>
            {
                List<ResourceTrackerModel> newResourceTrackers = [.. resource.Trackers.Select(resourceTracker =>
                {
                    List<ResourceActivityTrackerModel> newActivityTrackers = [.. resourceTracker.ActivityTrackers.Select(activityTracker =>
                    {
                        // ResourceSettings -> Resources -> Trackers -> ActivityTrackers -> ActivityId
                        int oldActivityId = activityTracker.ActivityId;
                        int newActivityId = idUpdatesLookup.TryGetValue(oldActivityId, out int mappedNewActivityId) ? mappedNewActivityId : oldActivityId;
                        return activityTracker with { ActivityId = newActivityId };
                    })];

                    return resourceTracker with { ActivityTrackers = newActivityTrackers };
                })];

                return resource with { Trackers = newResourceTrackers };
            })];

            // Return the new project scenario model with the updated dependent activities and resources.

            projectScenarioModel = projectScenarioModel with
            {
                DependentActivities = newDependentActivities,
                ResourceSettings = projectScenarioModel.ResourceSettings with
                {
                    Resources = newResources,
                }
            };

            return projectScenarioModel;
        }
    }
}
