using Shouldly;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.Engine.ProjectPlan.Tests
{
    /// <summary>
    /// Unit tests for scenario selection by name, id or id prefix, and for the
    /// scenario listing with its folder paths.
    /// </summary>
    public class ScenarioSelectorTests
    {
        private static readonly Guid s_Root = Guid.Parse(@"b0a4f078-4de5-4d3b-a2be-b9e2f2a4b6f1");
        private static readonly Guid s_Alpha = Guid.Parse(@"8f4d2f43-4c1b-4f16-9df8-40e1a2b3c4d5");
        private static readonly Guid s_Beta = Guid.Parse(@"17c3e2d9-95a4-4b47-b7ff-51f0a1b2c3d4");

        private static ProjectModel BuildProjectModel()
        {
            return new ProjectModel
            {
                Root = s_Root,
                Current = s_Alpha,
                Nodes =
                [
                    new ProjectScenarioNodeModel { Id = s_Alpha, ParentId = s_Root, NodeType = ProjectScenarioNodeType.File, Name = @"Alpha" },
                    new ProjectScenarioNodeModel { Id = s_Beta, ParentId = s_Root, NodeType = ProjectScenarioNodeType.File, Name = @"Beta" },
                ],
                Files =
                [
                    new ProjectScenarioFileModel { NodeId = s_Alpha, Scenario = new ProjectScenarioModel() },
                    new ProjectScenarioFileModel { NodeId = s_Beta, Scenario = new ProjectScenarioModel() },
                ],
            };
        }

        [Fact]
        public void ResolveScenarioId_Given_Name_Then_MatchesCaseInsensitively()
        {
            ScenarioSelector.ResolveScenarioId(BuildProjectModel(), @"beta").ShouldBe(s_Beta);
        }

        [Fact]
        public void ResolveScenarioId_Given_Id_Then_Matches()
        {
            ScenarioSelector.ResolveScenarioId(BuildProjectModel(), s_Beta.ToString()).ShouldBe(s_Beta);
        }

        [Fact]
        public void ResolveScenarioId_Given_UnknownName_Then_ThrowsNoMatch()
        {
            ScenarioSelectionException ex = Should.Throw<ScenarioSelectionException>(
                () => ScenarioSelector.ResolveScenarioId(BuildProjectModel(), @"Gamma"));

            ex.Failure.ShouldBe(ScenarioSelectionFailure.NoMatch);
            ex.Selector.ShouldBe(@"Gamma");
            ex.MatchCount.ShouldBe(0);
            ex.Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_NoScenarioMatchesSelector, @"Gamma"));
        }

        [Fact]
        public void ResolveScenarioId_Given_AmbiguousName_Then_ThrowsSeveralMatches()
        {
            ProjectModel projectModel = BuildProjectModel();
            projectModel.Nodes.Add(new ProjectScenarioNodeModel
            {
                Id = Guid.NewGuid(),
                ParentId = s_Root,
                NodeType = ProjectScenarioNodeType.File,
                Name = @"Beta",
            });

            ScenarioSelectionException ex = Should.Throw<ScenarioSelectionException>(
                () => ScenarioSelector.ResolveScenarioId(projectModel, @"Beta"));

            ex.Failure.ShouldBe(ScenarioSelectionFailure.SeveralMatches);
            ex.MatchCount.ShouldBe(2);
            ex.Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_SeveralScenariosMatchSelector, @"Beta", 2));
        }

        [Fact]
        public void ResolveScenarioId_Given_FolderName_Then_ThrowsNoMatch()
        {
            ProjectModel projectModel = BuildProjectModel();
            projectModel.Nodes.Add(new ProjectScenarioNodeModel
            {
                Id = Guid.NewGuid(),
                ParentId = s_Root,
                NodeType = ProjectScenarioNodeType.Folder,
                Name = @"Some Folder",
            });

            Should.Throw<ScenarioSelectionException>(
                () => ScenarioSelector.ResolveScenarioId(projectModel, @"Some Folder"))
                .Failure.ShouldBe(ScenarioSelectionFailure.NoMatch);
        }

        [Fact]
        public void ResolveScenarioId_Given_IdPrefix_Then_Matches()
        {
            ScenarioSelector.ResolveScenarioId(BuildProjectModel(), @"17c3e2d9").ShouldBe(s_Beta);
        }

        [Fact]
        public void ResolveScenarioId_Given_MinimalUniqueIdPrefix_Then_Matches()
        {
            ScenarioSelector.ResolveScenarioId(BuildProjectModel(), @"17c3").ShouldBe(s_Beta);
        }

        [Fact]
        public void ResolveScenarioId_Given_HyphenatedIdPrefix_Then_Matches()
        {
            ScenarioSelector.ResolveScenarioId(BuildProjectModel(), @"17c3e2d9-95a4").ShouldBe(s_Beta);
        }

        [Fact]
        public void ResolveScenarioId_Given_UppercaseIdPrefix_Then_Matches()
        {
            ScenarioSelector.ResolveScenarioId(BuildProjectModel(), @"17C3E2D9").ShouldBe(s_Beta);
        }

        [Fact]
        public void ResolveScenarioId_Given_AmbiguousIdPrefix_Then_ThrowsSeveralMatches()
        {
            ProjectModel projectModel = BuildProjectModel();
            projectModel.Nodes.Add(new ProjectScenarioNodeModel
            {
                Id = Guid.Parse(@"17c30000-0000-4000-8000-000000000000"),
                ParentId = s_Root,
                NodeType = ProjectScenarioNodeType.File,
                Name = @"Gamma",
            });

            Should.Throw<ScenarioSelectionException>(
                () => ScenarioSelector.ResolveScenarioId(projectModel, @"17c3"))
                .Failure.ShouldBe(ScenarioSelectionFailure.SeveralMatches);
        }

        [Fact]
        public void ResolveScenarioId_Given_TooShortIdPrefix_Then_ThrowsNoMatch()
        {
            // Three hex characters sit below the git-style abbreviation floor, so
            // this is treated as an unmatched name rather than an id.
            Should.Throw<ScenarioSelectionException>(
                () => ScenarioSelector.ResolveScenarioId(BuildProjectModel(), @"17c"))
                .Failure.ShouldBe(ScenarioSelectionFailure.NoMatch);
        }

        [Fact]
        public void ResolveScenarioId_Given_NameCollidingWithIdPrefix_Then_NameWins()
        {
            ProjectModel projectModel = BuildProjectModel();
            var namedId = Guid.NewGuid();
            projectModel.Nodes.Add(new ProjectScenarioNodeModel
            {
                Id = namedId,
                ParentId = s_Root,
                NodeType = ProjectScenarioNodeType.File,
                Name = @"cafe",
            });
            projectModel.Files.Add(new ProjectScenarioFileModel { NodeId = namedId, Scenario = new ProjectScenarioModel() });
            projectModel.Nodes.Add(new ProjectScenarioNodeModel
            {
                Id = Guid.Parse(@"cafe0000-0000-4000-8000-000000000000"),
                ParentId = s_Root,
                NodeType = ProjectScenarioNodeType.File,
                Name = @"Delta",
            });

            ScenarioSelector.ResolveScenarioId(projectModel, @"cafe").ShouldBe(namedId);
        }

        [Fact]
        public void ResolveScenarioId_Given_NodeWithoutScenarioFile_Then_ThrowsNoScenarioData()
        {
            ProjectModel projectModel = BuildProjectModel();
            projectModel.Files.RemoveAt(1);

            ScenarioSelectionException ex = Should.Throw<ScenarioSelectionException>(
                () => ScenarioSelector.ResolveScenarioId(projectModel, @"Beta"));

            ex.Failure.ShouldBe(ScenarioSelectionFailure.NoScenarioData);
            ex.MatchCount.ShouldBe(1);
            ex.Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ScenarioHasNoScenarioData, @"Beta"));
        }

        [Fact]
        public void ListScenarios_Given_Project_Then_ListsItsScenariosWithTheirPaths()
        {
            ProjectModel projectModel = BuildProjectModel();
            var folderId = Guid.NewGuid();
            var nestedId = Guid.NewGuid();
            projectModel.Nodes[1] = projectModel.Nodes[1] with { IsTracked = true };
            projectModel.Nodes.Add(new ProjectScenarioNodeModel
            {
                Id = folderId,
                ParentId = s_Root,
                NodeType = ProjectScenarioNodeType.Folder,
                Name = @"Experiments",
            });
            projectModel.Nodes.Add(new ProjectScenarioNodeModel
            {
                Id = nestedId,
                ParentId = folderId,
                NodeType = ProjectScenarioNodeType.File,
                Name = @"Gamma",
            });

            // Folders are not scenarios, so they are not listed themselves - they
            // prefix the path of each scenario in them.
            ScenarioSelector.ListScenarios(projectModel).ShouldBe(
            [
                new ScenarioSummary(@"Alpha", s_Alpha, IsTracked: false, IsCurrent: true),
                new ScenarioSummary(@"Beta", s_Beta, IsTracked: true, IsCurrent: false),
                new ScenarioSummary(@"Experiments/Gamma", nestedId, IsTracked: false, IsCurrent: false),
            ]);
        }

        [Fact]
        public void BuildNodePath_Given_NestedFolder_Then_PrefixesFolderName()
        {
            ProjectModel projectModel = BuildProjectModel();
            var folderId = Guid.NewGuid();
            var nestedId = Guid.NewGuid();
            projectModel.Nodes.Add(new ProjectScenarioNodeModel
            {
                Id = folderId,
                ParentId = s_Root,
                NodeType = ProjectScenarioNodeType.Folder,
                Name = @"Experiments",
            });
            ProjectScenarioNodeModel nested = new()
            {
                Id = nestedId,
                ParentId = folderId,
                NodeType = ProjectScenarioNodeType.File,
                Name = @"Gamma",
            };
            projectModel.Nodes.Add(nested);

            Dictionary<Guid, ProjectScenarioNodeModel> nodeLookup = projectModel.Nodes.ToDictionary(x => x.Id);

            ScenarioSelector.BuildNodePath(projectModel, nodeLookup, nested).ShouldBe(@"Experiments/Gamma");
        }

        [Fact]
        public void BuildNodePath_Given_ParentCycle_Then_Terminates()
        {
            var firstId = Guid.NewGuid();
            var secondId = Guid.NewGuid();
            ProjectScenarioNodeModel first = new()
            {
                Id = firstId,
                ParentId = secondId,
                NodeType = ProjectScenarioNodeType.File,
                Name = @"First",
            };
            ProjectScenarioNodeModel second = new()
            {
                Id = secondId,
                ParentId = firstId,
                NodeType = ProjectScenarioNodeType.Folder,
                Name = @"Second",
            };
            ProjectModel projectModel = new()
            {
                Root = s_Root,
                Nodes = [first, second],
            };

            Dictionary<Guid, ProjectScenarioNodeModel> nodeLookup = projectModel.Nodes.ToDictionary(x => x.Id);

            // A malformed parent cycle must not hang; the path just stops once a
            // node repeats.
            ScenarioSelector.BuildNodePath(projectModel, nodeLookup, first).ShouldBe(@"Second/First");
        }
    }
}
