using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for ProjectScenarioNodeHelper: the pure mechanics behind the
    /// scenario browser's cut/copy/paste/delete. Sibling name suggestion,
    /// top-most selection filtering (so a folder and its descendants are
    /// never processed twice), subtree collection (delete and cut removal),
    /// the cut-into-own-subtree guard, and recursive subtree cloning with
    /// scenario files and tags. Clone timestamps follow the Windows Explorer
    /// file rules: ModifiedOn is always preserved; CreatedOn is preserved on
    /// a move (cut) and reset to the paste time on a copy.
    /// </summary>
    public class ProjectScenarioNodeHelperTests
    {
        private static readonly Guid s_RootId = MakeId(999);
        private static readonly DateTimeOffset s_SourceCreatedTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        private static readonly DateTimeOffset s_SourceModifiedTime = new(2026, 2, 2, 0, 0, 0, TimeSpan.Zero);
        private static readonly DateTimeOffset s_PasteTime = new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);

        private static Guid MakeId(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");

        private static Func<Guid> SequentialIds(int start)
        {
            int next = start;
            return () => MakeId(next++);
        }

        private static ProjectScenarioNodeModel MakeFolder(
            int id,
            Guid parentId,
            string name)
        {
            return new ProjectScenarioNodeModel
            {
                Id = MakeId(id),
                ParentId = parentId,
                NodeType = ProjectScenarioNodeType.Folder,
                Name = name,
                CreatedOn = s_SourceCreatedTime,
                ModifiedOn = s_SourceModifiedTime,
            };
        }

        private static ProjectScenarioNodeModel MakeScenario(
            int id,
            Guid parentId,
            string name,
            bool isTracked = false)
        {
            return new ProjectScenarioNodeModel
            {
                Id = MakeId(id),
                ParentId = parentId,
                NodeType = ProjectScenarioNodeType.File,
                Name = name,
                CreatedOn = s_SourceCreatedTime,
                ModifiedOn = s_SourceModifiedTime,
                IsTracked = isTracked,
            };
        }

        private static ProjectScenarioTagModel MakeTag(
            int nodeId,
            string label)
        {
            return new ProjectScenarioTagModel
            {
                NodeId = MakeId(nodeId),
                Label = label,
            };
        }

        // A three-level tree used by several tests:
        // Folder 1 -> Scenario 2, Folder 3; Folder 3 -> Scenario 4.
        private static List<ProjectScenarioNodeModel> MakeTree()
        {
            return
            [
                MakeFolder(1, s_RootId, @"Outer"),
                MakeScenario(2, MakeId(1), @"Alpha"),
                MakeFolder(3, MakeId(1), @"Inner"),
                MakeScenario(4, MakeId(3), @"Beta"),
            ];
        }

        [Fact]
        public void SuggestNodeName_Given_NoClash_Then_SameName()
        {
            ProjectScenarioNodeHelper.SuggestNodeName(@"Alpha", new HashSet<string>())
                .ShouldBe(@"Alpha");
        }

        [Fact]
        public void SuggestNodeName_Given_Clash_Then_SuffixedName()
        {
            ProjectScenarioNodeHelper.SuggestNodeName(@"Alpha", new HashSet<string> { @"Alpha" })
                .ShouldBe(@"Alpha-1");
        }

        [Fact]
        public void SuggestNodeName_Given_RepeatedClashes_Then_IncrementingSuffix()
        {
            ProjectScenarioNodeHelper.SuggestNodeName(@"Alpha", new HashSet<string> { @"Alpha", @"Alpha-1" })
                .ShouldBe(@"Alpha-2");
        }

        [Fact]
        public void SelectTopMostNodes_Given_IndependentNodes_Then_AllReturnedInOrder()
        {
            List<ProjectScenarioNodeModel> allNodes =
            [
                MakeScenario(1, s_RootId, @"Alpha"),
                MakeScenario(2, s_RootId, @"Beta"),
            ];

            ProjectScenarioNodeHelper.SelectTopMostNodes(allNodes, [MakeId(2), MakeId(1)])
                .ShouldBe([MakeId(2), MakeId(1)]);
        }

        [Fact]
        public void SelectTopMostNodes_Given_FolderAndChild_Then_ChildDropped()
        {
            ProjectScenarioNodeHelper.SelectTopMostNodes(MakeTree(), [MakeId(1), MakeId(2)])
                .ShouldBe([MakeId(1)]);
        }

        [Fact]
        public void SelectTopMostNodes_Given_FolderAndDeepDescendant_Then_DescendantDropped()
        {
            // Scenario 4 sits two levels beneath Folder 1; listing it first
            // must not save it.
            ProjectScenarioNodeHelper.SelectTopMostNodes(MakeTree(), [MakeId(4), MakeId(1)])
                .ShouldBe([MakeId(1)]);
        }

        [Fact]
        public void SelectTopMostNodes_Given_DuplicateIds_Then_Deduplicated()
        {
            ProjectScenarioNodeHelper.SelectTopMostNodes(MakeTree(), [MakeId(2), MakeId(2)])
                .ShouldBe([MakeId(2)]);
        }

        [Fact]
        public void SelectTopMostNodes_Given_UnknownId_Then_Dropped()
        {
            ProjectScenarioNodeHelper.SelectTopMostNodes(MakeTree(), [MakeId(42), MakeId(2)])
                .ShouldBe([MakeId(2)]);
        }

        [Fact]
        public void CollectSubtreeIds_Given_ScenarioNode_Then_JustItself()
        {
            ProjectScenarioNodeHelper.CollectSubtreeIds(MakeTree(), [MakeId(2)])
                .ShouldBe([MakeId(2)]);
        }

        [Fact]
        public void CollectSubtreeIds_Given_Folder_Then_AllDescendantsIncluded()
        {
            List<Guid> result = ProjectScenarioNodeHelper.CollectSubtreeIds(MakeTree(), [MakeId(1)]);

            result.Count.ShouldBe(4);
            result.ShouldContain(MakeId(1));
            result.ShouldContain(MakeId(2));
            result.ShouldContain(MakeId(3));
            result.ShouldContain(MakeId(4));
        }

        [Fact]
        public void CollectSubtreeIds_Given_OverlappingRoots_Then_NoDuplicates()
        {
            // Scenario 4 is already inside Folder 1's subtree.
            List<Guid> result = ProjectScenarioNodeHelper.CollectSubtreeIds(MakeTree(), [MakeId(1), MakeId(4)]);

            result.Count.ShouldBe(4);
            result.Distinct().Count().ShouldBe(4);
        }

        [Fact]
        public void CollectSubtreeIds_Given_UnknownRoot_Then_Empty()
        {
            ProjectScenarioNodeHelper.CollectSubtreeIds(MakeTree(), [MakeId(42)])
                .ShouldBeEmpty();
        }

        [Fact]
        public void IsWithinSubtree_Given_RootItself_Then_True()
        {
            ProjectScenarioNodeHelper.IsWithinSubtree(MakeTree(), MakeId(1), [MakeId(1)])
                .ShouldBeTrue();
        }

        [Fact]
        public void IsWithinSubtree_Given_DeepDescendant_Then_True()
        {
            // Folder 3 sits inside Folder 1.
            ProjectScenarioNodeHelper.IsWithinSubtree(MakeTree(), MakeId(3), [MakeId(1)])
                .ShouldBeTrue();
        }

        [Fact]
        public void IsWithinSubtree_Given_Sibling_Then_False()
        {
            List<ProjectScenarioNodeModel> allNodes =
            [
                .. MakeTree(),
                MakeFolder(5, s_RootId, @"Elsewhere"),
            ];

            ProjectScenarioNodeHelper.IsWithinSubtree(allNodes, MakeId(5), [MakeId(1)])
                .ShouldBeFalse();
        }

        [Fact]
        public void IsWithinSubtree_Given_VirtualRootDestination_Then_False()
        {
            // The virtual root is not part of the node table.
            ProjectScenarioNodeHelper.IsWithinSubtree(MakeTree(), s_RootId, [MakeId(1)])
                .ShouldBeFalse();
        }

        [Fact]
        public void CloneSubtrees_Given_Copy_Then_CreatedOnResetAndModifiedOnPreserved()
        {
            List<ProjectScenarioNodeModel> allNodes = [MakeScenario(1, s_RootId, @"Alpha", isTracked: true)];
            Dictionary<Guid, ProjectScenarioModel> scenarioLookup = new() { [MakeId(1)] = new ProjectScenarioModel() };
            Guid destinationId = MakeId(50);

            NodeCloneResult result = ProjectScenarioNodeHelper.CloneSubtrees(
                allNodes,
                scenarioLookup,
                [],
                [MakeId(1)],
                destinationId,
                new HashSet<string>(),
                SequentialIds(100),
                s_PasteTime,
                preserveCreatedOn: false);

            ProjectScenarioNodeModel clone = result.Nodes.ShouldHaveSingleItem();
            clone.Id.ShouldBe(MakeId(100));
            clone.ParentId.ShouldBe(destinationId);
            clone.NodeType.ShouldBe(ProjectScenarioNodeType.File);
            clone.Name.ShouldBe(@"Alpha");
            clone.IsTracked.ShouldBeTrue();

            // Windows copy semantics: fresh CreatedOn, source's ModifiedOn.
            clone.CreatedOn.ShouldBe(s_PasteTime);
            clone.ModifiedOn.ShouldBe(s_SourceModifiedTime);

            result.IdMap.ShouldHaveSingleItem();
            result.IdMap[MakeId(1)].ShouldBe(MakeId(100));
        }

        [Fact]
        public void CloneSubtrees_Given_Cut_Then_BothTimestampsPreserved()
        {
            List<ProjectScenarioNodeModel> allNodes = [MakeScenario(1, s_RootId, @"Alpha")];

            NodeCloneResult result = ProjectScenarioNodeHelper.CloneSubtrees(
                allNodes,
                new Dictionary<Guid, ProjectScenarioModel>(),
                [],
                [MakeId(1)],
                MakeId(50),
                new HashSet<string>(),
                SequentialIds(100),
                s_PasteTime,
                preserveCreatedOn: true);

            ProjectScenarioNodeModel clone = result.Nodes.ShouldHaveSingleItem();

            // Windows move semantics: both timestamps survive the move.
            clone.CreatedOn.ShouldBe(s_SourceCreatedTime);
            clone.ModifiedOn.ShouldBe(s_SourceModifiedTime);
        }

        [Fact]
        public void CloneSubtrees_Given_CutFolder_Then_NestedTimestampsPreserved()
        {
            NodeCloneResult result = ProjectScenarioNodeHelper.CloneSubtrees(
                MakeTree(),
                new Dictionary<Guid, ProjectScenarioModel>(),
                [],
                [MakeId(1)],
                MakeId(50),
                new HashSet<string>(),
                SequentialIds(100),
                s_PasteTime,
                preserveCreatedOn: true);

            result.Nodes.Count.ShouldBe(4);

            foreach (ProjectScenarioNodeModel clone in result.Nodes)
            {
                clone.CreatedOn.ShouldBe(s_SourceCreatedTime);
                clone.ModifiedOn.ShouldBe(s_SourceModifiedTime);
            }
        }

        [Fact]
        public void CloneSubtrees_Given_NameClash_Then_TopLevelNameSuffixed()
        {
            List<ProjectScenarioNodeModel> allNodes = [MakeScenario(1, s_RootId, @"Alpha")];

            NodeCloneResult result = ProjectScenarioNodeHelper.CloneSubtrees(
                allNodes,
                new Dictionary<Guid, ProjectScenarioModel>(),
                [],
                [MakeId(1)],
                s_RootId,
                new HashSet<string> { @"Alpha" },
                SequentialIds(100),
                s_PasteTime,
                preserveCreatedOn: false);

            result.Nodes.ShouldHaveSingleItem().Name.ShouldBe(@"Alpha-1");
        }

        [Fact]
        public void CloneSubtrees_Given_MultipleSourcesWithSameName_Then_SequentialSuffixes()
        {
            List<ProjectScenarioNodeModel> allNodes =
            [
                MakeScenario(1, s_RootId, @"Alpha"),
                MakeScenario(2, MakeId(9), @"Alpha"),
                MakeFolder(9, s_RootId, @"Folder"),
            ];

            NodeCloneResult result = ProjectScenarioNodeHelper.CloneSubtrees(
                allNodes,
                new Dictionary<Guid, ProjectScenarioModel>(),
                [],
                [MakeId(1), MakeId(2)],
                MakeId(50),
                new HashSet<string> { @"Alpha" },
                SequentialIds(100),
                s_PasteTime,
                preserveCreatedOn: false);

            result.Nodes.Count.ShouldBe(2);
            result.Nodes[0].Name.ShouldBe(@"Alpha-1");
            result.Nodes[1].Name.ShouldBe(@"Alpha-2");
        }

        [Fact]
        public void CloneSubtrees_Given_Scenario_Then_ScenarioContentDeepCloned()
        {
            var sourceScenario = new ProjectScenarioModel
            {
                ProjectStart = new DateTimeOffset(2026, 3, 4, 0, 0, 0, TimeSpan.Zero),
            };
            List<ProjectScenarioNodeModel> allNodes = [MakeScenario(1, s_RootId, @"Alpha")];
            Dictionary<Guid, ProjectScenarioModel> scenarioLookup = new() { [MakeId(1)] = sourceScenario };

            NodeCloneResult result = ProjectScenarioNodeHelper.CloneSubtrees(
                allNodes,
                scenarioLookup,
                [],
                [MakeId(1)],
                MakeId(50),
                new HashSet<string>(),
                SequentialIds(100),
                s_PasteTime,
                preserveCreatedOn: false);

            ProjectScenarioFileModel file = result.Files.ShouldHaveSingleItem();
            file.NodeId.ShouldBe(MakeId(100));
            file.Scenario.ShouldNotBeSameAs(sourceScenario);
            file.Scenario.ProjectStart.ShouldBe(sourceScenario.ProjectStart);
        }

        [Fact]
        public void CloneSubtrees_Given_TaggedNode_Then_TagsCopiedToCloneId()
        {
            List<ProjectScenarioNodeModel> allNodes = [MakeScenario(1, s_RootId, @"Alpha")];
            List<ProjectScenarioTagModel> tags =
            [
                MakeTag(1, @"red"),
                MakeTag(1, @"blue"),
                MakeTag(7, @"unrelated"),
            ];

            NodeCloneResult result = ProjectScenarioNodeHelper.CloneSubtrees(
                allNodes,
                new Dictionary<Guid, ProjectScenarioModel>(),
                tags,
                [MakeId(1)],
                MakeId(50),
                new HashSet<string>(),
                SequentialIds(100),
                s_PasteTime,
                preserveCreatedOn: false);

            result.Tags.Count.ShouldBe(2);
            result.Tags[0].NodeId.ShouldBe(MakeId(100));
            result.Tags[0].Label.ShouldBe(@"red");
            result.Tags[1].NodeId.ShouldBe(MakeId(100));
            result.Tags[1].Label.ShouldBe(@"blue");

            // The source tag models are untouched: the clone's tags are its own.
            tags.Count.ShouldBe(3);
            tags[0].NodeId.ShouldBe(MakeId(1));
        }

        [Fact]
        public void CloneSubtrees_Given_Folder_Then_SubtreeClonedRecursively()
        {
            List<ProjectScenarioNodeModel> allNodes = MakeTree();
            Dictionary<Guid, ProjectScenarioModel> scenarioLookup = new()
            {
                [MakeId(2)] = new ProjectScenarioModel(),
                [MakeId(4)] = new ProjectScenarioModel(),
            };
            List<ProjectScenarioTagModel> tags =
            [
                MakeTag(1, @"outer-tag"),
                MakeTag(4, @"beta-tag"),
            ];

            NodeCloneResult result = ProjectScenarioNodeHelper.CloneSubtrees(
                allNodes,
                scenarioLookup,
                tags,
                [MakeId(1)],
                MakeId(50),
                new HashSet<string>(),
                SequentialIds(100),
                s_PasteTime,
                preserveCreatedOn: false);

            result.Nodes.Count.ShouldBe(4);
            result.IdMap.Count.ShouldBe(4);

            // The top-level clone lands under the destination; every nested
            // clone hangs off its parent's clone.
            ProjectScenarioNodeModel outerClone = result.Nodes.Single(x => x.Id == result.IdMap[MakeId(1)]);
            ProjectScenarioNodeModel alphaClone = result.Nodes.Single(x => x.Id == result.IdMap[MakeId(2)]);
            ProjectScenarioNodeModel innerClone = result.Nodes.Single(x => x.Id == result.IdMap[MakeId(3)]);
            ProjectScenarioNodeModel betaClone = result.Nodes.Single(x => x.Id == result.IdMap[MakeId(4)]);

            outerClone.ParentId.ShouldBe(MakeId(50));
            alphaClone.ParentId.ShouldBe(outerClone.Id);
            innerClone.ParentId.ShouldBe(outerClone.Id);
            betaClone.ParentId.ShouldBe(innerClone.Id);

            // Nested clones keep their names verbatim.
            alphaClone.Name.ShouldBe(@"Alpha");
            innerClone.Name.ShouldBe(@"Inner");
            betaClone.Name.ShouldBe(@"Beta");

            // Copy semantics apply to every clone in the subtree.
            foreach (ProjectScenarioNodeModel clone in result.Nodes)
            {
                clone.CreatedOn.ShouldBe(s_PasteTime);
                clone.ModifiedOn.ShouldBe(s_SourceModifiedTime);
            }

            // Scenario files are cloned for the file nodes only.
            result.Files.Count.ShouldBe(2);
            result.Files.Select(x => x.NodeId).ShouldBe([alphaClone.Id, betaClone.Id], ignoreOrder: true);

            // Tags follow their nodes to the clones.
            result.Tags.Count.ShouldBe(2);
            result.Tags.Single(x => x.Label == @"outer-tag").NodeId.ShouldBe(outerClone.Id);
            result.Tags.Single(x => x.Label == @"beta-tag").NodeId.ShouldBe(betaClone.Id);
        }

        [Fact]
        public void CloneSubtrees_Given_EmptyFolder_Then_SingleFolderCloned()
        {
            List<ProjectScenarioNodeModel> allNodes = [MakeFolder(1, s_RootId, @"Outer")];

            NodeCloneResult result = ProjectScenarioNodeHelper.CloneSubtrees(
                allNodes,
                new Dictionary<Guid, ProjectScenarioModel>(),
                [],
                [MakeId(1)],
                MakeId(50),
                new HashSet<string>(),
                SequentialIds(100),
                s_PasteTime,
                preserveCreatedOn: false);

            ProjectScenarioNodeModel clone = result.Nodes.ShouldHaveSingleItem();
            clone.NodeType.ShouldBe(ProjectScenarioNodeType.Folder);
            result.Files.ShouldBeEmpty();
        }

        [Fact]
        public void CloneSubtrees_Given_FolderPastedIntoItself_Then_SnapshotCloned()
        {
            // Copying a folder into itself must clone the pre-existing
            // subtree exactly once (the traversal works from a snapshot).
            List<ProjectScenarioNodeModel> allNodes = MakeTree();

            NodeCloneResult result = ProjectScenarioNodeHelper.CloneSubtrees(
                allNodes,
                new Dictionary<Guid, ProjectScenarioModel>(),
                [],
                [MakeId(1)],
                MakeId(1),
                new HashSet<string> { @"Alpha", @"Inner" },
                SequentialIds(100),
                s_PasteTime,
                preserveCreatedOn: false);

            result.Nodes.Count.ShouldBe(4);
            result.Nodes.Single(x => x.Id == result.IdMap[MakeId(1)]).ParentId.ShouldBe(MakeId(1));
        }

        [Fact]
        public void CloneSubtrees_Given_UnknownSourceId_Then_Skipped()
        {
            NodeCloneResult result = ProjectScenarioNodeHelper.CloneSubtrees(
                MakeTree(),
                new Dictionary<Guid, ProjectScenarioModel>(),
                [],
                [MakeId(42)],
                MakeId(50),
                new HashSet<string>(),
                SequentialIds(100),
                s_PasteTime,
                preserveCreatedOn: false);

            result.Nodes.ShouldBeEmpty();
            result.Files.ShouldBeEmpty();
            result.Tags.ShouldBeEmpty();
            result.IdMap.ShouldBeEmpty();
        }

        [Fact]
        public void CloneSubtrees_Given_MultipleSources_Then_ClonedInInputOrder()
        {
            List<ProjectScenarioNodeModel> allNodes =
            [
                MakeScenario(1, s_RootId, @"Alpha"),
                MakeScenario(2, s_RootId, @"Beta"),
            ];

            NodeCloneResult result = ProjectScenarioNodeHelper.CloneSubtrees(
                allNodes,
                new Dictionary<Guid, ProjectScenarioModel>(),
                [],
                [MakeId(2), MakeId(1)],
                MakeId(50),
                new HashSet<string>(),
                SequentialIds(100),
                s_PasteTime,
                preserveCreatedOn: false);

            result.Nodes.Count.ShouldBe(2);
            result.Nodes[0].Name.ShouldBe(@"Beta");
            result.Nodes[1].Name.ShouldBe(@"Alpha");
        }
    }
}
