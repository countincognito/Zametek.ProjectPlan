using Shouldly;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Engine.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for what zpp serve answers when a request asks for a simulation of the schedule: its statistics in the answer,
    /// its chart and CSV files among the outputs, the limit on its iterations, and that the time of a job stops it.
    /// </summary>
    public class ProjectEndpointsSimulationTests
        : IClassFixture<EngineFixture>
    {
        private readonly EngineFixture m_Engine;

        public ProjectEndpointsSimulationTests(EngineFixture engine)
        {
            m_Engine = engine;
        }

        private static byte[] Estimated()
        {
            return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, @"Assets", @"estimated.zpp"));
        }

        private static async Task<HttpResponseMessage> PostAsync(
            RunningServer server,
            string optionsJson,
            string query = @"",
            string? accept = null)
        {
            using var content = RunningServer.CompileContent(Estimated(), @"estimated.zpp");
            content.Add(new StringContent(optionsJson, System.Text.Encoding.UTF8, @"application/json"), @"options");
            using var request = new HttpRequestMessage(HttpMethod.Post, $@"/v1/projects/compile{query}") { Content = content };

            if (accept is not null)
            {
                request.Headers.TryAddWithoutValidation(@"Accept", accept);
            }

            return await server.Client.SendAsync(request);
        }

        [Fact]
        public async Task Compile_Given_ASimulation_Then_AnswersWithItsStatisticsBetweenTheMetricsAndTheOutputs()
        {
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner);

            using HttpResponseMessage response = await PostAsync(server, @"{""simulation"":{""iterations"":1000,""seed"":""77"",""targetFinishDays"":30}}");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            string json = await response.Content.ReadAsStringAsync();
            using JsonDocument document = JsonDocument.Parse(json);
            document.RootElement.EnumerateObject().Select(x => x.Name).ShouldBe([@"metrics", @"simulation", @"outputs"]);

            JsonElement simulation = document.RootElement.GetProperty(@"simulation");
            simulation.EnumerateObject().Select(x => x.Name).ShouldBe(
            [
                @"iterations", @"validIterations", @"failedIterations", @"seed", @"distribution", @"modifiedBetaShape",
                @"plannedFinishDays", @"plannedFinishDate", @"meanDays", @"standardDeviationDays",
                @"earliestFinishDays", @"earliestFinishDate", @"latestFinishDays", @"latestFinishDate",
                @"percentiles", @"targetFinishDays", @"targetFinishDate", @"targetProbability",
            ]);

            // The seed is text, the distribution a name, and a shape value is null for a distribution that has none.
            simulation.GetProperty(@"seed").ValueKind.ShouldBe(JsonValueKind.String);
            simulation.GetProperty(@"seed").GetString().ShouldBe(@"77");
            simulation.GetProperty(@"distribution").GetString().ShouldBe(@"betaPert");
            simulation.GetProperty(@"modifiedBetaShape").ValueKind.ShouldBe(JsonValueKind.Null);
            simulation.GetProperty(@"iterations").GetInt32().ShouldBe(1000);
            simulation.GetProperty(@"validIterations").GetInt32().ShouldBe(1000);
            simulation.GetProperty(@"targetFinishDays").GetInt32().ShouldBe(30);
            simulation.GetProperty(@"targetFinishDate").ValueKind.ShouldBe(JsonValueKind.String);
            simulation.GetProperty(@"percentiles").GetArrayLength().ShouldBe(9);

            // It is the plan as it stands that the project's own metrics say.
            document.RootElement.GetProperty(@"metrics").GetProperty(@"networkDuration").GetInt32()
                .ShouldBe(simulation.GetProperty(@"plannedFinishDays").GetInt32());
        }

        [Fact]
        public async Task Compile_Given_NoSimulation_Then_TheAnswerHasNone()
        {
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner);

            using HttpResponseMessage response = await PostAsync(server, @"{}");

            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            document.RootElement.EnumerateObject().Select(x => x.Name).ShouldBe([@"metrics", @"outputs"]);
        }

        [Fact]
        public async Task Compile_Given_TheOutputsOfASimulation_Then_AnswersWithEachNamedAsZppNamesIt()
        {
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner);

            using HttpResponseMessage response = await PostAsync(
                server,
                """
                {
                    "simulation": { "iterations": 300 },
                    "outputs": {
                        "simulationChart": { "format": "png", "width": 400, "height": 300 },
                        "simulationStatistics": {}, "simulationHistogram": {}, "simulationPaths": {},
                        "simulationActivities": {}, "simulationIterations": {}
                    }
                }
                """);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            CompileResponse compiled = await RunningServer.ReadAsync<CompileResponse>(response);
            compiled.Simulation.ShouldNotBeNull().ValidIterations.ShouldBe(300);
            compiled.Outputs.Select(x => x.Kind).ShouldBe(
            [
                JobOutput.SimulationChart, JobOutput.SimulationStatistics, JobOutput.SimulationHistogram,
                JobOutput.SimulationPaths, JobOutput.SimulationActivities, JobOutput.SimulationIterations,
            ]);
            compiled.Outputs.Select(x => x.FileName).ShouldBe(
            [
                @"estimated-simulation.png", @"estimated-simulation-statistics.csv", @"estimated-simulation-histogram.csv",
                @"estimated-simulation-paths.csv", @"estimated-simulation-activities.csv", @"estimated-simulation-iterations.csv",
            ]);
            compiled.Outputs.Select(x => x.ContentType).ShouldBe([@"image/png", .. Enumerable.Repeat(@"text/csv", 5)]);

            // The CSV files are those of the panel: UTF-8 with a byte order mark.
            compiled.Outputs[1].Content.ShouldNotBeNull().Take(3).ShouldBe([(byte)0xEF, (byte)0xBB, (byte)0xBF]);
            compiled.Outputs[5].Content.ShouldNotBeNull().Length.ShouldBeGreaterThan(300);
        }

        [Fact]
        public async Task Compile_Given_TheSameSimulationTwice_Then_TheSameAnswerByteForByte()
        {
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner);
            const string options = """{"simulation":{"iterations":2000,"seed":"5","distribution":"triangular","targetFinishDays":28},"outputs":{"simulationStatistics":{},"simulationIterations":{}}}""";

            using HttpResponseMessage first = await PostAsync(server, options);
            using HttpResponseMessage second = await PostAsync(server, options);

            (await second.Content.ReadAsByteArrayAsync()).ShouldBe(await first.Content.ReadAsByteArrayAsync());
        }

        [Fact]
        public async Task Compile_Given_AnotherSeed_Then_AnotherSample()
        {
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner);

            using HttpResponseMessage one = await PostAsync(server, @"{""simulation"":{""iterations"":2000,""seed"":""1""}}");
            using HttpResponseMessage two = await PostAsync(server, @"{""simulation"":{""iterations"":2000,""seed"":""2""}}");

            (await RunningServer.ReadAsync<CompileResponse>(two)).Simulation.ShouldNotBe((await RunningServer.ReadAsync<CompileResponse>(one)).Simulation);
        }

        [Fact]
        public async Task Compile_Given_AZip_Then_HasTheFilesOfTheSimulationAndItsStatisticsInResultJson()
        {
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner);

            using HttpResponseMessage response = await PostAsync(
                server,
                @"{""simulation"":{""iterations"":200},""outputs"":{""simulationStatistics"":{},""simulationPaths"":{}}}",
                accept: @"application/zip");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            using var archive = new ZipArchive(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
            archive.Entries.Select(x => x.FullName).ShouldBe(
                [@"estimated-simulation-statistics.csv", @"estimated-simulation-paths.csv", @"result.json"]);

            using Stream result = archive.GetEntry(@"result.json")!.Open();
            using JsonDocument document = JsonDocument.Parse(result);
            document.RootElement.GetProperty(@"simulation").GetProperty(@"validIterations").GetInt32().ShouldBe(200);
        }

        [Fact]
        public async Task Compile_Given_MoreIterationsThanTheLimit_Then_422WithTheLimitNamed()
        {
            await using RunningServer server = await RunningServer.StartAsync(
                m_Engine.JobRunner,
                new ServeSettings { Limits = new ServeLimits { MaxSimulationIterations = 500 } });

            using HttpResponseMessage within = await PostAsync(server, @"{""simulation"":{""iterations"":500}}");
            using HttpResponseMessage over = await PostAsync(server, @"{""simulation"":{""iterations"":501}}");

            within.StatusCode.ShouldBe(HttpStatusCode.OK);
            over.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            ProblemResponse problem = await RunningServer.ReadAsync<ProblemResponse>(over);
            ProblemError error = problem.Errors.ShouldNotBeNull().ShouldHaveSingleItem();
            error.Pointer.ShouldBe(@"#/options/simulation/iterations");
            error.Code.ShouldBe(@"outOfRange");
            error.Detail.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeErrorNumberOutOfRange, 1, 500));
        }

        [Fact]
        public async Task Compile_Given_NoIterationsAndALimitBelowTheDefault_Then_TheLimitIsRun()
        {
            await using RunningServer server = await RunningServer.StartAsync(
                m_Engine.JobRunner,
                new ServeSettings { Limits = new ServeLimits { MaxSimulationIterations = 700 } });

            using HttpResponseMessage response = await PostAsync(server, @"{""simulation"":{}}");

            (await RunningServer.ReadAsync<CompileResponse>(response)).Simulation.ShouldNotBeNull().Iterations.ShouldBe(700);
        }

        [Fact]
        public async Task Compile_Given_AnOutputOfASimulationAndNoSimulation_Then_422AtTheOutput()
        {
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner);

            using HttpResponseMessage response = await PostAsync(server, @"{""outputs"":{""simulationPaths"":{}}}");

            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            ProblemResponse problem = await RunningServer.ReadAsync<ProblemResponse>(response);
            ProblemError error = problem.Errors.ShouldNotBeNull().ShouldHaveSingleItem();
            error.Pointer.ShouldBe(@"#/options/outputs/simulationPaths");
            error.Code.ShouldBe(@"notAllowedWithoutSimulation");
        }

        [Fact]
        public async Task Compile_Given_ASimulationThatTheTimeOfTheJobStops_Then_503JobTimeoutAndItEndsBeforeItsLastIteration()
        {
            // The sample plan has resources, and each iteration schedules them again: a million of them take far longer than
            // the second the job has.
            await using RunningServer server = await RunningServer.StartAsync(
                m_Engine.JobRunner,
                new ServeSettings { Limits = new ServeLimits { MaxSimulationIterations = 1_000_000, JobTimeoutSeconds = 1 } });
            byte[] sample;
            await using (Stream stream = WarmUpService.OpenSample())
            using (var memory = new MemoryStream())
            {
                await stream.CopyToAsync(memory);
                sample = memory.ToArray();
            }

            using var content = RunningServer.CompileContent(sample, @"warm-up.zpp");
            content.Add(
                new StringContent(@"{""simulation"":{""iterations"":1000000}}", System.Text.Encoding.UTF8, @"application/json"),
                @"options");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            using HttpResponseMessage response = await server.Client.PostAsync(@"/v1/projects/compile", content);

            stopwatch.Stop();
            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            ProblemResponse problem = await RunningServer.ReadAsync<ProblemResponse>(response);
            problem.Type.ShouldBe(ProblemHelper.GetType(ProblemKind.JobTimeout));
            stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(30));
        }
    }
}
