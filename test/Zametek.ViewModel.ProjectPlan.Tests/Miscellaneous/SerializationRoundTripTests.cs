using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Data.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    public class SerializationRoundTripTests
    {
        #region Fields

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

        #region Helpers

        // Saves the project into a stream of its own, rewound so that it can be read straight back.
        private async Task<MemoryStream> SaveToStreamAsync(ProjectModel model)
        {
            var stream = new MemoryStream();
            await m_Saver.SaveProjectFileAsync(model, stream);
            stream.Position = 0;
            return stream;
        }

        private async Task<ProjectModel> RoundTripAsync(ProjectModel model)
        {
            using MemoryStream stream = await SaveToStreamAsync(model);
            return await m_Opener.OpenProjectFileAsync(stream);
        }

        private static string ReadAllText(MemoryStream stream) => Encoding.UTF8.GetString(stream.ToArray());

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
            ProjectModel model = BuildMinimalProjectModel();

            using MemoryStream stream = await SaveToStreamAsync(model);

            JObject json = JObject.Parse(ReadAllText(stream));
            json["Version"]!.ToString().ShouldBe(Versions.v0_6_1);
        }

        [Fact]
        public async Task SavedFile_IsValidJson()
        {
            ProjectModel model = BuildMinimalProjectModel();

            using MemoryStream stream = await SaveToStreamAsync(model);

            string content = ReadAllText(stream);
            Should.NotThrow(() => JObject.Parse(content));
        }

        [Fact]
        public async Task SavedFile_EndsLinesWithLf()
        {
            // On every platform, so that the same plan saves to the same bytes on Windows and on Linux. Windows' own
            // line end is "\r\n", which is where this test has teeth.
            ProjectModel model = BuildMinimalProjectModel();

            using MemoryStream stream = await SaveToStreamAsync(model);

            string content = ReadAllText(stream);
            content.ShouldContain(NewLineHelper.NewLine);
            content.ShouldNotContain(NewLineHelper.CarriageReturn);
        }

        [Fact]
        public async Task SaveProject_LeavesTheStreamOpen()
        {
            // The caller owns the stream: a file to close, a buffer to read back, a response body to send.
            using var stream = new MemoryStream();

            await m_Saver.SaveProjectFileAsync(BuildMinimalProjectModel(), stream);

            stream.CanWrite.ShouldBeTrue();
            stream.Length.ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task OpenProject_LeavesTheStreamOpen()
        {
            using MemoryStream stream = await SaveToStreamAsync(BuildMinimalProjectModel());

            await m_Opener.OpenProjectFileAsync(stream);

            stream.CanRead.ShouldBeTrue();
        }

        [Fact]
        public async Task OpenProject_Given_UnknownVersion_Then_TheVersionIsNamed()
        {
            // A stream has no file name to report, so the message names what could not be read instead.
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(@"{ ""Version"": ""v9.9.9"" }"));

            InvalidDataException exception = await Should.ThrowAsync<InvalidDataException>(() => m_Opener.OpenProjectFileAsync(stream));

            exception.Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_UnknownProjectFileVersion, @"v9.9.9"));
        }

        [Fact]
        public async Task RoundTrip_Minimal_Preserves_Version()
        {
            ProjectModel original = BuildMinimalProjectModel();

            ProjectModel loaded = await RoundTripAsync(original);

            loaded.Version.ShouldBe(Versions.v0_6_1);
        }

        [Fact]
        public async Task RoundTrip_Minimal_Preserves_NodeCount()
        {
            ProjectModel original = BuildMinimalProjectModel();

            ProjectModel loaded = await RoundTripAsync(original);

            loaded.Nodes.Count.ShouldBe(original.Nodes.Count);
        }

        [Fact]
        public async Task RoundTrip_Minimal_Preserves_FileCount()
        {
            ProjectModel original = BuildMinimalProjectModel();

            ProjectModel loaded = await RoundTripAsync(original);

            loaded.Files.Count.ShouldBe(original.Files.Count);
        }

        [Fact]
        public async Task RoundTrip_Minimal_Preserves_ProjectStart()
        {
            ProjectModel original = BuildMinimalProjectModel();
            DateTimeOffset expectedStart = original.Files[0].Scenario.ProjectStart;

            ProjectModel loaded = await RoundTripAsync(original);

            loaded.Files[0].Scenario.ProjectStart.ShouldBe(expectedStart);
        }

        [Fact]
        public async Task RoundTrip_Minimal_Preserves_NodeNames()
        {
            ProjectModel original = BuildMinimalProjectModel();
            string expectedName = original.Nodes[0].Name;

            ProjectModel loaded = await RoundTripAsync(original);

            loaded.Nodes[0].Name.ShouldBe(expectedName);
        }

        [Fact]
        public async Task RoundTrip_Preserves_EdgeRoutingModes()
        {
            ProjectModel original = BuildProjectModelWithGraphLayout();

            ProjectModel loaded = await RoundTripAsync(original);

            ProjectScenarioModel scenario = loaded.Files[0].Scenario;
            scenario.DisplaySettings.ArrowGraphEdgeRoutingMode.ShouldBe(EdgeRoutingMode.Rectilinear);
            scenario.DisplaySettings.VertexGraphEdgeRoutingMode.ShouldBe(EdgeRoutingMode.Spline);
        }

        [Fact]
        public async Task RoundTrip_Preserves_ArrowGraphLayout()
        {
            ProjectModel original = BuildProjectModelWithGraphLayout();

            ProjectModel loaded = await RoundTripAsync(original);

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
            ProjectModel original = BuildProjectModelWithGraphLayout();

            ProjectModel loaded = await RoundTripAsync(original);

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
            ProjectModel original = BuildMinimalProjectModel();

            ProjectModel loaded1 = await RoundTripAsync(original);
            ProjectModel loaded2 = await RoundTripAsync(loaded1);

            loaded2.Version.ShouldBe(Versions.v0_6_1);
        }

        [Fact]
        public async Task DoubleRoundTrip_Preserves_NodeCount()
        {
            ProjectModel original = BuildMinimalProjectModel();

            ProjectModel loaded1 = await RoundTripAsync(original);
            ProjectModel loaded2 = await RoundTripAsync(loaded1);

            loaded2.Nodes.Count.ShouldBe(original.Nodes.Count);
        }

        #endregion
    }
}
