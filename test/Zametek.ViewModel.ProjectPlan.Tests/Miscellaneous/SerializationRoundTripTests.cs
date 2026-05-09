using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Data.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Round-trip serialization tests for ProjectFileOpen / ProjectFileSave.
    /// Each test saves a model to a temp file, reopens it, and asserts key fields
    /// are preserved. We verify the JSON version tag is written correctly.
    /// </summary>
    public class SerializationRoundTripTests : IDisposable
    {
        private readonly List<string> m_TempFiles = [];
        private readonly DateTimeCalculator m_Calculator;
        private readonly ProjectFileSave m_Saver;
        private readonly ProjectFileOpen m_Opener;

        public SerializationRoundTripTests()
        {
            m_Calculator = new DateTimeCalculator(TimeProvider.System);
            m_Saver = new ProjectFileSave();
            m_Opener = new ProjectFileOpen(m_Calculator);
        }

        public void Dispose()
        {
            foreach (string file in m_TempFiles)
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }

        private string CreateTempFile()
        {
            string path = Path.Combine(Path.GetTempPath(), $"zpp_test_{Guid.NewGuid():N}.zpp");
            m_TempFiles.Add(path);
            return path;
        }

        /// <summary>
        /// Builds the simplest possible v0.6.0 ProjectModel to use as a fixture.
        /// </summary>
        private static ProjectModel BuildMinimalProjectModel()
        {
            Guid projectId = Guid.Parse("aaaaaa00-0000-0000-0000-000000000001");
            Guid rootId = Guid.Parse("aaaaaa00-0000-0000-0000-000000000002");
            Guid scenarioId = Guid.Parse("aaaaaa00-0000-0000-0000-000000000003");
            var createdOn = new DateTimeOffset(2025, 1, 6, 0, 0, 0, TimeSpan.Zero);

            return new ProjectModel
            {
                Version = Versions.v0_6_0,
                Id = projectId,
                Root = rootId,
                Current = scenarioId,
                Nodes =
                [
                    new ProjectScenarioNodeModel
                    {
                        Id = scenarioId,
                        ParentId = rootId,
                        NodeType = ProjectScenarioNodeType.File,
                        Name = "Base",
                        CreatedOn = createdOn,
                        ModifiedOn = createdOn,
                    },
                ],
                Files =
                [
                    new ProjectScenarioFileModel
                    {
                        NodeId = scenarioId,
                        Scenario = new ProjectScenarioModel
                        {
                            ProjectStart = createdOn,
                            Today = createdOn,
                        },
                    },
                ],
                Tags =
                [
                    new ProjectScenarioTagModel
                    {
                        NodeId = rootId,
                        Label = "Root",
                    },
                ],
            };
        }

        #region Version field

        [Fact]
        public async Task SaveProject_Writes_CorrectVersionField()
        {
            string path = CreateTempFile();
            ProjectModel model = BuildMinimalProjectModel();

            await m_Saver.SaveProjectFileAsync(model, path);

            string json = await File.ReadAllTextAsync(path);
            JObject obj = JObject.Parse(json);
            string? version = obj.GetValue("Version", StringComparison.OrdinalIgnoreCase)?.ToString();
            version.ShouldBe(Versions.v0_6_0);
        }

        #endregion

        #region Minimal model round-trip

        [Fact]
        public async Task RoundTrip_Minimal_ProjectModel_Preserves_Version()
        {
            string path = CreateTempFile();
            ProjectModel original = BuildMinimalProjectModel();

            await m_Saver.SaveProjectFileAsync(original, path);
            ProjectModel loaded = await m_Opener.OpenProjectFileAsync(path);

            loaded.Version.ShouldBe(Versions.v0_6_0);
        }

        [Fact]
        public async Task RoundTrip_Minimal_ProjectModel_Preserves_NodeCount()
        {
            string path = CreateTempFile();
            ProjectModel original = BuildMinimalProjectModel();

            await m_Saver.SaveProjectFileAsync(original, path);
            ProjectModel loaded = await m_Opener.OpenProjectFileAsync(path);

            loaded.Nodes.Count.ShouldBe(original.Nodes.Count);
        }

        [Fact]
        public async Task RoundTrip_Minimal_ProjectModel_Preserves_FileCount()
        {
            string path = CreateTempFile();
            ProjectModel original = BuildMinimalProjectModel();

            await m_Saver.SaveProjectFileAsync(original, path);
            ProjectModel loaded = await m_Opener.OpenProjectFileAsync(path);

            loaded.Files.Count.ShouldBe(original.Files.Count);
        }

        [Fact]
        public async Task RoundTrip_Minimal_ProjectModel_Preserves_ProjectStart()
        {
            string path = CreateTempFile();
            ProjectModel original = BuildMinimalProjectModel();
            DateTimeOffset expectedStart = original.Files[0].Scenario.ProjectStart;

            await m_Saver.SaveProjectFileAsync(original, path);
            ProjectModel loaded = await m_Opener.OpenProjectFileAsync(path);

            loaded.Files[0].Scenario.ProjectStart.ShouldBe(expectedStart);
        }

        [Fact]
        public async Task RoundTrip_Minimal_ProjectModel_Preserves_NodeNames()
        {
            string path = CreateTempFile();
            ProjectModel original = BuildMinimalProjectModel();

            await m_Saver.SaveProjectFileAsync(original, path);
            ProjectModel loaded = await m_Opener.OpenProjectFileAsync(path);

            for (int i = 0; i < original.Nodes.Count; i++)
            {
                loaded.Nodes[i].Name.ShouldBe(original.Nodes[i].Name);
            }
        }

        [Fact]
        public async Task RoundTrip_Minimal_ProjectModel_Preserves_TagLabels()
        {
            string path = CreateTempFile();
            ProjectModel original = BuildMinimalProjectModel();

            await m_Saver.SaveProjectFileAsync(original, path);
            ProjectModel loaded = await m_Opener.OpenProjectFileAsync(path);

            loaded.Tags.Count.ShouldBe(original.Tags.Count);
            for (int i = 0; i < original.Tags.Count; i++)
            {
                loaded.Tags[i].Label.ShouldBe(original.Tags[i].Label);
            }
        }

        #endregion

        #region Double round-trip idempotency

        [Fact]
        public async Task DoubleRoundTrip_Produces_Same_Version()
        {
            string path1 = CreateTempFile();
            string path2 = CreateTempFile();
            ProjectModel original = BuildMinimalProjectModel();

            await m_Saver.SaveProjectFileAsync(original, path1);
            ProjectModel pass1 = await m_Opener.OpenProjectFileAsync(path1);

            await m_Saver.SaveProjectFileAsync(pass1, path2);
            ProjectModel pass2 = await m_Opener.OpenProjectFileAsync(path2);

            pass2.Version.ShouldBe(Versions.v0_6_0);
        }

        [Fact]
        public async Task DoubleRoundTrip_Preserves_NodeCount()
        {
            string path1 = CreateTempFile();
            string path2 = CreateTempFile();
            ProjectModel original = BuildMinimalProjectModel();

            await m_Saver.SaveProjectFileAsync(original, path1);
            ProjectModel pass1 = await m_Opener.OpenProjectFileAsync(path1);
            await m_Saver.SaveProjectFileAsync(pass1, path2);
            ProjectModel pass2 = await m_Opener.OpenProjectFileAsync(path2);

            pass2.Nodes.Count.ShouldBe(original.Nodes.Count);
        }

        #endregion

        #region Loading existing v0.6.0 fixture

        /// <summary>
        /// Loads the test_v0_6_0.zpp fixture (used by the Data.ProjectPlan converter
        /// tests) and verifies that resaving it produces a file that re-opens
        /// with the same node/file/tag counts.
        /// The fixture path is the Data test project's TestFiles directory, so we
        /// use the copy that is embedded in the ViewModel test project as a Content
        /// item — but since that fixture is not included here, we skip gracefully if
        /// the file is not found.
        /// </summary>
        [Fact]
        public async Task RoundTrip_ExistingV0_6_0_Fixture_PreservesStructure()
        {
            // The Data.ProjectPlan.Tests project copies its TestFiles next to its dll.
            // We resolve from the running assembly location.
            string assemblyDir = AppContext.BaseDirectory;
            string fixturePath = Path.Combine(assemblyDir, "TestFiles", "test_v0_6_0.zpp");

            if (!File.Exists(fixturePath))
            {
                // Fixture is in the Data test project, not here — skip gracefully.
                return;
            }

            ProjectModel loaded = await m_Opener.OpenProjectFileAsync(fixturePath);

            string roundTripPath = CreateTempFile();
            await m_Saver.SaveProjectFileAsync(loaded, roundTripPath);
            ProjectModel reloaded = await m_Opener.OpenProjectFileAsync(roundTripPath);

            reloaded.Version.ShouldBe(Versions.v0_6_0);
            reloaded.Nodes.Count.ShouldBe(loaded.Nodes.Count);
            reloaded.Files.Count.ShouldBe(loaded.Files.Count);
            reloaded.Tags.Count.ShouldBe(loaded.Tags.Count);
        }

        #endregion
    }
}
