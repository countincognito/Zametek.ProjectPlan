using Newtonsoft.Json.Linq;
using Shouldly;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;
using Zametek.Engine.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for zpp serve's API when a request cannot be answered: each kind of problem it answers with, with the status
    /// that says why and the type that tells it from the others, what each lists and what none says - nothing of the
    /// server - and what every response, a problem or not, carries: its request's id.
    /// </summary>
    [Collection(ProgramExitCodeTests.CollectionName)]
    public class ProjectProblemsTests
        : IClassFixture<EngineFixture>, IAsyncLifetime
    {
        private const string c_ApiKey = @"s3cr3t-k3y";
        private const string c_StatusTypeBase = @"https://tools.ietf.org/html/rfc9110#section-";

        private static readonly Regex s_RequestId = new(@"^[0-9a-f]{32}$");

        private static readonly ServeLimits s_Limits = new();

        private readonly EngineFixture m_Engine;
        private RunningServer? m_Server;

        public ProjectProblemsTests(EngineFixture engine)
        {
            m_Engine = engine;
        }

        private RunningServer Server => m_Server ?? throw new InvalidOperationException();

        public async Task InitializeAsync()
        {
            m_Server = await RunningServer.StartAsync(m_Engine.JobRunner, new ServeSettings { Limits = s_Limits });
        }

        public async Task DisposeAsync()
        {
            if (m_Server is not null)
            {
                await m_Server.DisposeAsync();
            }
        }

        private static byte[] Asset(string filename)
        {
            return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, @"Assets", filename));
        }

        private static byte[] TwoScenarios()
        {
            return Asset(@"two-scenarios.zpp");
        }

        // The two scenarios' project, changed as the test needs it.
        private static byte[] TwoScenarios(Action<JObject> change)
        {
            JObject plan = JObject.Parse(Encoding.UTF8.GetString(TwoScenarios()));
            change(plan);
            return Encoding.UTF8.GetBytes(plan.ToString());
        }

        // The project that the first plan of the two scenarios' - an activity that depends on one that is not there - has
        // two kinds of error: and a cycle among two more.
        private static byte[] PlanWithTwoKindsOfError()
        {
            JObject plan = JObject.Parse(Encoding.UTF8.GetString(Asset(@"broken-dependency.zpp")));
            var activities = (JArray)plan[@"Files"]![0]![@"Scenario"]![@"DependentActivities"]!;

            foreach ((int id, int dependency) in new[] { (2, 3), (3, 2) })
            {
                JToken activity = activities[0].DeepClone();
                activity[@"Activity"]![@"Id"] = id;
                activity[@"Activity"]![@"Name"] = $@"Task {id}";
                activity[@"Dependencies"] = new JArray(dependency);
                activities.Add(activity);
            }

            return Encoding.UTF8.GetBytes(plan.ToString());
        }

        private async Task<(HttpResponseMessage Response, ProblemResponse Problem)> CompileAsync(
            byte[] plan,
            string filename = @"plan.zpp",
            CompileOptions? options = null,
            string part = @"project",
            string query = @"",
            RunningServer? server = null)
        {
            using var content = RunningServer.CompileContent(plan, filename, options, part);
            HttpResponseMessage response = await (server ?? Server).Client.PostAsync($@"/v1/projects/compile{query}", content);
            return (response, await RunningServer.ReadProblemAsync(response));
        }

        private async Task<(HttpResponseMessage Response, ProblemResponse Problem)> CompileJsonAsync(
            string optionsJson,
            string query = @"")
        {
            using var content = RunningServer.CompileContent(TwoScenarios(), @"two-scenarios.zpp");
            content.Add(new StringContent(optionsJson, Encoding.UTF8, @"application/json"), @"options");
            HttpResponseMessage response = await Server.Client.PostAsync($@"/v1/projects/compile{query}", content);
            return (response, await RunningServer.ReadProblemAsync(response));
        }

        private static string[] Where(ProblemResponse problem)
        {
            return [.. problem.Errors.ShouldNotBeNull().Select(x => $@"{x.Pointer ?? x.Parameter} {x.Code}")];
        }

        [Fact]
        public async Task Compile_Given_APlanThatDoesNotCompile_Then_UnprocessableListingTheCompilersErrors()
        {
            (HttpResponseMessage response, ProblemResponse problem) = await CompileAsync(Asset(@"broken-dependency.zpp"), @"broken-dependency.zpp");
            using HttpResponseMessage _ = response;

            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            problem.Type.ShouldBe(ProblemHelper.GetType(ProblemKind.CompilationFailed));
            problem.Title.ShouldBe(Resource.ProjectPlan.Messages.Message_ServeTitleCompilationFailed);
            problem.Detail.ShouldBe(Resource.ProjectPlan.Messages.Message_ServeProjectHasACompilationError);
            ProblemError error = problem.Errors.ShouldNotBeNull().ShouldHaveSingleItem();
            error.Pointer.ShouldBe(@"#/project");
            error.Code.ShouldBe(@"P0010");
            error.Detail.ShouldContain(@"999 is invalid but referenced by: 1");
            error.Detail.ShouldBe(error.Detail.TrimEnd());
            // Its lines end as everything the server says does, whatever system the compiler wrote them on.
            error.Detail.ShouldContain("\n");
            error.Detail.ShouldNotContain("\r");
            problem.Console.ShouldBeNull();
            problem.Metrics.ShouldBeNull();
            problem.Outputs.ShouldBeNull();
        }

        [Fact]
        public async Task Compile_Given_APlanWithTwoKindsOfError_Then_ListsBothWithTheirCodes()
        {
            (HttpResponseMessage response, ProblemResponse problem) = await CompileAsync(PlanWithTwoKindsOfError());
            using HttpResponseMessage _ = response;

            problem.Detail.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeProjectHasCompilationErrors, 2));
            Where(problem).ShouldBe([@"#/project P0010", @"#/project P0020"]);
        }

        [Fact]
        public async Task Compile_Given_APlanThatDoesNotCompileAndTheConsole_Then_TheConsoleHasTheCompilersReportAndExitCode3()
        {
            (HttpResponseMessage response, ProblemResponse problem) = await CompileAsync(Asset(@"broken-dependency.zpp"), @"broken-dependency.zpp", query: @"?include=console");
            using HttpResponseMessage _ = response;

            ConsoleResponse console = problem.Console.ShouldNotBeNull();
            console.ExitCode.ShouldBe((int)ExitCode.CompilationErrors);
            JobTranscriptEntry entry = console.Transcript.ShouldHaveSingleItem();
            entry.Kind.ShouldBe(JobTranscriptKind.Display);
            entry.HasErrors.ShouldBeTrue();
            entry.Text.ShouldBe(console.StandardOutput[NewLineHelper.NewLine.Length..^NewLineHelper.NewLine.Length]);
        }

        [Fact]
        public async Task Compile_Given_AFileThatIsNotAProject_Then_UnprocessableSayingSoWithoutTheReadersWords()
        {
            (HttpResponseMessage response, ProblemResponse problem) = await CompileAsync(Encoding.UTF8.GetBytes(@"This is not a plan."), @"plan.zpp", query: @"?include=console");
            using HttpResponseMessage _ = response;

            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            problem.Type.ShouldBe(ProblemHelper.GetType(ProblemKind.ProjectNotReadable));
            problem.Detail.ShouldBe(Resource.ProjectPlan.Messages.Message_ServeProjectNotReadable);
            ProblemError error = problem.Errors.ShouldNotBeNull().ShouldHaveSingleItem();
            error.Pointer.ShouldBe(@"#/project");
            error.Code.ShouldBe(@"unreadable");
            error.Detail.ShouldBe(Resource.ProjectPlan.Messages.Message_ServeProjectNotReadable);

            // What zpp prints is in the console, which the request asked for: the reader's words, as zpp has always said them.
            ConsoleResponse console = problem.Console.ShouldNotBeNull();
            console.ExitCode.ShouldBe((int)ExitCode.Failure);
            console.StandardError.ShouldNotBeNullOrWhiteSpace();
            console.Transcript.ShouldHaveSingleItem().Kind.ShouldBe(JobTranscriptKind.ErrorLine);
            problem.Detail.ShouldNotBeNull().ShouldNotContain(console.StandardError.Trim());
        }

        [Fact]
        public async Task Compile_Given_AFileThatIsNotAWorkbook_Then_UnprocessableNamingTheImport()
        {
            (HttpResponseMessage response, ProblemResponse problem) = await CompileAsync(Encoding.UTF8.GetBytes(@"This is not a workbook."), @"plan.xlsx", part: @"import");
            using HttpResponseMessage _ = response;

            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            problem.Type.ShouldBe(ProblemHelper.GetType(ProblemKind.ProjectNotReadable));
            problem.Detail.ShouldBe(Resource.ProjectPlan.Messages.Message_ServeWorkbookNotReadable);
            Where(problem).ShouldBe([@"#/import unreadable"]);
        }

        [Fact]
        public async Task ListScenarios_Given_AFileThatIsNotAProject_Then_Unprocessable()
        {
            using var content = RunningServer.CompileContent(Encoding.UTF8.GetBytes(@"This is not a plan."), @"plan.zpp");
            using HttpResponseMessage response = await Server.Client.PostAsync(@"/v1/projects/scenarios", content);
            ProblemResponse problem = await RunningServer.ReadProblemAsync(response);

            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            problem.Type.ShouldBe(ProblemHelper.GetType(ProblemKind.ProjectNotReadable));
            Where(problem).ShouldBe([@"#/project unreadable"]);
        }

        [Theory]
        [InlineData(@"Gamma", @"notFound")]
        [InlineData(@"Alpha", @"ambiguous")]
        [InlineData(@"Beta", @"hasNoData")]
        public async Task Compile_Given_AScenarioThatCannotBeSelected_Then_UnprocessableSayingWhy(string selector, string code)
        {
            // Two scenarios named Alpha for the second, and the file of the second scenario gone for the third.
            byte[] plan = code switch
            {
                @"ambiguous" => TwoScenarios(x => x[@"Nodes"]![1]![@"Name"] = @"Alpha"),
                @"hasNoData" => TwoScenarios(x => ((JArray)x[@"Files"]!)[1].Remove()),
                _ => TwoScenarios(),
            };

            (HttpResponseMessage response, ProblemResponse problem) = await CompileAsync(
                plan,
                options: new CompileOptions { Scenario = selector },
                query: @"?include=console");
            using HttpResponseMessage _ = response;

            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            problem.Type.ShouldBe(ProblemHelper.GetType(ProblemKind.ScenarioNotSelectable));
            ProblemError error = problem.Errors.ShouldNotBeNull().ShouldHaveSingleItem();
            error.Pointer.ShouldBe(@"#/options/scenario");
            error.Code.ShouldBe(code);
            error.Detail.ShouldContain(selector);
            problem.Detail.ShouldBe(error.Detail);
            problem.Console.ShouldNotBeNull().ExitCode.ShouldBe((int)ExitCode.Failure);
        }

        [Fact]
        public async Task Compile_Given_ACompilationThatRunsOutOfTime_Then_UnprocessableWithExitCode4()
        {
            await using FailingEngine engine = FailingEngine.WithACompilationThatRunsOutOfTime();
            await using RunningServer server = await RunningServer.StartAsync(engine.JobRunner);

            (HttpResponseMessage response, ProblemResponse problem) = await CompileAsync(TwoScenarios(), query: @"?include=console", server: server);
            using HttpResponseMessage _ = response;

            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            problem.Type.ShouldBe(ProblemHelper.GetType(ProblemKind.CompilationTimedOut));
            problem.Title.ShouldBe(Resource.ProjectPlan.Messages.Message_ServeTitleCompilationTimedOut);
            problem.Detail.ShouldBe(Resource.ProjectPlan.Messages.Message_ServeCompilationTimedOut);
            problem.Errors.ShouldBeNull();

            ConsoleResponse console = problem.Console.ShouldNotBeNull();
            console.ExitCode.ShouldBe((int)ExitCode.CompilationTimeout);
            console.StandardError.ShouldNotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task Compile_Given_AnOutputThatCannotBeProduced_Then_ServerErrorWithTheOthersAndTheMetrics()
        {
            await using FailingEngine engine = FailingEngine.WithAGanttChartThatCannotBeDrawn();
            await using RunningServer server = await RunningServer.StartAsync(engine.JobRunner);

            (HttpResponseMessage response, ProblemResponse problem) = await CompileAsync(
                TwoScenarios(),
                options: new CompileOptions
                {
                    Outputs = new OutputsOptions
                    {
                        Project = new ProjectOptions(),
                        GanttChart = new ChartOptions { Format = PlotExport.Png, Width = 100, Height = 100 },
                        ArrowGraph = new GraphOptions { Format = GraphExport.Svg },
                    },
                },
                server: server);
            using HttpResponseMessage _ = response;

            response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
            problem.Type.ShouldBe(ProblemHelper.GetType(ProblemKind.OutputFailed));
            problem.Detail.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeOutputFailed, @"gantt chart"));
            ProblemError error = problem.Errors.ShouldNotBeNull().ShouldHaveSingleItem();
            error.Pointer.ShouldBe(@"#/options/outputs/ganttChart");
            error.Code.ShouldBe(@"failed");

            // What was produced, and the metrics, are in it as extension members.
            problem.Metrics.ShouldNotBeNull().NetworkDuration.ShouldBe(5);
            problem.Outputs.ShouldNotBeNull().Select(x => x.Kind).ShouldBe([JobOutput.Project, JobOutput.ArrowGraph]);
            problem.Outputs.ShouldAllBe(x => x.Content != null && x.Content.Length > 0);
            problem.Console.ShouldBeNull();
        }

        [Fact]
        public async Task Compile_Given_AnOutputThatCannotBeProducedAndTheConsole_Then_TheConsoleSaysSoWithExitCode1()
        {
            await using FailingEngine engine = FailingEngine.WithAGanttChartThatCannotBeDrawn();
            await using RunningServer server = await RunningServer.StartAsync(engine.JobRunner);

            (HttpResponseMessage response, ProblemResponse problem) = await CompileAsync(
                TwoScenarios(),
                options: new CompileOptions { Outputs = new OutputsOptions { GanttChart = new ChartOptions { Format = PlotExport.Png, Width = 100, Height = 100 } } },
                query: @"?include=console",
                server: server);
            using HttpResponseMessage _ = response;

            ConsoleResponse console = problem.Console.ShouldNotBeNull();
            console.ExitCode.ShouldBe((int)ExitCode.Failure);
            console.StandardError.ShouldNotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task Compile_Given_AFailureNobodyExpected_Then_ServerErrorThatSaysNothingOfIt()
        {
            await using FailingEngine engine = FailingEngine.ThatFailsUnexpectedly();
            await using RunningServer server = await RunningServer.StartAsync(engine.JobRunner);

            using var content = RunningServer.CompileContent(TwoScenarios(), @"two-scenarios.zpp");
            using HttpResponseMessage response = await server.Client.PostAsync(@"/v1/projects/compile?include=console", content);
            string json = await response.Content.ReadAsStringAsync();
            ProblemResponse problem = JsonSerializer.Deserialize<ProblemResponse>(json, ProjectEndpoints.JsonOptions).ShouldNotBeNull();

            response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
            problem.Type.ShouldBe(ProblemHelper.GetType(ProblemKind.UnexpectedError));
            problem.Detail.ShouldBe(Resource.ProjectPlan.Messages.Message_ServeUnexpectedError);
            problem.Errors.ShouldBeNull();

            // Not what the exception says, nor its type, nor the path it names - not even in the console, which says only that
            // the server failed, and which request it was.
            json.ShouldNotContain(FailingEngine.Secret);
            json.ShouldNotContain(@"InvalidOperationException");
            ConsoleResponse console = problem.Console.ShouldNotBeNull();
            console.ExitCode.ShouldBe((int)ExitCode.Failure);
            console.StandardError.ShouldContain(problem.TraceId);
        }

        [Fact]
        public async Task Compile_Given_AFailureNobodyExpectedOnAListing_Then_ServerError()
        {
            await using FailingEngine engine = FailingEngine.ThatFailsUnexpectedly();
            await using RunningServer server = await RunningServer.StartAsync(engine.JobRunner);

            using var content = RunningServer.CompileContent(TwoScenarios(), @"two-scenarios.zpp");
            using HttpResponseMessage response = await server.Client.PostAsync(@"/v1/projects/scenarios", content);
            ProblemResponse problem = await RunningServer.ReadProblemAsync(response);

            response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
            problem.Type.ShouldBe(ProblemHelper.GetType(ProblemKind.UnexpectedError));
        }

        [Fact]
        public async Task Compile_Given_AJobThatRunsOutOfTime_Then_ServiceUnavailableAndToldToWait()
        {
            // A limit of no time at all is one no job can keep to.
            await using RunningServer server = await RunningServer.StartAsync(
                m_Engine.JobRunner,
                new ServeSettings { Limits = s_Limits with { JobTimeoutSeconds = 0 } });

            (HttpResponseMessage compile, ProblemResponse compileProblem) = await CompileAsync(TwoScenarios(), server: server);
            using var content = RunningServer.CompileContent(TwoScenarios(), @"two-scenarios.zpp");
            using HttpResponseMessage scenarios = await server.Client.PostAsync(@"/v1/projects/scenarios", content);
            ProblemResponse scenariosProblem = await RunningServer.ReadProblemAsync(scenarios);
            using HttpResponseMessage _ = compile;

            foreach ((HttpResponseMessage response, ProblemResponse problem) in new[] { (compile, compileProblem), (scenarios, scenariosProblem) })
            {
                response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
                response.Headers.RetryAfter.ShouldNotBeNull().Delta.ShouldBe(TimeSpan.FromSeconds(JobServer.RetryAfterSeconds));
                problem.Type.ShouldBe(ProblemHelper.GetType(ProblemKind.JobTimeout));
                problem.Detail.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeJobTimedOut, 0));
            }
        }

        [Fact]
        public async Task Compile_Given_ABodyThatIsNotMultipart_Then_UnsupportedMediaType()
        {
            using var content = new StringContent(@"{}", Encoding.UTF8, @"application/json");
            using HttpResponseMessage response = await Server.Client.PostAsync(@"/v1/projects/compile", content);
            ProblemResponse problem = await RunningServer.ReadProblemAsync(response);

            response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
            problem.Type.ShouldBe($@"{c_StatusTypeBase}15.5.16");
            problem.Title.ShouldBe(@"Unsupported Media Type");
            problem.Detail.ShouldBe(Resource.ProjectPlan.Messages.Message_ServeRequestNotMultipartCompile);
        }

        [Fact]
        public async Task ListScenarios_Given_ABodyThatIsNotMultipart_Then_UnsupportedMediaType()
        {
            using var content = new StringContent(@"{}", Encoding.UTF8, @"application/json");
            using HttpResponseMessage response = await Server.Client.PostAsync(@"/v1/projects/scenarios", content);
            ProblemResponse problem = await RunningServer.ReadProblemAsync(response);

            response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
            problem.Detail.ShouldBe(Resource.ProjectPlan.Messages.Message_ServeRequestNotMultipartScenarios);
        }

        [Theory]
        [InlineData(@"/v1/projects/compile")]
        [InlineData(@"/v1/projects/scenarios")]
        public async Task Post_Given_AnAcceptThatNothingOfferedMeets_Then_NotAcceptable(string path)
        {
            using var content = RunningServer.CompileContent(TwoScenarios(), @"two-scenarios.zpp");
            using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = content };
            request.Headers.Accept.ParseAdd(@"text/html");
            using HttpResponseMessage response = await Server.Client.SendAsync(request);
            ProblemResponse problem = await RunningServer.ReadProblemAsync(response);

            response.StatusCode.ShouldBe(HttpStatusCode.NotAcceptable);
            problem.Type.ShouldBe($@"{c_StatusTypeBase}15.5.7");
            problem.Detail.ShouldStartWith(@"The server can answer as application/json");
        }

        [Fact]
        public async Task ListScenarios_Given_ThatAZipIsAskedFor_Then_NotAcceptable()
        {
            // A listing is JSON only.
            using var content = RunningServer.CompileContent(TwoScenarios(), @"two-scenarios.zpp");
            using var request = new HttpRequestMessage(HttpMethod.Post, @"/v1/projects/scenarios") { Content = content };
            request.Headers.Accept.ParseAdd(@"application/zip");
            using HttpResponseMessage response = await Server.Client.SendAsync(request);

            response.StatusCode.ShouldBe(HttpStatusCode.NotAcceptable);
        }

        [Fact]
        public async Task Compile_Given_ARequestLargerThanTheLimit_Then_PayloadTooLarge()
        {
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner, new ServeSettings { Limits = s_Limits with { MaxUploadMegabytes = 1 } });

            // Asking to continue first, so that the server answers before the request is sent in full.
            using var request = new HttpRequestMessage(HttpMethod.Post, @"/v1/projects/compile")
            {
                Content = RunningServer.CompileContent(new byte[2 * 1024 * 1024], @"large.zpp"),
            };
            request.Headers.ExpectContinue = true;
            using HttpResponseMessage response = await server.Client.SendAsync(request);
            ProblemResponse problem = await RunningServer.ReadProblemAsync(response);

            response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
            problem.Type.ShouldBe($@"{c_StatusTypeBase}15.5.14");
            problem.Detail.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeRequestTooLarge, 1));
        }

        [Fact]
        public async Task Compile_Given_OptionsLargerThanOptionsAre_Then_PayloadTooLarge()
        {
            string options = $@"{{""scenario"":""{new string('x', CompileOptionsHelper.MaxOptionsBytes)}""}}";

            (HttpResponseMessage response, ProblemResponse problem) = await CompileJsonAsync(options);
            using HttpResponseMessage _ = response;

            response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
            Where(problem).ShouldBe([@"#/options tooLarge"]);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Compile_Given_OptionsOfExactlyTheLargestSize_Then_ReadThemAndOneByteMoreIsTooLarge(bool asFile)
        {
            // {"scenario":""} is 15 bytes before the name that fills it to the size: a scenario that is not there, which is
            // a problem for the job to find, and not for the request to be refused for.
            string Options(int bytes) => $@"{{""scenario"":""{new string('x', bytes - 15)}""}}";

            async Task<HttpResponseMessage> PostAsync(string options)
            {
                using var content = RunningServer.CompileContent(TwoScenarios(), @"two-scenarios.zpp");

                if (asFile)
                {
                    content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(options)), @"options", @"options.json");
                }
                else
                {
                    content.Add(new StringContent(options, Encoding.UTF8, @"application/json"), @"options");
                }

                return await Server.Client.PostAsync(@"/v1/projects/compile", content);
            }

            using HttpResponseMessage atTheLimit = await PostAsync(Options(CompileOptionsHelper.MaxOptionsBytes));
            using HttpResponseMessage overTheLimit = await PostAsync(Options(CompileOptionsHelper.MaxOptionsBytes + 1));

            atTheLimit.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            (await RunningServer.ReadProblemAsync(atTheLimit)).Type.ShouldBe(ProblemHelper.GetType(ProblemKind.ScenarioNotSelectable));
            overTheLimit.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
            Where(await RunningServer.ReadProblemAsync(overTheLimit)).ShouldBe([@"#/options tooLarge"]);
        }

        [Fact]
        public async Task Compile_Given_NoProject_Then_UnprocessableNamingThePartThatIsRequired()
        {
            using var content = new MultipartFormDataContent { { new StringContent(@"{}", Encoding.UTF8, @"application/json"), @"options" } };
            using HttpResponseMessage response = await Server.Client.PostAsync(@"/v1/projects/compile", content);
            ProblemResponse problem = await RunningServer.ReadProblemAsync(response);

            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            problem.Type.ShouldBe(ProblemHelper.GetType(ProblemKind.ValidationFailed));
            problem.Title.ShouldBe(Resource.ProjectPlan.Messages.Message_ServeTitleValidationFailed);
            problem.Detail.ShouldBe(Resource.ProjectPlan.Messages.Message_ServeRequestHasAProblem);
            ProblemError error = problem.Errors.ShouldNotBeNull().ShouldHaveSingleItem();
            error.Pointer.ShouldBe(@"#/project");
            error.Code.ShouldBe(@"required");
            error.Detail.ShouldBe(Resource.ProjectPlan.Messages.Message_ServeErrorProjectRequired);
        }

        [Fact]
        public async Task Compile_Given_BothAProjectAndAnImport_Then_Unprocessable()
        {
            using var content = RunningServer.CompileContent(TwoScenarios(), @"two-scenarios.zpp");
            content.Add(new ByteArrayContent(TwoScenarios()), @"import", @"plan.xlsx");
            using HttpResponseMessage response = await Server.Client.PostAsync(@"/v1/projects/compile", content);
            ProblemResponse problem = await RunningServer.ReadProblemAsync(response);

            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            Where(problem).ShouldBe([@"#/import notAllowed"]);
        }

        [Fact]
        public async Task Compile_Given_AProjectThatIsAFieldAndNotAFile_Then_Unprocessable()
        {
            using var content = new MultipartFormDataContent { { new StringContent(@"{}"), @"project" } };
            using HttpResponseMessage response = await Server.Client.PostAsync(@"/v1/projects/compile", content);
            ProblemResponse problem = await RunningServer.ReadProblemAsync(response);

            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            Where(problem).ShouldBe([@"#/project wrongType"]);
        }

        [Fact]
        public async Task Compile_Given_APartTheEndpointDoesNotTake_Then_Unprocessable()
        {
            using var content = RunningServer.CompileContent(TwoScenarios(), @"two-scenarios.zpp");
            content.Add(new StringContent(@"red"), @"colour");
            using HttpResponseMessage response = await Server.Client.PostAsync(@"/v1/projects/compile", content);
            ProblemResponse problem = await RunningServer.ReadProblemAsync(response);

            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            Where(problem).ShouldBe([@"#/colour unknownProperty"]);
            problem.Errors.ShouldNotBeNull().ShouldHaveSingleItem().Detail.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeErrorPartNotKnown, @"colour"));
        }

        [Fact]
        public async Task ListScenarios_Given_PartsOtherThanTheProject_Then_UnprocessableNamingEach()
        {
            using var content = RunningServer.CompileContent(TwoScenarios(), @"two-scenarios.zpp", new CompileOptions { Scenario = @"Beta" });
            content.Add(new ByteArrayContent(TwoScenarios()), @"import", @"plan.xlsx");
            using HttpResponseMessage response = await Server.Client.PostAsync(@"/v1/projects/scenarios", content);
            ProblemResponse problem = await RunningServer.ReadProblemAsync(response);

            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            Where(problem).ShouldBe([@"#/options unknownProperty", @"#/import unknownProperty"], ignoreOrder: true);
        }

        [Fact]
        public async Task ListScenarios_Given_NoProject_Then_Unprocessable()
        {
            using var content = new MultipartFormDataContent { { new StringContent(@"x"), @"colour" } };
            using HttpResponseMessage response = await Server.Client.PostAsync(@"/v1/projects/scenarios", content);
            ProblemResponse problem = await RunningServer.ReadProblemAsync(response);

            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            Where(problem).ShouldBe([@"#/colour unknownProperty", @"#/project required"], ignoreOrder: true);
            problem.Detail.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeRequestHasProblems, 2));
        }

        [Fact]
        public async Task Compile_Given_APlanWithoutAName_Then_UnprocessableBecauseItsOutputsAreNamedAfterIt()
        {
            (HttpResponseMessage response, ProblemResponse problem) = await CompileAsync(TwoScenarios(), @".zpp");
            using HttpResponseMessage _ = response;

            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            Where(problem).ShouldBe([@"#/project required"]);
            problem.Errors.ShouldNotBeNull().ShouldHaveSingleItem().Detail.ShouldBe(Resource.ProjectPlan.Messages.Message_ServeErrorFileNeedsName);
        }

        [Theory]
        [InlineData(@"plan.mpp")]
        [InlineData(@"plan.xml")]
        [InlineData(@"plan.csv")]
        public async Task Compile_Given_AnImportThatIsNotAWorkbook_Then_UnsupportedMediaType(string filename)
        {
            (HttpResponseMessage response, ProblemResponse problem) = await CompileAsync(TwoScenarios(), filename, part: @"import");
            using HttpResponseMessage _ = response;

            response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
            problem.Type.ShouldBe($@"{c_StatusTypeBase}15.5.16");
            problem.Detail.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeCannotImport, filename));
            Where(problem).ShouldBe([@"#/import unsupportedFormat"]);
        }

        [Fact]
        public async Task Compile_Given_AnImportWithAPathThatIsNotAWorkbook_Then_TheDetailNamesTheFileAlone()
        {
            (HttpResponseMessage response, ProblemResponse problem) = await CompileAsync(TwoScenarios(), @"some/where/else/plan.mpp", part: @"import");
            using HttpResponseMessage _ = response;

            response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
            problem.Detail.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeCannotImport, @"plan.mpp"));
            problem.Detail.ShouldNotBeNull().ShouldNotContain(@"where");
        }

        [Fact]
        public async Task Compile_Given_ASeveralProblemsInTheOptions_Then_UnprocessableListingEveryOne()
        {
            (HttpResponseMessage response, ProblemResponse problem) = await CompileJsonAsync(
                @"{""compileTimeout"":""PT0S"",""outputs"":{""ganttChart"":{""format"":""png"",""width"":99999,""height"":600},""ev"":{}},""colour"":""red""}");
            using HttpResponseMessage _ = response;

            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            problem.Type.ShouldBe(ProblemHelper.GetType(ProblemKind.ValidationFailed));
            problem.Detail.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeRequestHasProblems, 4));
            Where(problem).ShouldBe(
            [
                @"#/options/compileTimeout outOfRange",
                @"#/options/outputs/ganttChart/width outOfRange",
                @"#/options/outputs/ev unknownProperty",
                @"#/options/colour unknownProperty",
            ]);

            // Not the parser's words, which name the server's types.
            string[] details = [.. problem.Errors.ShouldNotBeNull().Select(x => x.Detail)];
            details.ShouldAllBe(x => !x.Contains(@"Zametek") && !x.Contains(@".NET") && !x.Contains(@"System."));
            details[1].ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeErrorPixelsOutOfRange, s_Limits.MaxChartWidth));
        }

        [Theory]
        [InlineData(@"{")]
        [InlineData(@"")]
        [InlineData(@"not json")]
        public async Task Compile_Given_OptionsThatAreNotJson_Then_BadRequest(string options)
        {
            (HttpResponseMessage response, ProblemResponse problem) = await CompileJsonAsync(options);
            using HttpResponseMessage _ = response;

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            problem.Type.ShouldBe(ProblemHelper.GetType(ProblemKind.MalformedRequest));
            problem.Title.ShouldBe(Resource.ProjectPlan.Messages.Message_ServeTitleMalformedRequest);
            Where(problem).ShouldBe([@"#/options invalidFormat"]);
            problem.Errors.ShouldNotBeNull().ShouldHaveSingleItem().Detail.ShouldStartWith(@"must be valid JSON (line 1, position ");
        }

        [Fact]
        public async Task Compile_Given_AScenarioAndAnImport_Then_UnprocessableBecauseOnlyAProjectHasScenarios()
        {
            (HttpResponseMessage response, ProblemResponse problem) = await CompileAsync(
                TwoScenarios(),
                @"plan.xlsx",
                new CompileOptions { Scenario = @"Beta" },
                @"import");
            using HttpResponseMessage _ = response;

            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            Where(problem).ShouldBe([@"#/options/scenario notAllowedWithImport"]);
        }

        [Fact]
        public async Task Compile_Given_ThatAnIncludeIsNotOneTheAnswerHas_Then_BadRequestNamingTheParameter()
        {
            using var content = RunningServer.CompileContent(TwoScenarios(), @"two-scenarios.zpp");
            using HttpResponseMessage response = await Server.Client.PostAsync(@"/v1/projects/compile?include=console,metrics", content);
            ProblemResponse problem = await RunningServer.ReadProblemAsync(response);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            problem.Type.ShouldBe(ProblemHelper.GetType(ProblemKind.MalformedRequest));
            ProblemError error = problem.Errors.ShouldNotBeNull().ShouldHaveSingleItem();
            error.Parameter.ShouldBe(@"include");
            error.Pointer.ShouldBeNull();
            error.Code.ShouldBe(@"notAllowed");
            error.Detail.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeErrorIncludeNotKnown, @"metrics"));
        }

        [Theory]
        [InlineData(@"?include=Console", @"Console")]
        [InlineData(@"?include=CONSOLE", @"CONSOLE")]
        [InlineData(@"?include=console,Console", @"Console")]
        public async Task Compile_Given_AnIncludeInAnotherCaseThanTheAnswerHas_Then_BadRequestBecauseValuesAreMatchedAsWritten(string query, string value)
        {
            using var content = RunningServer.CompileContent(TwoScenarios(), @"two-scenarios.zpp");
            using HttpResponseMessage response = await Server.Client.PostAsync($@"/v1/projects/compile{query}", content);
            ProblemResponse problem = await RunningServer.ReadProblemAsync(response);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            ProblemError error = problem.Errors.ShouldNotBeNull().ShouldHaveSingleItem();
            error.Parameter.ShouldBe(@"include");
            error.Code.ShouldBe(@"notAllowed");
            error.Detail.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeErrorIncludeNotKnown, value));
        }

        [Theory]
        [InlineData(@"?limit=5", @"limit")]
        [InlineData(@"?Include=console", @"Include")]
        [InlineData(@"?fields=metrics", @"fields")]
        public async Task Compile_Given_AParameterTheEndpointDoesNotTake_Then_BadRequestSoThatAMisspeltOneIsNotIgnored(string query, string parameter)
        {
            using var content = RunningServer.CompileContent(TwoScenarios(), @"two-scenarios.zpp");
            using HttpResponseMessage response = await Server.Client.PostAsync($@"/v1/projects/compile{query}", content);
            ProblemResponse problem = await RunningServer.ReadProblemAsync(response);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            Where(problem).ShouldBe([$@"{parameter} unknownProperty"]);
        }

        [Fact]
        public async Task Compile_Given_ProblemsOfBothKinds_Then_BadRequestStillListingThemAll()
        {
            // A parameter that is wrong - which a client cannot mend by changing what it says - and options that are not
            // valid: the more general of the two statuses, and every problem.
            (HttpResponseMessage response, ProblemResponse problem) = await CompileJsonAsync(
                @"{""baseTheme"":""Sepia"",""now"":""yesterday""}",
                @"?include=everything");
            using HttpResponseMessage _ = response;

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            problem.Type.ShouldBe(ProblemHelper.GetType(ProblemKind.MalformedRequest));
            problem.Detail.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeRequestHasProblems, 3));
            Where(problem).ShouldBe([@"include notAllowed", @"#/options/baseTheme notAllowed", @"#/options/now invalidFormat"]);
        }

        [Fact]
        public async Task Compile_Given_OptionsInTheWrongCase_Then_UnprocessableBecauseNamesAndValuesAreMatchedAsWritten()
        {
            (HttpResponseMessage response, ProblemResponse problem) = await CompileJsonAsync(@"{""Scenario"":""Beta"",""baseTheme"":""Dark""}");
            using HttpResponseMessage _ = response;

            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            Where(problem).ShouldBe([@"#/options/Scenario unknownProperty", @"#/options/baseTheme notAllowed"]);
        }

        [Fact]
        public async Task Post_Given_NoKey_Then_UnauthorizedAsAProblem()
        {
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner, new ServeSettings { ApiKey = c_ApiKey });

            using var content = RunningServer.CompileContent(TwoScenarios(), @"two-scenarios.zpp");
            using HttpResponseMessage response = await server.Client.PostAsync(@"/v1/projects/compile", content);
            ProblemResponse problem = await RunningServer.ReadProblemAsync(response);

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            response.Headers.WwwAuthenticate.ShouldHaveSingleItem().Scheme.ShouldBe(@"Bearer");
            problem.Type.ShouldBe($@"{c_StatusTypeBase}15.5.2");
            problem.Title.ShouldBe(@"Unauthorized");
            problem.Detail.ShouldBe(Resource.ProjectPlan.Messages.Message_ServeApiKeyRequired);
        }

        [Fact]
        public async Task Get_Given_APathThatIsNotOne_Then_NotFoundAsAProblem()
        {
            using HttpResponseMessage response = await Server.Client.GetAsync(@"/v1/nothing");
            ProblemResponse problem = await RunningServer.ReadProblemAsync(response);

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            problem.Type.ShouldBe($@"{c_StatusTypeBase}15.5.5");
            problem.Title.ShouldBe(@"Not Found");
            problem.Detail.ShouldBeNull();
        }

        [Fact]
        public async Task Get_Given_AMethodThePathDoesNotTake_Then_MethodNotAllowedWithWhatItTakes()
        {
            using HttpResponseMessage response = await Server.Client.GetAsync(@"/v1/projects/compile");
            ProblemResponse problem = await RunningServer.ReadProblemAsync(response);

            response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
            response.Content.Headers.Allow.ShouldBe([@"POST", @"OPTIONS"], ignoreOrder: true);
            problem.Type.ShouldBe($@"{c_StatusTypeBase}15.5.6");
        }

        [Fact]
        public async Task Every_Given_AnyResponse_Then_CarriesItsRequestIdAndNosniff()
        {
            // An answer, a problem, an unknown path, a method that is not taken, the server's own account of itself, and a
            // probe of its health.
            using HttpResponseMessage answer = await PostCompileAsync();
            using HttpResponseMessage problem = await Server.Client.PostAsync(@"/v1/projects/compile", new MultipartFormDataContent());
            using HttpResponseMessage notFound = await Server.Client.GetAsync(@"/v1/nothing");
            using HttpResponseMessage notAllowed = await Server.Client.GetAsync(@"/v1/projects/compile");
            using HttpResponseMessage info = await Server.Client.GetAsync(@"/v1/info");
            using HttpResponseMessage health = await Server.Client.GetAsync(@"/health/live");

            HttpResponseMessage[] responses = [answer, problem, notFound, notAllowed, info, health];
            string[] ids = [.. responses.Select(x => x.Headers.GetValues(ResponseHeadersMiddleware.RequestIdHeader).ShouldHaveSingleItem())];

            ids.ShouldAllBe(x => s_RequestId.IsMatch(x));
            ids.Distinct().Count().ShouldBe(ids.Length);
            responses.ShouldAllBe(x => x.Headers.GetValues(@"X-Content-Type-Options").Single() == @"nosniff");
            responses.ShouldAllBe(x => !x.Headers.Contains(@"Zpp-Job-Id"));
        }

        [Fact]
        public async Task Every_Given_AnAnswerFromTheApi_Then_IsNotToBeStored()
        {
            using HttpResponseMessage answer = await PostCompileAsync();
            using HttpResponseMessage problem = await Server.Client.PostAsync(@"/v1/projects/compile", new MultipartFormDataContent());
            using HttpResponseMessage notFound = await Server.Client.GetAsync(@"/v1/nothing");
            using HttpResponseMessage notAllowed = await Server.Client.GetAsync(@"/v1/projects/compile");

            foreach (HttpResponseMessage response in new[] { answer, problem, notFound, notAllowed })
            {
                response.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue();
            }
        }

        [Fact]
        public async Task Every_Given_ATraceparent_Then_TheRequestKeepsItsTraceId()
        {
            const string traceId = @"4bf92f3577b34da6a3ce929d0e0e4736";
            using var content = new MultipartFormDataContent();
            using var request = new HttpRequestMessage(HttpMethod.Post, @"/v1/projects/compile") { Content = content };
            request.Headers.Add(@"traceparent", $@"00-{traceId}-00f067aa0ba902b7-01");
            using HttpResponseMessage response = await Server.Client.SendAsync(request);
            ProblemResponse problem = await RunningServer.ReadProblemAsync(response);

            response.Headers.GetValues(ResponseHeadersMiddleware.RequestIdHeader).ShouldHaveSingleItem().ShouldBe(traceId);
            problem.TraceId.ShouldBe(traceId);
        }

        [Theory]
        [InlineData(@"garbage")]
        [InlineData(@"00-4bf92f3577b34da6a3ce929d0e0e4736-0000000000000000-01")]
        [InlineData(@"ff-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01")]
        public async Task Every_Given_ATraceparentThatIsNotValid_Then_TheRequestGetsATraceIdOfItsOwn(string traceparent)
        {
            using var content = new MultipartFormDataContent();
            using var request = new HttpRequestMessage(HttpMethod.Post, @"/v1/projects/compile") { Content = content };
            request.Headers.Add(@"traceparent", traceparent);
            using HttpResponseMessage response = await Server.Client.SendAsync(request);
            ProblemResponse problem = await RunningServer.ReadProblemAsync(response);

            string requestId = response.Headers.GetValues(ResponseHeadersMiddleware.RequestIdHeader).ShouldHaveSingleItem();
            requestId.ShouldMatch(s_RequestId.ToString());
            requestId.ShouldNotBe(@"4bf92f3577b34da6a3ce929d0e0e4736");
            problem.TraceId.ShouldBe(requestId);
        }

        private async Task<HttpResponseMessage> PostCompileAsync()
        {
            using var content = RunningServer.CompileContent(TwoScenarios(), @"two-scenarios.zpp");
            return await Server.Client.PostAsync(@"/v1/projects/compile", content);
        }
    }
}
