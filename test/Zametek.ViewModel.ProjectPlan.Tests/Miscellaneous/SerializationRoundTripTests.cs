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

        /// <summary>
        /// A v0.6.0 ProjectModel whose scenario carries persisted graph layout (node positions) and
        /// per-graph edge routing modes, for exercising their save/load round-trip.
        /// </summary>
        private static ProjectModel BuildProjectModelWithGraphLayout()
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
                            DisplaySettings = new ProjectScenarioDisplaySettingsModel
                            {
                                ArrowGraphEdgeRoutingMode = EdgeRoutingMode.Rectilinear,
                                VertexGraphEdgeRoutingMode = EdgeRoutingMode.Spline,
                            },
                            // Arrow event-node ids are negative (generated); vertex ids are positive
                            // (activity ids). Cover both.
                            ArrowGraphLayout = new GraphLayoutModel
                            {
                                Nodes =
                                [
                                    new NodeLayoutModel { Id = -1, X = 12.5, Y = -34.0 },
                                    new NodeLayoutModel { Id = -2, X = 100.0, Y = 200.0 },
                                ],
                            },
                            VertexGraphLayout = new GraphLayoutModel
                            {
                                Nodes =
                                [
                                    new NodeLayoutModel { Id = 1, X = 5.0, Y = 6.0 },
                                ],
                            },
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

        [Fact]
        public async Task RoundTrip_Preserves_EdgeRoutingModes()
        {
            string path = GetTempFile();
            ProjectModel original = BuildProjectModelWithGraphLayout();

            await m_Saver.SaveProjectFileAsync(original, path);
            ProjectModel loaded = await m_Opener.OpenProjectFileAsync(path);

            ProjectScenarioModel scenario = loaded.Files[0].Scenario;
            scenario.DisplaySettings.ArrowGraphEdgeRoutingMode.ShouldBe(EdgeRoutingMode.Rectilinear);
            scenario.DisplaySettings.VertexGraphEdgeRoutingMode.ShouldBe(EdgeRoutingMode.Spline);
        }

        [Fact]
        public async Task RoundTrip_Preserves_ArrowGraphLayout()
        {
            string path = GetTempFile();
            ProjectModel original = BuildProjectModelWithGraphLayout();

            await m_Saver.SaveProjectFileAsync(original, path);
            ProjectModel loaded = await m_Opener.OpenProjectFileAsync(path);

            List<NodeLayoutModel> nodes = loaded.Files[0].Scenario.ArrowGraphLayout.Nodes;
            nodes.Count.ShouldBe(2);
            nodes[0].Id.ShouldBe(-1);
            nodes[0].X.ShouldBe(12.5);
            nodes[0].Y.ShouldBe(-34.0);
            nodes[1].Id.ShouldBe(-2);
            nodes[1].X.ShouldBe(100.0);
            nodes[1].Y.ShouldBe(200.0);
        }

        [Fact]
        public async Task RoundTrip_Preserves_VertexGraphLayout()
        {
            string path = GetTempFile();
            ProjectModel original = BuildProjectModelWithGraphLayout();

            await m_Saver.SaveProjectFileAsync(original, path);
            ProjectModel loaded = await m_Opener.OpenProjectFileAsync(path);

            List<NodeLayoutModel> nodes = loaded.Files[0].Scenario.VertexGraphLayout.Nodes;
            nodes.Count.ShouldBe(1);
            nodes[0].Id.ShouldBe(1);
            nodes[0].X.ShouldBe(5.0);
            nodes[0].Y.ShouldBe(6.0);
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
