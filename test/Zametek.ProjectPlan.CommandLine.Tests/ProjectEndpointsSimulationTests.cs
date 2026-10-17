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
    /// Tests for what zpp serve answers about the simulation of the schedule: its statistics in the answer, the chart and the CSV
    /// files among the outputs, a result that the project holds, the limit on the iterations of a project that the server runs,
    /// and that the time of a job stops it. The settings of a simulation are the project's, and so each test that needs some
    /// makes a project that holds them, as zpp saves one with -o.
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

        // The estimated plan, saved by a job that has these settings, and has run the simulation if a result is asked for.
        private async Task<byte[]> SimulatedAsync(
            SimulationSettingsModel settings,
            bool withResult = false,
            byte[]? plan = null)
        {
            using var input = new MemoryStream(plan ?? Estimated());
            var sink = new MemoryJobSink(new BufferedConsole());

            await m_Engine.JobRunner.RunAsync(
                new JobRequest { Input = input, SaveProject = true, SimulationSettings = settings, RunSimulation = withResult },
                sink);

            return sink.Outputs.Single(x => x.Output == JobOutput.Project).Content;
        }

        private static async Task<HttpResponseMessage> PostAsync(
            RunningServer server,
            string optionsJson,
            byte[]? plan = null,
            string query = @"",
            string? accept = null)
        {
            using var content = RunningServer.CompileContent(plan ?? Estimated(), @"estimated.zpp");
            content.Add(new StringContent(optionsJson, System.Text.Encoding.UTF8, @"application/json"), @"options");
            using var request = new HttpRequestMessage(HttpMethod.Post, $@"/v1/projects/compile{query}") { Content = content };

            if (accept is not null)
            {
                request.Headers.TryAddWithoutValidation(@"Accept", accept);
            }

            return await server.Client.SendAsync(request);
        }

        [Fact]
        public async Task Compile_Given_ARunOfTheSimulation_Then_AnswersWithItsStatisticsBetweenTheMetricsAndTheOutputs()
        {
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner);
            byte[] plan = await SimulatedAsync(new SimulationSettingsModel { Iterations = 1000, Seed = 77, TargetFinishTime = 30, UseRandomSeed = false });

            using HttpResponseMessage response = await PostAsync(server, @"{""runSimulation"":true}", plan);

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
        public async Task Compile_Given_NoRunAndNoResultInTheProject_Then_TheAnswerHasNoSimulation()
        {
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner);

            using HttpResponseMessage response = await PostAsync(server, @"{}");

            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            document.RootElement.EnumerateObject().Select(x => x.Name).ShouldBe([@"metrics", @"outputs"]);
        }

        [Fact]
        public async Task Compile_Given_NoRunAndAProjectThatHoldsAResult_Then_TheAnswerHasTheStatisticsOfTheResult()
        {
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner);
            byte[] plan = await SimulatedAsync(new SimulationSettingsModel { Iterations = 700, Seed = 4, TargetFinishTime = 30, UseRandomSeed = false }, withResult: true);

            using HttpResponseMessage response = await PostAsync(server, @"{}", plan);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            CompileResponse compiled = await RunningServer.ReadAsync<CompileResponse>(response);
            SimulationResponse simulation = compiled.Simulation.ShouldNotBeNull();
            simulation.Iterations.ShouldBe(700);
            simulation.Seed.ShouldBe(4UL);
            simulation.TargetFinishDays.ShouldBe(30);
            compiled.Outputs.ShouldBeEmpty();
        }

        [Fact]
        public async Task Compile_Given_TheOutputOfASimulationAndARun_Then_AnswersWithEachFileNamedAsZppNamesIt()
        {
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner);
            byte[] plan = await SimulatedAsync(new SimulationSettingsModel { Iterations = 300 });

            using HttpResponseMessage response = await PostAsync(
                server,
                """
                {
                    "runSimulation": true,
                    "outputs": {
                        "simulation": { "format": "png", "width": 400, "height": 300 }
                    }
                }
                """,
                plan);

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
        public async Task Compile_Given_TheOutputOfASimulationAndAResultThatTheProjectHolds_Then_AnswersWithEachFileButTheIterations()
        {
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner);
            byte[] plan = await SimulatedAsync(new SimulationSettingsModel { Iterations = 300, Seed = 8, UseRandomSeed = false }, withResult: true);

            using HttpResponseMessage response = await PostAsync(
                server,
                @"{""outputs"":{""simulation"":{""format"":""svg"",""width"":400,""height"":300}}}",
                plan);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            CompileResponse compiled = await RunningServer.ReadAsync<CompileResponse>(response);
            compiled.Simulation.ShouldNotBeNull().Iterations.ShouldBe(300);

            // The iterations of a run are not in a project, and so there is no file of them.
            compiled.Outputs.Select(x => x.Kind).ShouldBe(
            [
                JobOutput.SimulationChart, JobOutput.SimulationStatistics, JobOutput.SimulationHistogram,
                JobOutput.SimulationPaths, JobOutput.SimulationActivities,
            ]);
        }

        [Fact]
        public async Task Compile_Given_TheSameRunTwice_Then_TheSameAnswerByteForByte()
        {
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner);

            // The project saves "New seed each run", and the run uses the seed that it saved.
            byte[] plan = await SimulatedAsync(new SimulationSettingsModel { Iterations = 2000, Seed = 5, Distribution = DurationDistribution.Triangular, TargetFinishTime = 28, UseRandomSeed = true });
            const string options = """{"runSimulation":true,"outputs":{"simulation":{"format":"svg","width":300,"height":200}}}""";

            using HttpResponseMessage first = await PostAsync(server, options, plan);
            using HttpResponseMessage second = await PostAsync(server, options, plan);

            (await second.Content.ReadAsByteArrayAsync()).ShouldBe(await first.Content.ReadAsByteArrayAsync());
        }

        [Fact]
        public async Task Compile_Given_AnotherSeedInTheProject_Then_AnotherSample()
        {
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner);
            byte[] one = await SimulatedAsync(new SimulationSettingsModel { Iterations = 2000, Seed = 1 });
            byte[] two = await SimulatedAsync(new SimulationSettingsModel { Iterations = 2000, Seed = 2 });

            using HttpResponseMessage first = await PostAsync(server, @"{""runSimulation"":true}", one);
            using HttpResponseMessage second = await PostAsync(server, @"{""runSimulation"":true}", two);

            (await RunningServer.ReadAsync<CompileResponse>(second)).Simulation.ShouldNotBe((await RunningServer.ReadAsync<CompileResponse>(first)).Simulation);
        }

        [Fact]
        public async Task Compile_Given_AZip_Then_HasTheFilesOfTheSimulationAndItsStatisticsInResultJson()
        {
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner);
            byte[] plan = await SimulatedAsync(new SimulationSettingsModel { Iterations = 200 });

            using HttpResponseMessage response = await PostAsync(
                server,
                @"{""runSimulation"":true,""outputs"":{""simulation"":{""format"":""png"",""width"":200,""height"":100}}}",
                plan,
                accept: @"application/zip");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            using var archive = new ZipArchive(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
            archive.Entries.Select(x => x.FullName).ShouldBe(
            [
                @"estimated-simulation.png", @"estimated-simulation-statistics.csv", @"estimated-simulation-histogram.csv",
                @"estimated-simulation-paths.csv", @"estimated-simulation-activities.csv", @"estimated-simulation-iterations.csv",
                @"result.json",
            ]);

            using Stream result = archive.GetEntry(@"result.json")!.Open();
            using JsonDocument document = JsonDocument.Parse(result);
            document.RootElement.GetProperty(@"simulation").GetProperty(@"validIterations").GetInt32().ShouldBe(200);
        }

        [Fact]
        public async Task Compile_Given_AProjectWithMoreIterationsThanTheLimitAndARun_Then_422WithTheLimitNamed()
        {
            await using RunningServer server = await RunningServer.StartAsync(
                m_Engine.JobRunner,
                new ServeSettings { Limits = new ServeLimits { MaxSimulationIterations = 500 } });
            byte[] within = await SimulatedAsync(new SimulationSettingsModel { Iterations = 500 });
            byte[] over = await SimulatedAsync(new SimulationSettingsModel { Iterations = 501 });

            using HttpResponseMessage allowed = await PostAsync(server, @"{""runSimulation"":true}", within);
            using HttpResponseMessage refused = await PostAsync(server, @"{""runSimulation"":true}", over);

            allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
            refused.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            ProblemResponse problem = await RunningServer.ReadAsync<ProblemResponse>(refused);
            problem.Type.ShouldBe(ProblemHelper.GetType(ProblemKind.ValidationFailed));
            ProblemError error = problem.Errors.ShouldNotBeNull().ShouldHaveSingleItem();
            error.Pointer.ShouldBe(@"#/options/runSimulation");
            error.Code.ShouldBe(@"outOfRange");
            error.Detail.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_SimulationIterationsAboveLimit, 501, 500));
        }

        [Fact]
        public async Task Compile_Given_AProjectWithMoreIterationsThanTheLimitAndNoRun_Then_TheAnswerIsNotLimited()
        {
            // The limit is on what a run costs. The statistics of a result that the project already holds cost nothing.
            await using RunningServer server = await RunningServer.StartAsync(
                m_Engine.JobRunner,
                new ServeSettings { Limits = new ServeLimits { MaxSimulationIterations = 100 } });
            byte[] plan = await SimulatedAsync(new SimulationSettingsModel { Iterations = 300 }, withResult: true);

            using HttpResponseMessage response = await PostAsync(server, @"{}", plan);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await RunningServer.ReadAsync<CompileResponse>(response)).Simulation.ShouldNotBeNull().Iterations.ShouldBe(300);
        }

        [Fact]
        public async Task Compile_Given_AProjectThatHasNoSettingsAndALimitBelowTheDefault_Then_422BecauseTheDefaultIsTheSettings()
        {
            await using RunningServer server = await RunningServer.StartAsync(
                m_Engine.JobRunner,
                new ServeSettings { Limits = new ServeLimits { MaxSimulationIterations = 700 } });

            using HttpResponseMessage response = await PostAsync(server, @"{""runSimulation"":true}");

            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            (await RunningServer.ReadAsync<ProblemResponse>(response)).Errors.ShouldNotBeNull().ShouldHaveSingleItem().Detail
                .ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_SimulationIterationsAboveLimit, new SimulationSettingsModel().Iterations, 700));
        }

        [Fact]
        public async Task Compile_Given_TheOutputOfASimulationAndNeitherARunNorAResult_Then_422AtTheOutput()
        {
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner);

            using HttpResponseMessage response = await PostAsync(server, @"{""outputs"":{""simulation"":{""width"":300,""height"":200}}}");

            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            ProblemResponse problem = await RunningServer.ReadAsync<ProblemResponse>(response);
            problem.Type.ShouldBe(ProblemHelper.GetType(ProblemKind.SimulationNotPossible));
            ProblemError error = problem.Errors.ShouldNotBeNull().ShouldHaveSingleItem();
            error.Pointer.ShouldBe(@"#/options/outputs/simulation");
            error.Code.ShouldBe(@"noResult");
            error.Detail.ShouldBe(Resource.ProjectPlan.Messages.Message_SimulationOutputsNeedAResult);
        }

        [Fact]
        public async Task Compile_Given_ASettingThatARunCannotUse_Then_422AtTheRun()
        {
            // A project file that somebody edited by hand: the shape value of the modified beta-PERT distribution is not above 0.
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner);
            byte[] plan = await SimulatedAsync(new SimulationSettingsModel { Distribution = DurationDistribution.ModifiedBetaPert, ModifiedBetaShape = -1.0 });

            using HttpResponseMessage response = await PostAsync(server, @"{""runSimulation"":true}", plan);

            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            ProblemResponse problem = await RunningServer.ReadAsync<ProblemResponse>(response);
            problem.Type.ShouldBe(ProblemHelper.GetType(ProblemKind.SimulationNotPossible));
            ProblemError error = problem.Errors.ShouldNotBeNull().ShouldHaveSingleItem();
            error.Pointer.ShouldBe(@"#/options/runSimulation");
            error.Code.ShouldBe(@"outOfRange");
        }

        [Fact]
        public async Task Compile_Given_ASimulationThatTheTimeOfTheJobStops_Then_503JobTimeoutAndNotTheAnswerOfARunToItsEnd()
        {
            // The sample plan has resources, and each iteration schedules them again: a million of them take minutes, and
            // the job has a second. An answer that is the problem job-timeout, and not a 200, is a job that was stopped. How
            // long the stop takes depends on how busy the machine is (the test projects of a build run side by side, and
            // every test of a project runs side by side too, on a thread pool that the iterations of a simulation share), and
            // so the test says nothing of how soon, and gives the server's answer more time than the client of the other
            // tests does. How soon a simulation stops once it is told to is a test of the engine, which does not depend on a
            // clock.
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

            // The sample, saved with a million iterations as its settings.
            byte[] plan = await SimulatedAsync(new SimulationSettingsModel { Iterations = 1_000_000 }, plan: sample);

            using var patient = new HttpClient { BaseAddress = server.Client.BaseAddress, Timeout = TimeSpan.FromMinutes(5) };
            using var content = RunningServer.CompileContent(plan, @"warm-up.zpp");
            content.Add(new StringContent(@"{""runSimulation"":true}", System.Text.Encoding.UTF8, @"application/json"), @"options");

            using HttpResponseMessage response = await patient.PostAsync(@"/v1/projects/compile", content);

            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            ProblemResponse problem = await RunningServer.ReadAsync<ProblemResponse>(response);
            problem.Type.ShouldBe(ProblemHelper.GetType(ProblemKind.JobTimeout));
        }
    }
}
