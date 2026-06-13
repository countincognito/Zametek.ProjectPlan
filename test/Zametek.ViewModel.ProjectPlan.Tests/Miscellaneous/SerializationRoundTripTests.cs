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
    public class SerializationRoundTripTests
        : IDisposable
    {
        #region Fields

        private readonly List<string> m_TempFiles = [];
        private readonly DateTimeCalculator m_Calculator;
        private readonly ProjectFileSave m_Saver;
        private readonly ProjectFileOpen m_Opener;

        #endregion

        #region Ctors

        public SerializationRoundTripTests()
        {
            m_Calculator = new DateTimeCalculator(TimeProvider.System);
            m_Saver = new ProjectFileSave();
            m_Opener = new ProjectFileOpen(m_Calculator);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            foreach (string path in m_TempFiles)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        #endregion

        #region Helpers

        private string GetTempFile()
        {
            string path = Path.GetTempFileName() + ".zpp";
            m_TempFiles.Add(path);
            return path;
        }

        /// <summary>
        /// Builds the simplest possible v0.6.0 ProjectModel to use as a fixture.
        /// </summary>
        private static ProjectModel BuildMinimalProjectModel()
        {
            var nodeId = Guid.NewGuid();
            var rootId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;

            return new ProjectModel
            {
                Version = Versions.v0_6_0,
                Id = Guid.NewGuid(),
                Root = rootId,
                Current = nodeId,
                Nodes =
                [
                    new ProjectScenarioNodeModel
                    {
                        Id = nodeId,
                        ParentId = rootId,
                        NodeType = ProjectScenarioNodeType.File,
                        Name = "Scenario 1",
                        CreatedOn = now,
                        ModifiedOn = now,
                    },
                ],
                Files =
                [
                    new ProjectScenarioFileModel
                    {
                        NodeId = nodeId,
                        Scenario = new ProjectScenarioModel
                        {
                            ProjectStart = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
                            Today = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero),
                        },
                    },
                ],
                Tags = [],
            };
        }

        #endregion

        #region Tests

        [Fact]
        public async Task SaveProject_Writes_CorrectVersionField()
        {
            string path = GetTempFile();
            ProjectModel model = BuildMinimalProjectModel();

            await m_Saver.SaveProjectFileAsync(model, path);

            string content = await File.ReadAllTextAsync(path);
            JObject json = JObject.Parse(content);
            json["Version"]!.ToString().ShouldBe(Versions.v0_6_1);
        }

        [Fact]
        public async Task SavedFile_IsValidJson()
        {
            string path = GetTempFile();
            ProjectModel model = BuildMinimalProjectModel();

            await m_Saver.SaveProjectFileAsync(model, path);

            string content = await File.ReadAllTextAsync(path);
            Should.NotThrow(() => JObject.Parse(content));
        }

        [Fact]
        public async Task RoundTrip_Minimal_Preserves_Version()
        {
            string path = GetTempFile();
            ProjectModel original = BuildMinimalProjectModel();

            await m_Saver.SaveProjectFileAsync(original, path);
            ProjectModel loaded = await m_Opener.OpenProjectFileAsync(path);

            loaded.Version.ShouldBe(Versions.v0_6_1);
        }

        [Fact]
        public async Task RoundTrip_Minimal_Preserves_NodeCount()
        {
            string path = GetTempFile();
            ProjectModel original = BuildMinimalProjectModel();

            await m_Saver.SaveProjectFileAsync(original, path);
            ProjectModel loaded = await m_Opener.OpenProjectFileAsync(path);

            loaded.Nodes.Count.ShouldBe(original.Nodes.Count);
        }

        [Fact]
        public async Task RoundTrip_Minimal_Preserves_FileCount()
        {
            string path = GetTempFile();
            ProjectModel original = BuildMinimalProjectModel();

            await m_Saver.SaveProjectFileAsync(original, path);
            ProjectModel loaded = await m_Opener.OpenProjectFileAsync(path);

            loaded.Files.Count.ShouldBe(original.Files.Count);
        }

        [Fact]
        public async Task RoundTrip_Minimal_Preserves_ProjectStart()
        {
            string path = GetTempFile();
            ProjectModel original = BuildMinimalProjectModel();
            DateTimeOffset expectedStart = original.Files[0].Scenario.ProjectStart;

            await m_Saver.SaveProjectFileAsync(original, path);
            ProjectModel loaded = await m_Opener.OpenProjectFileAsync(path);

            loaded.Files[0].Scenario.ProjectStart.ShouldBe(expectedStart);
        }

        [Fact]
        public async Task RoundTrip_Minimal_Preserves_NodeNames()
        {
            string path = GetTempFile();
            ProjectModel original = BuildMinimalProjectModel();
            string expectedName = original.Nodes[0].Name;

            await m_Saver.SaveProjectFileAsync(original, path);
            ProjectModel loaded = await m_Opener.OpenProjectFileAsync(path);

            loaded.Nodes[0].Name.ShouldBe(expectedName);
        }

        #endregion

        #region Double round-trip idempotency

        [Fact]
        public async Task DoubleRoundTrip_Produces_Same_Version()
        {
            string path1 = GetTempFile();
            string path2 = GetTempFile();
            ProjectModel original = BuildMinimalProjectModel();

            await m_Saver.SaveProjectFileAsync(original, path1);
            ProjectModel loaded1 = await m_Opener.OpenProjectFileAsync(path1);

            await m_Saver.SaveProjectFileAsync(loaded1, path2);
            ProjectModel loaded2 = await m_Opener.OpenProjectFileAsync(path2);

            loaded2.Version.ShouldBe(Versions.v0_6_1);
        }

        [Fact]
        public async Task DoubleRoundTrip_Preserves_NodeCount()
        {
            string path1 = GetTempFile();
            string path2 = GetTempFile();
            ProjectModel original = BuildMinimalProjectModel();

            await m_Saver.SaveProjectFileAsync(original, path1);
            ProjectModel loaded1 = await m_Opener.OpenProjectFileAsync(path1);

            await m_Saver.SaveProjectFileAsync(loaded1, path2);
            ProjectModel loaded2 = await m_Opener.OpenProjectFileAsync(path2);

            loaded2.Nodes.Count.ShouldBe(original.Nodes.Count);
        }

        #endregion
    }
}
