using Shouldly;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Unit tests for the internal Program helpers: scenario resolution by name
    /// or id, scenario path building for the listing, export file naming, and
    /// the option-combination validation that backs the usage-error exit code.
    /// </summary>
    public class ProgramHelperTests
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
            Program.ResolveScenarioId(BuildProjectModel(), @"beta").ShouldBe(s_Beta);
        }

        [Fact]
        public void ResolveScenarioId_Given_Id_Then_Matches()
        {
            Program.ResolveScenarioId(BuildProjectModel(), s_Beta.ToString()).ShouldBe(s_Beta);
        }

        [Fact]
        public void ResolveScenarioId_Given_UnknownName_Then_Throws()
        {
            Should.Throw<InvalidOperationException>(
                () => Program.ResolveScenarioId(BuildProjectModel(), @"Gamma"));
        }

        [Fact]
        public void ResolveScenarioId_Given_AmbiguousName_Then_Throws()
        {
            ProjectModel projectModel = BuildProjectModel();
            projectModel.Nodes.Add(new ProjectScenarioNodeModel
            {
                Id = Guid.NewGuid(),
                ParentId = s_Root,
                NodeType = ProjectScenarioNodeType.File,
                Name = @"Beta",
            });

            Should.Throw<InvalidOperationException>(
                () => Program.ResolveScenarioId(projectModel, @"Beta"));
        }

        [Fact]
        public void ResolveScenarioId_Given_FolderName_Then_Throws()
        {
            ProjectModel projectModel = BuildProjectModel();
            projectModel.Nodes.Add(new ProjectScenarioNodeModel
            {
                Id = Guid.NewGuid(),
                ParentId = s_Root,
                NodeType = ProjectScenarioNodeType.Folder,
                Name = @"Some Folder",
            });

            Should.Throw<InvalidOperationException>(
                () => Program.ResolveScenarioId(projectModel, @"Some Folder"));
        }

        [Fact]
        public void ResolveScenarioId_Given_IdPrefix_Then_Matches()
        {
            Program.ResolveScenarioId(BuildProjectModel(), @"17c3e2d9").ShouldBe(s_Beta);
        }

        [Fact]
        public void ResolveScenarioId_Given_MinimalUniqueIdPrefix_Then_Matches()
        {
            Program.ResolveScenarioId(BuildProjectModel(), @"17c3").ShouldBe(s_Beta);
        }

        [Fact]
        public void ResolveScenarioId_Given_HyphenatedIdPrefix_Then_Matches()
        {
            Program.ResolveScenarioId(BuildProjectModel(), @"17c3e2d9-95a4").ShouldBe(s_Beta);
        }

        [Fact]
        public void ResolveScenarioId_Given_UppercaseIdPrefix_Then_Matches()
        {
            Program.ResolveScenarioId(BuildProjectModel(), @"17C3E2D9").ShouldBe(s_Beta);
        }

        [Fact]
        public void ResolveScenarioId_Given_AmbiguousIdPrefix_Then_Throws()
        {
            ProjectModel projectModel = BuildProjectModel();
            projectModel.Nodes.Add(new ProjectScenarioNodeModel
            {
                Id = Guid.Parse(@"17c30000-0000-4000-8000-000000000000"),
                ParentId = s_Root,
                NodeType = ProjectScenarioNodeType.File,
                Name = @"Gamma",
            });

            Should.Throw<InvalidOperationException>(
                () => Program.ResolveScenarioId(projectModel, @"17c3"));
        }

        [Fact]
        public void ResolveScenarioId_Given_TooShortIdPrefix_Then_Throws()
        {
            // Three hex characters sit below the git-style abbreviation floor, so
            // this is treated as an unmatched name rather than an id.
            Should.Throw<InvalidOperationException>(
                () => Program.ResolveScenarioId(BuildProjectModel(), @"17c"));
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

            Program.ResolveScenarioId(projectModel, @"cafe").ShouldBe(namedId);
        }

        [Fact]
        public void ResolveScenarioId_Given_NodeWithoutScenarioFile_Then_Throws()
        {
            ProjectModel projectModel = BuildProjectModel();
            projectModel.Files.RemoveAt(1);

            Should.Throw<InvalidOperationException>(
                () => Program.ResolveScenarioId(projectModel, @"Beta"));
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

            Program.BuildNodePath(projectModel, nodeLookup, nested).ShouldBe(@"Experiments/Gamma");
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
            Program.BuildNodePath(projectModel, nodeLookup, first).ShouldBe(@"Second/First");
        }

        [Fact]
        public void BuildExportFilePath_Given_Format_Then_LowercasesExtension()
        {
            string expected = Path.Combine(@"exports", @"Project-gantt.png");

            Program.BuildExportFilePath(@"exports", @"Project", @"-gantt", @"Png").ShouldBe(expected);
        }

        [Fact]
        public void ValidateOptions_Given_InputAndImport_Then_UsageException()
        {
            Should.Throw<Program.UsageException>(
                () => Program.ValidateOptions(new Options
                {
                    InputFilename = @"a.zpp",
                    ImportFilename = @"b.xlsx",
                }));
        }

        [Fact]
        public void ValidateOptions_Given_ScenarioWithImport_Then_UsageException()
        {
            Should.Throw<Program.UsageException>(
                () => Program.ValidateOptions(new Options
                {
                    ImportFilename = @"b.xlsx",
                    Scenario = @"Beta",
                }));
        }

        [Fact]
        public void ValidateOptions_Given_ScenarioAndListScenarios_Then_UsageException()
        {
            Should.Throw<Program.UsageException>(
                () => Program.ValidateOptions(new Options
                {
                    InputFilename = @"a.zpp",
                    Scenario = @"Beta",
                    ListScenarios = true,
                }));
        }

        [Fact]
        public void ValidateOptions_Given_DirectoryWithoutSize_Then_UsageException()
        {
            Should.Throw<Program.UsageException>(
                () => Program.ValidateOptions(new Options
                {
                    InputFilename = @"a.zpp",
                    GanttDirectory = Path.GetTempPath(),
                }));
        }

        [Fact]
        public void ValidateOptions_Given_MissingExportDirectory_Then_Throws()
        {
            string missingDirectory = Path.Combine(Path.GetTempPath(), $@"zpp-missing-{Guid.NewGuid():N}");

            Should.Throw<InvalidOperationException>(
                () => Program.ValidateOptions(new Options
                {
                    InputFilename = @"a.zpp",
                    GanttDirectory = missingDirectory,
                    GanttSize = [800, 600],
                }));
        }

        [Fact]
        public void ValidateOptions_Given_ValidCombination_Then_DoesNotThrow()
        {
            Should.NotThrow(
                () => Program.ValidateOptions(new Options
                {
                    InputFilename = @"a.zpp",
                    GanttDirectory = Path.GetTempPath(),
                    GanttSize = [800, 600],
                }));
        }
    }
}
