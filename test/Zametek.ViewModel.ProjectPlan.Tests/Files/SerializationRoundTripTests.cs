using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Data.ProjectPlan;
using Zametek.Maths.Graphs;
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
            json["Version"]!.ToString().ShouldBe(Versions.v0_6_2);
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
        public async Task OpenProject_Given_AFileOlderThanV0_3_0_Then_ItsTimesAreReadInTheClocksTimeZone()
        {
            // A v0.2.1 file holds its project's start without an offset, so where it is comes from the clock the file is
            // opened by, not from the machine.
            TimeZoneInfo zone = TimeZoneInfo.CreateCustomTimeZone(@"Test +05:30", TimeSpan.FromMinutes(330), @"Test +05:30", @"Test +05:30");
            var opener = new ProjectFileOpen(new DateTimeCalculator(new FixedTimeProvider(new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero), zone)));
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes($@"{{ ""Version"": ""{Versions.v0_2_1}"", ""ProjectStart"": ""2015-01-05T08:00:00"" }}"));

            ProjectModel project = await opener.OpenProjectFileAsync(stream);

            DateTimeOffset projectStart = project.Files.ShouldHaveSingleItem().Scenario.ProjectStart;
            (projectStart.DateTime, projectStart.Offset).ShouldBe((new DateTime(2015, 1, 5, 8, 0, 0), TimeSpan.FromMinutes(330)));
        }

        [Fact]
        public async Task RoundTrip_Minimal_Preserves_Version()
        {
            ProjectModel original = BuildMinimalProjectModel();

            ProjectModel loaded = await RoundTripAsync(original);

            loaded.Version.ShouldBe(Versions.v0_6_2);
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

            loaded2.Version.ShouldBe(Versions.v0_6_2);
        }

        [Fact]
        public async Task DoubleRoundTrip_Preserves_NodeCount()
        {
            ProjectModel original = BuildMinimalProjectModel();

            ProjectModel loaded1 = await RoundTripAsync(original);
            ProjectModel loaded2 = await RoundTripAsync(loaded1);

            loaded2.Nodes.Count.ShouldBe(original.Nodes.Count);
        }

        [Fact]
        public async Task RoundTrip_Preserves_TheEstimatesOfADuration()
        {
            // Three activities, one in each shape that a plan can hold: all three
            // estimates, a most likely duration alone, and no estimates at all.
            ProjectModel original = WithActivities(
                BuildMinimalProjectModel(),
                new ActivityModel { Id = 1, Duration = 6, MostLikelyDuration = 5, OptimisticDuration = 3, PessimisticDuration = 12 },
                new ActivityModel { Id = 2, Duration = 4, MostLikelyDuration = 4 },
                new ActivityModel { Id = 3, Duration = 7 });

            using MemoryStream stream = await SaveToStreamAsync(original);
            JObject json = JObject.Parse(ReadAllText(stream));
            var written = (JArray)json["Files"]![0]!["Scenario"]!["DependentActivities"]!;

            // A member that is null is not written, so an activity with no estimates
            // looks in the file as it did in the version before them.
            written[0]["Activity"]!["MostLikelyDuration"]!.Value<int>().ShouldBe(5);
            written[0]["Activity"]!["OptimisticDuration"]!.Value<int>().ShouldBe(3);
            written[0]["Activity"]!["PessimisticDuration"]!.Value<int>().ShouldBe(12);
            written[1]["Activity"]!["OptimisticDuration"].ShouldBeNull();
            written[2]["Activity"]!["MostLikelyDuration"].ShouldBeNull();

            stream.Position = 0;
            ProjectModel loaded = await m_Opener.OpenProjectFileAsync(stream);

            List<ActivityModel> activities = [.. loaded.Files[0].Scenario.DependentActivities.Select(x => x.Activity)];
            (activities[0].Duration, activities[0].MostLikelyDuration, activities[0].OptimisticDuration, activities[0].PessimisticDuration)
                .ShouldBe((6, 5, 3, 12));
            (activities[1].Duration, activities[1].MostLikelyDuration, activities[1].OptimisticDuration, activities[1].PessimisticDuration)
                .ShouldBe((4, 4, (int?)null, (int?)null));
            (activities[2].Duration, activities[2].MostLikelyDuration, activities[2].OptimisticDuration, activities[2].PessimisticDuration)
                .ShouldBe((7, (int?)null, (int?)null, (int?)null));
        }

        [Fact]
        public async Task OpenProject_Given_AV0_6_1File_Then_ItOpensAsV0_6_2WithTheSameDurationsAndNoEstimates()
        {
            // The file of every plan that exists today. It has no estimates, and its Duration
            // is the duration that was scheduled, which must stay the duration.
            ProjectModel current = WithActivities(
                BuildMinimalProjectModel(),
                new ActivityModel { Id = 1, Duration = 6 },
                new ActivityModel { Id = 2, Duration = 0 });
            Data.ProjectPlan.v0_6_1.ProjectModel stored = new VersionMapper().FromCurrentToV0_6_1(current);
            stored.Version.ShouldBe(Versions.v0_6_1);

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(stored)));
            ProjectModel loaded = await m_Opener.OpenProjectFileAsync(stream);

            loaded.Version.ShouldBe(Versions.v0_6_2);
            List<ActivityModel> activities = [.. loaded.Files[0].Scenario.DependentActivities.Select(x => x.Activity)];
            activities.Select(x => x.Duration).ShouldBe([6, 0]);
            activities.ShouldAllBe(x => x.MostLikelyDuration == null
                && x.OptimisticDuration == null
                && x.PessimisticDuration == null);
        }

        #endregion

        #region The simulation

        // A scenario that holds the settings of a simulation and what it found: every member has a value of its own,
        // so that one that is not carried across shows.
        private static ProjectModel BuildProjectModelWithASimulation()
        {
            ProjectModel project = BuildMinimalProjectModel();

            return project with
            {
                Files =
                [
                    project.Files[0] with
                    {
                        Scenario = project.Files[0].Scenario with
                        {
                            SimulationSettings = new SimulationSettingsModel
                            {
                                Iterations = 1234,
                                Distribution = DurationDistribution.ModifiedBetaPert,
                                ModifiedBetaShape = 6.5,
                                Seed = ulong.MaxValue - 3,
                                UseRandomSeed = false,
                                TargetFinishTime = 77,
                            },
                            SimulationSummary = new SimulationSummaryModel
                            {
                                IterationCount = 1234,

                                // The run that made the result used other values than the settings have now.
                                Seed = ulong.MaxValue - 9,
                                Distribution = DurationDistribution.Triangular,
                                ModifiedBetaShape = null,
                                ValidIterationCount = 1200,
                                FailedIterationCount = 34,
                                PlannedFinishTime = 60,
                                TargetFinishTime = 77,
                                TargetProbability = 0.875,
                                Mean = 66.25,
                                StandardDeviation = 4.5,
                                Minimum = 50,
                                Maximum = 90,
                                Percentiles = [new SimulationPercentileModel { Percentile = 50, FinishTime = 66 }, new SimulationPercentileModel { Percentile = 90, FinishTime = 73 }],
                                BinWidth = 2,
                                Bins =
                                [
                                    new SimulationHistogramBinModel { FirstFinishTime = 50, LastFinishTime = 51, Count = 10, CumulativeShare = 0.25, PathGroupWeights = [6.5, 3.5, 0.0] },
                                    new SimulationHistogramBinModel { FirstFinishTime = 52, LastFinishTime = 53, Count = 30, CumulativeShare = 1.0, PathGroupWeights = [20.0, 10.0, 0.0] },
                                ],
                                Paths =
                                [
                                    new SimulationPathModel { Rank = 1, Label = "Design > Build ~> Test", ShortLabel = "Design > Build ~> Test", IdLabel = "1 > 3 ~> 7", HasResourceLinks = true, Weight = 700.5, Share = 0.5837 },
                                    new SimulationPathModel { Rank = 2, Label = "Design > Test", ShortLabel = "Design > Test", IdLabel = "1 > 7", HasResourceLinks = false, Weight = 300.0, Share = 0.25 },
                                ],
                                ChartPathCount = 2,
                                OtherPathsWeight = 199.5,
                                Activities =
                                [
                                    new SimulationActivityCriticalityModel { ActivityId = 1, Name = "Design", CriticalIterationCount = 1200, CriticalityIndex = 1.0 },
                                    new SimulationActivityCriticalityModel { ActivityId = 3, Name = "Build", CriticalIterationCount = 700, CriticalityIndex = 0.5833 },
                                ],
                            },
                        },
                    },
                ],
            };
        }

        [Fact]
        public async Task RoundTrip_Preserves_TheSettingsOfTheSimulation()
        {
            ProjectModel original = BuildProjectModelWithASimulation();

            ProjectModel loaded = await RoundTripAsync(original);

            loaded.Files[0].Scenario.SimulationSettings.ShouldBe(original.Files[0].Scenario.SimulationSettings);
        }

        [Fact]
        public async Task RoundTrip_Preserves_WhatTheSimulationFound()
        {
            ProjectModel original = BuildProjectModelWithASimulation();
            SimulationSummaryModel expected = original.Files[0].Scenario.SimulationSummary!;

            ProjectModel loaded = await RoundTripAsync(original);

            SimulationSummaryModel summary = loaded.Files[0].Scenario.SimulationSummary.ShouldNotBeNull();
            (summary.IterationCount, summary.ValidIterationCount, summary.FailedIterationCount, summary.PlannedFinishTime, summary.TargetFinishTime, summary.TargetProbability)
                .ShouldBe((1234, 1200, 34, 60, 77, 0.875));
            (summary.Seed, summary.Distribution, summary.ModifiedBetaShape)
                .ShouldBe((ulong.MaxValue - 9, DurationDistribution.Triangular, (double?)null));
            (summary.Mean, summary.StandardDeviation, summary.Minimum, summary.Maximum, summary.BinWidth, summary.ChartPathCount, summary.OtherPathsWeight)
                .ShouldBe((66.25, 4.5, 50, 90, 2, 2, 199.5));
            summary.Percentiles.ShouldBe(expected.Percentiles);
            summary.Paths.ShouldBe(expected.Paths);
            summary.Activities.ShouldBe(expected.Activities);
            summary.Bins.Select(x => (x.FirstFinishTime, x.LastFinishTime, x.Count, x.CumulativeShare))
                .ShouldBe(expected.Bins.Select(x => (x.FirstFinishTime, x.LastFinishTime, x.Count, x.CumulativeShare)));
            summary.Bins.Select(x => string.Join(",", x.PathGroupWeights)).ShouldBe(expected.Bins.Select(x => string.Join(",", x.PathGroupWeights)));

            // The critical paths as activity ids are the ones the Critical paths table shows, and a file holds them.
            summary.Paths.Select(x => x.IdLabel).ShouldBe(["1 > 3 ~> 7", "1 > 7"]);
        }

        [Fact]
        public async Task RoundTrip_Given_NoSimulationResult_Then_TheFileHoldsNoneAndTheSettingsAreTheDefaults()
        {
            using MemoryStream stream = await SaveToStreamAsync(BuildMinimalProjectModel());

            // A result that is null is not written.
            JObject json = JObject.Parse(ReadAllText(stream));
            json["Files"]![0]!["Scenario"]!["SimulationSummary"].ShouldBeNull();

            stream.Position = 0;
            ProjectModel loaded = await m_Opener.OpenProjectFileAsync(stream);

            ProjectScenarioModel scenario = loaded.Files[0].Scenario;
            scenario.SimulationSummary.ShouldBeNull();
            scenario.SimulationSettings.ShouldBe(new SimulationSettingsModel());
        }

        [Fact]
        public async Task SaveProject_Given_ASimulation_Then_TheSeedIsAFullWidthNumberInTheFile()
        {
            // The seed is 64 bits wide, and a reader that holds numbers as doubles would lose the end of it. It is
            // written as the number it is, and read as the number it is.
            ProjectModel original = BuildProjectModelWithASimulation();

            using MemoryStream stream = await SaveToStreamAsync(original);

            JObject json = JObject.Parse(ReadAllText(stream));
            json["Files"]![0]!["Scenario"]!["SimulationSettings"]!["Seed"]!.ToString().ShouldBe((ulong.MaxValue - 3).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        #endregion

        #region Helpers for the estimates

        private static ProjectModel WithActivities(ProjectModel project, params ActivityModel[] activities) =>
            project with
            {
                Files =
                [
                    project.Files[0] with
                    {
                        Scenario = project.Files[0].Scenario with
                        {
                            DependentActivities = [.. activities.Select(x => new DependentActivityModel { Activity = x })],
                        },
                    },
                ],
            };

        #endregion
    }
}
