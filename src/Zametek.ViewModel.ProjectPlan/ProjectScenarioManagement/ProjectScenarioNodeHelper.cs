using DynamicData.Binding;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Utility;
using SortDirection = Zametek.Common.ProjectPlan.SortDirection;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// The output of cloning node subtrees: the cloned node models (parents
    /// before children), the cloned scenario files, the cloned tags (each
    /// keyed to its clone's new id), and the source-to-clone id map (used to
    /// relocate the loaded scenario when a cut moves it).
    /// </summary>
    public sealed record NodeCloneResult
    {
        public List<ProjectScenarioNodeModel> Nodes { get; init; } = [];

        public List<ProjectScenarioFileModel> Files { get; init; } = [];

        public List<ProjectScenarioTagModel> Tags { get; init; } = [];

        public Dictionary<Guid, Guid> IdMap { get; init; } = [];
    }

    /// <summary>
    /// Pure helper logic for the project scenario browser: the node sort
    /// comparers (with their deterministic tie-breakers), sibling name
    /// suggestion, subtree traversal, top-most selection filtering, the
    /// cut-into-own-subtree guard, and recursive cloning of folders and
    /// scenarios (with their tags) for cut/copy/paste. Kept free of
    /// view-model state so the behaviour can be unit tested directly.
    /// </summary>
    public static class ProjectScenarioNodeHelper
    {
        /// <summary>
        /// Builds the comparer for a node sort mode and direction, always
        /// with a deterministic tie-breaker. The tie-breaker matters because
        /// the timestamp keys are not unique: a batch operation (multi-select
        /// paste, a recursive folder clone) mints one timestamp for every
        /// node it creates, and a comparer without a secondary key leaves the
        /// relative order of such ties unspecified - inserts append ties in
        /// arrival order while re-sorts shuffle them stably - so the tree
        /// appeared unsorted whenever timestamps collided. Timestamp modes
        /// break ties by name; name mode (unique within a sibling set anyway)
        /// breaks ties by creation time. The secondary key follows the
        /// primary direction, so a descending sort reads fully reversed.
        /// </summary>
        public static SortExpressionComparer<IManagedNodeViewModel> BuildSortComparer(
            SortMode sortMode,
            SortDirection sortDirection)
        {
            Func<IManagedNodeViewModel, IComparable> primary = sortMode switch
            {
                SortMode.Name => (x) => x.Name,
                SortMode.CreatedOn => (x) => x.CreatedOn,
                SortMode.ModifiedOn => (x) => x.ModifiedOn,
                _ => throw new ArgumentOutOfRangeException(nameof(sortMode), @$"{Resource.ProjectPlan.Messages.Message_UnknownSortMode} {sortMode}"),
            };

            Func<IManagedNodeViewModel, IComparable> tieBreaker = sortMode switch
            {
                SortMode.Name => (x) => x.CreatedOn,
                _ => (x) => x.Name,
            };

            return sortDirection switch
            {
                SortDirection.Ascending => SortExpressionComparer<IManagedNodeViewModel>.Ascending(primary).ThenByAscending(tieBreaker),
                SortDirection.Descending => SortExpressionComparer<IManagedNodeViewModel>.Descending(primary).ThenByDescending(tieBreaker),
                _ => throw new ArgumentOutOfRangeException(nameof(sortDirection), @$"{Resource.ProjectPlan.Messages.Message_UnknownSortDirection} {sortDirection}"),
            };
        }

        /// <summary>
        /// Suggests a unique node name within a sibling set by suffixing
        /// -1, -2, ... until the name is free.
        /// </summary>
        public static string SuggestNodeName(
            string suggestedName,
            ISet<string> existingNames)
        {
            ArgumentNullException.ThrowIfNull(suggestedName);
            ArgumentNullException.ThrowIfNull(existingNames);

            int count = 0;
            string newName = suggestedName;

            while (existingNames.Contains(newName))
            {
                count++;
                newName = $@"{suggestedName}-{count}";
            }

            return newName;
        }

        /// <summary>
        /// Filters a selection down to its top-most nodes: any node whose
        /// ancestor is also selected is dropped, so acting on the selection
        /// can never process part of a subtree twice. Input order is
        /// preserved; duplicate and unknown ids are dropped.
        /// </summary>
        public static List<Guid> SelectTopMostNodes(
            IReadOnlyCollection<ProjectScenarioNodeModel> allNodes,
            IEnumerable<Guid> nodeIds)
        {
            ArgumentNullException.ThrowIfNull(allNodes);
            ArgumentNullException.ThrowIfNull(nodeIds);

            Dictionary<Guid, ProjectScenarioNodeModel> nodeLookup = BuildNodeLookup(allNodes);
            HashSet<Guid> selectedIds = [.. nodeIds];
            List<Guid> result = [];
            HashSet<Guid> seenIds = [];

            foreach (Guid nodeId in nodeIds)
            {
                if (!seenIds.Add(nodeId)
                    || !nodeLookup.TryGetValue(nodeId, out ProjectScenarioNodeModel? node))
                {
                    continue;
                }

                // Walk the ancestor chain; the visited set guards against
                // malformed (cyclic) parent links. The chain ends when the
                // parent id is unknown (i.e. the virtual root).
                bool hasSelectedAncestor = false;
                HashSet<Guid> visitedIds = [nodeId];
                Guid parentId = node.ParentId;

                while (nodeLookup.TryGetValue(parentId, out ProjectScenarioNodeModel? parent)
                    && visitedIds.Add(parentId))
                {
                    if (selectedIds.Contains(parentId))
                    {
                        hasSelectedAncestor = true;
                        break;
                    }
                    parentId = parent.ParentId;
                }

                if (!hasSelectedAncestor)
                {
                    result.Add(nodeId);
                }
            }

            return result;
        }

        /// <summary>
        /// Collects each root node plus all its descendants, in
        /// parents-before-children order. Overlapping roots are deduplicated
        /// and unknown roots contribute nothing.
        /// </summary>
        public static List<Guid> CollectSubtreeIds(
            IReadOnlyCollection<ProjectScenarioNodeModel> allNodes,
            IEnumerable<Guid> rootIds)
        {
            ArgumentNullException.ThrowIfNull(allNodes);
            ArgumentNullException.ThrowIfNull(rootIds);

            Dictionary<Guid, ProjectScenarioNodeModel> nodeLookup = BuildNodeLookup(allNodes);
            Dictionary<Guid, List<ProjectScenarioNodeModel>> childrenIndex = BuildChildrenIndex(allNodes);
            List<Guid> result = [];
            HashSet<Guid> collectedIds = [];

            void Collect(Guid nodeId)
            {
                if (!collectedIds.Add(nodeId))
                {
                    return;
                }

                result.Add(nodeId);

                if (childrenIndex.TryGetValue(nodeId, out List<ProjectScenarioNodeModel>? children))
                {
                    foreach (ProjectScenarioNodeModel child in children)
                    {
                        Collect(child.Id);
                    }
                }
            }

            foreach (Guid rootId in rootIds)
            {
                if (nodeLookup.ContainsKey(rootId))
                {
                    Collect(rootId);
                }
            }

            return result;
        }

        /// <summary>
        /// True when the candidate node is one of the given subtree roots or
        /// lies anywhere beneath one. Used to block pasting cut nodes into
        /// themselves or their own subtrees (the freshly pasted clones would
        /// otherwise be removed along with the originals).
        /// </summary>
        public static bool IsWithinSubtree(
            IReadOnlyCollection<ProjectScenarioNodeModel> allNodes,
            Guid candidateId,
            IEnumerable<Guid> subtreeRootIds)
        {
            ArgumentNullException.ThrowIfNull(allNodes);
            ArgumentNullException.ThrowIfNull(subtreeRootIds);

            Dictionary<Guid, ProjectScenarioNodeModel> nodeLookup = BuildNodeLookup(allNodes);
            HashSet<Guid> rootIds = [.. subtreeRootIds];
            HashSet<Guid> visitedIds = [];
            Guid currentId = candidateId;

            while (visitedIds.Add(currentId))
            {
                if (rootIds.Contains(currentId))
                {
                    return true;
                }
                if (!nodeLookup.TryGetValue(currentId, out ProjectScenarioNodeModel? node))
                {
                    // Reached the virtual root (or an unknown id).
                    return false;
                }
                currentId = node.ParentId;
            }

            // A cycle in the parent links; treat as not contained.
            return false;
        }

        /// <summary>
        /// Clones the subtrees rooted at the source ids beneath the
        /// destination parent, from a snapshot of the node table (so cloning
        /// a folder into itself is safe). Every clone receives a fresh id
        /// from the factory; top-level clones are renamed to avoid clashes
        /// with the destination's sibling names (which this method extends as
        /// each clone lands), while nested clones keep their names verbatim
        /// because their parent folders are freshly created. Scenario content
        /// is deep-cloned and each clone carries its own copies of its
        /// source's tags. Timestamps follow the Windows Explorer file rules:
        /// a clone always keeps its source's ModifiedOn; a move (cut, i.e.
        /// preserveCreatedOn = true) also keeps the source's CreatedOn, while
        /// a copy receives the given timestamp as its CreatedOn. The source
        /// ids are expected to be top-most (see
        /// <see cref="SelectTopMostNodes"/>); unknown ids are skipped.
        /// </summary>
        public static NodeCloneResult CloneSubtrees(
            IReadOnlyCollection<ProjectScenarioNodeModel> allNodes,
            IReadOnlyDictionary<Guid, ProjectScenarioModel> scenarioLookup,
            IReadOnlyCollection<ProjectScenarioTagModel> tags,
            IEnumerable<Guid> sourceIds,
            Guid destinationParentId,
            ISet<string> existingDestinationNames,
            Func<Guid> idFactory,
            DateTimeOffset timestamp,
            bool preserveCreatedOn)
        {
            ArgumentNullException.ThrowIfNull(allNodes);
            ArgumentNullException.ThrowIfNull(scenarioLookup);
            ArgumentNullException.ThrowIfNull(tags);
            ArgumentNullException.ThrowIfNull(sourceIds);
            ArgumentNullException.ThrowIfNull(existingDestinationNames);
            ArgumentNullException.ThrowIfNull(idFactory);

            Dictionary<Guid, ProjectScenarioNodeModel> nodeLookup = BuildNodeLookup(allNodes);
            Dictionary<Guid, List<ProjectScenarioNodeModel>> childrenIndex = BuildChildrenIndex(allNodes);

            Dictionary<Guid, List<string>> tagIndex = [];
            foreach (ProjectScenarioTagModel tag in tags)
            {
                if (!tagIndex.TryGetValue(tag.NodeId, out List<string>? labels))
                {
                    labels = [];
                    tagIndex[tag.NodeId] = labels;
                }
                labels.Add(tag.Label);
            }

            var result = new NodeCloneResult();

            void CloneNode(
                ProjectScenarioNodeModel source,
                Guid newParentId,
                string newName)
            {
                Guid newId = idFactory();
                result.IdMap[source.Id] = newId;

                // ModifiedOn is always inherited from the source (the record
                // `with` carries it over); CreatedOn is inherited on a move
                // and reset to the paste timestamp on a copy.
                result.Nodes.Add(source with
                {
                    Id = newId,
                    ParentId = newParentId,
                    Name = newName,
                    CreatedOn = preserveCreatedOn ? source.CreatedOn : timestamp,
                });

                if (tagIndex.TryGetValue(source.Id, out List<string>? labels))
                {
                    foreach (string label in labels)
                    {
                        result.Tags.Add(new ProjectScenarioTagModel
                        {
                            NodeId = newId,
                            Label = label,
                        });
                    }
                }

                if (source.NodeType != ProjectScenarioNodeType.Folder
                    && scenarioLookup.TryGetValue(source.Id, out ProjectScenarioModel? scenario))
                {
                    result.Files.Add(new ProjectScenarioFileModel
                    {
                        NodeId = newId,
                        Scenario = scenario.CloneObject(),
                    });
                }

                if (childrenIndex.TryGetValue(source.Id, out List<ProjectScenarioNodeModel>? children))
                {
                    foreach (ProjectScenarioNodeModel child in children)
                    {
                        CloneNode(child, newId, child.Name);
                    }
                }
            }

            foreach (Guid sourceId in sourceIds)
            {
                if (!nodeLookup.TryGetValue(sourceId, out ProjectScenarioNodeModel? source))
                {
                    continue;
                }

                string newName = SuggestNodeName(source.Name, existingDestinationNames);
                existingDestinationNames.Add(newName);
                CloneNode(source, destinationParentId, newName);
            }

            return result;
        }

        private static Dictionary<Guid, ProjectScenarioNodeModel> BuildNodeLookup(
            IReadOnlyCollection<ProjectScenarioNodeModel> allNodes)
        {
            Dictionary<Guid, ProjectScenarioNodeModel> nodeLookup = [];
            foreach (ProjectScenarioNodeModel node in allNodes)
            {
                nodeLookup.TryAdd(node.Id, node);
            }
            return nodeLookup;
        }

        private static Dictionary<Guid, List<ProjectScenarioNodeModel>> BuildChildrenIndex(
            IReadOnlyCollection<ProjectScenarioNodeModel> allNodes)
        {
            Dictionary<Guid, List<ProjectScenarioNodeModel>> childrenIndex = [];
            foreach (ProjectScenarioNodeModel node in allNodes)
            {
                if (!childrenIndex.TryGetValue(node.ParentId, out List<ProjectScenarioNodeModel>? children))
                {
                    children = [];
                    childrenIndex[node.ParentId] = children;
                }
                children.Add(node);
            }
            return childrenIndex;
        }
    }
}
