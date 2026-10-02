using Shouldly;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit;
using Zametek.Engine.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for zpp serve's API: what it takes a job as, what it refuses and
    /// with which status, the zip it answers with when asked for one, a
    /// project's scenarios, what it says about itself, and its health. What a
    /// job prints and produces - the same as zpp - JobEndpointsParityTests pins.
    /// </summary>
    public class JobEndpointsTests
        : IClassFixture<EngineFixture>, IAsyncLifetime
    {
        private const string c_Now = @"2026-10-02T09:00:00+01:00";

        private static readonly ServeLimits s_Limits = new();

        private readonly EngineFixture m_Engine;
        private RunningServer? m_Server;

        public JobEndpointsTests(EngineFixture engine)
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

        private static byte[] TwoScenarios()
        {
            return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, @"Assets", @"two-scenarios.zpp"));
        }

        private static async Task<string> ProblemDetailAsync(HttpResponseMessage response)
        {
            response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe(@"application/problem+json");
            using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return problem.RootElement.GetProperty(@"detail").GetString() ?? string.Empty;
        }

        private async Task<HttpResponseMessage> PostJobAsync(JobOptions? options = null)
        {
            using var content = RunningServer.JobContent(TwoScenarios(), @"two-scenarios.zpp", options);
            return await Server.Client.PostAsync(@"/v1/jobs", content);
        }

        private async Task<HttpResponseMessage> PostJobAsync(string optionsJson)
        {
            using var content = RunningServer.JobContent(TwoScenarios(), @"two-scenarios.zpp");
            content.Add(new StringContent(optionsJson), @"options");
            return await Server.Client.PostAsync(@"/v1/jobs", content);
        }

        [Fact]
        public async Task RunJob_Given_ABodyThatIsNotMultipart_Then_UnsupportedMediaType()
        {
            using var content = new StringContent(@"{}", Encoding.UTF8, @"application/json");
            using HttpResponseMessage response = await Server.Client.PostAsync(@"/v1/jobs", content);

            response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
            (await ProblemDetailAsync(response)).ShouldBe(Resource.ProjectPlan.Messages.Message_ServeRequestNotMultipart);
        }

        [Fact]
        public async Task RunJob_Given_NoPlan_Then_BadRequest()
        {
            using var content = new MultipartFormDataContent { { new StringContent(@"{}"), @"options" } };
            using HttpResponseMessage response = await Server.Client.PostAsync(@"/v1/jobs", content);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await ProblemDetailAsync(response)).ShouldBe(Resource.ProjectPlan.Messages.Message_ServeRequestNeedsPlan);
        }

        [Fact]
        public async Task RunJob_Given_BothAnInputAndAnImport_Then_BadRequest()
        {
            using var content = RunningServer.JobContent(TwoScenarios(), @"two-scenarios.zpp");
            content.Add(new ByteArrayContent(TwoScenarios()), @"import", @"plan.xlsx");
            using HttpResponseMessage response = await Server.Client.PostAsync(@"/v1/jobs", content);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await ProblemDetailAsync(response)).ShouldBe(Resource.ProjectPlan.Messages.Message_ServeRequestNeedsPlan);
        }

        [Theory]
        [InlineData(@"plan.mpp")]
        [InlineData(@"plan.xml")]
        [InlineData(@"plan.csv")]
        public async Task RunJob_Given_AnImportThatIsNotAWorkbook_Then_UnsupportedMediaType(string filename)
        {
            using var content = RunningServer.JobContent(TwoScenarios(), filename, part: @"import");
            using HttpResponseMessage response = await Server.Client.PostAsync(@"/v1/jobs", content);

            response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
            (await ProblemDetailAsync(response)).ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeCannotImport, filename));
        }

        [Fact]
        public async Task RunJob_Given_APlanWithoutAName_Then_BadRequest()
        {
            // Its outputs are named after it.
            using var content = RunningServer.JobContent(TwoScenarios(), @".zpp");
            using HttpResponseMessage response = await Server.Client.PostAsync(@"/v1/jobs", content);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await ProblemDetailAsync(response)).ShouldBe(Resource.ProjectPlan.Messages.Message_ServePlanNeedsName);
        }

        [Theory]
        [InlineData(@"{""metricFormat"":""json""}")]
        [InlineData(@"{""metricsFormat"":2}")]
        [InlineData(@"{""compileTimeout"":""500""}")]
        [InlineData(@"{""gantt"":{""format"":""png""}}")]
        [InlineData(@"{""gantt"":{""format"":""gif"",""width"":800,""height"":600}}")]
        [InlineData(@"{")]
        public async Task RunJob_Given_OptionsThatAreNotValid_Then_BadRequest(string options)
        {
            // A misspelt option, an enum by number, a number in quotes, a chart without its size, a format zpp does not
            // have, and JSON that is not JSON.
            using HttpResponseMessage response = await PostJobAsync(options);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await ProblemDetailAsync(response)).ShouldStartWith(string.Format(Resource.ProjectPlan.Messages.Message_ServeOptionsNotValid, string.Empty));
        }

        [Theory]
        [InlineData(@"png")]
        [InlineData(@"PNG")]
        [InlineData(@"Png")]
        public async Task RunJob_Given_AFormatInAnyCase_Then_TakesIt(string format)
        {
            using HttpResponseMessage response = await PostJobAsync($@"{{""gantt"":{{""format"":""{format}"",""width"":80,""height"":60}}}}");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            JobResponse job = await RunningServer.ReadAsync<JobResponse>(response);
            job.Outputs.ShouldHaveSingleItem().FileName.ShouldBe(@"two-scenarios-gantt.png");
        }

        [Theory]
        [InlineData(0, 600)]
        [InlineData(800, 0)]
        [InlineData(5001, 600)]
        [InlineData(800, 5001)]
        public async Task RunJob_Given_AChartSizeOutsideTheLimits_Then_BadRequest(int width, int height)
        {
            using HttpResponseMessage response = await PostJobAsync(new JobOptions
            {
                Gantt = new ChartOptions { Format = PlotExport.Png, Width = width, Height = height },
            });

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await ProblemDetailAsync(response)).ShouldBe(string.Format(
                Resource.ProjectPlan.Messages.Message_ServeChartSizeOutOfRange, @"gantt", s_Limits.MaxChartWidth, s_Limits.MaxChartHeight));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(60_001)]
        public async Task RunJob_Given_ACompileTimeoutOutsideTheLimit_Then_BadRequest(int compileTimeout)
        {
            // Unlike zpp, the server does not let a job switch the limit off.
            using HttpResponseMessage response = await PostJobAsync(new JobOptions { CompileTimeout = compileTimeout });

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await ProblemDetailAsync(response)).ShouldBe(string.Format(
                Resource.ProjectPlan.Messages.Message_ServeCompileTimeoutOutOfRange, s_Limits.MaxCompileTimeoutMilliseconds));
        }

        [Fact]
        public async Task RunJob_Given_TheLongestCompileTimeoutTheLimitAllows_Then_RunsTheJob()
        {
            using HttpResponseMessage response = await PostJobAsync(new JobOptions { CompileTimeout = s_Limits.MaxCompileTimeoutMilliseconds });

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await RunningServer.ReadAsync<JobResponse>(response)).ExitCode.ShouldBe(0);
        }

        [Fact]
        public async Task RunJob_Given_NowWithoutAnOffset_Then_BadRequest()
        {
            using HttpResponseMessage response = await PostJobAsync(new JobOptions { Now = @"2026-10-02T09:00:00" });

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await ProblemDetailAsync(response)).ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_OptionMustBeADateTimeWithOffset, @"'now'"));
        }

        [Fact]
        public async Task RunJob_Given_AScenarioWithAnImport_Then_BadRequest()
        {
            // As zpp's --scenario is only valid with --input.
            using var content = RunningServer.JobContent(TwoScenarios(), @"plan.xlsx", new JobOptions { Scenario = @"Beta" }, @"import");
            using HttpResponseMessage response = await Server.Client.PostAsync(@"/v1/jobs", content);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await ProblemDetailAsync(response)).ShouldBe(Resource.ProjectPlan.Messages.Message_ServeScenarioOnlyWithInput);
        }

        [Fact]
        public async Task RunJob_Given_OptionsInAFile_Then_TakesThem()
        {
            using var content = RunningServer.JobContent(TwoScenarios(), @"two-scenarios.zpp");
            content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(@"{""metricsFormat"":""json""}")), @"options", @"options.json");
            using HttpResponseMessage response = await Server.Client.PostAsync(@"/v1/jobs", content);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            JobResponse job = await RunningServer.ReadAsync<JobResponse>(response);
            using JsonDocument metrics = JsonDocument.Parse(job.Stdout);
            metrics.RootElement.GetProperty(@"TotalCost").GetDouble().ShouldBe(0);
        }

        [Fact]
        public async Task RunJob_Given_NoOptions_Then_RunsTheJobWithZppsDefaults()
        {
            using HttpResponseMessage response = await PostJobAsync();

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            JobResponse job = await RunningServer.ReadAsync<JobResponse>(response);
            job.ExitCode.ShouldBe(0);
            job.Stdout.ShouldStartWith(NewLineHelper.NewLine + @"| ");
            job.Stderr.ShouldBeEmpty();
            job.Outputs.ShouldBeEmpty();
            job.Metrics.ShouldNotBeNull().GetProperty(@"NetworkDuration").GetInt32().ShouldBe(5);
        }

        [Fact]
        public async Task RunJob_Given_AJob_Then_ItsIdIsInTheHeaderAndTheResponse()
        {
            using HttpResponseMessage response = await PostJobAsync();

            string jobId = response.Headers.GetValues(JobEndpoints.JobIdHeader).ShouldHaveSingleItem();
            jobId.ShouldNotBeNullOrWhiteSpace();
            (await RunningServer.ReadAsync<JobResponse>(response)).JobId.ShouldBe(jobId);
        }

        [Fact]
        public async Task RunJob_Given_AZipIsAccepted_Then_AnswersWithTheOutputsAndTheRestInResultJson()
        {
            var options = new JobOptions
            {
                Now = c_Now,
                Output = true,
                Gantt = new ChartOptions { Format = PlotExport.Svg, Width = 800, Height = 600 },
            };

            using HttpResponseMessage json = await PostJobAsync(options);
            JobResponse expected = await RunningServer.ReadAsync<JobResponse>(json);

            using var content = RunningServer.JobContent(TwoScenarios(), @"two-scenarios.zpp", options);
            using var request = new HttpRequestMessage(HttpMethod.Post, @"/v1/jobs") { Content = content };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(@"application/zip"));
            using HttpResponseMessage response = await Server.Client.SendAsync(request);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe(@"application/zip");
            response.Content.Headers.ContentDisposition.ShouldNotBeNull().FileName.ShouldNotBeNull().Trim('"').ShouldBe(@"two-scenarios.zip");

            using var zip = new ZipArchive(await response.Content.ReadAsStreamAsync(), ZipArchiveMode.Read);
            zip.Entries.Select(x => x.FullName).ShouldBe([@"two-scenarios.zpp", @"two-scenarios-gantt.svg", @"result.json"]);

            // Each output as the JSON response carries it, and every file stamped with the time the job ran at.
            foreach (JobResponseOutput output in expected.Outputs)
            {
                ZipArchiveEntry entry = zip.GetEntry(output.FileName).ShouldNotBeNull();
                using var bytes = new MemoryStream();
                await using (Stream stream = entry.Open())
                {
                    await stream.CopyToAsync(bytes);
                }
                bytes.ToArray().ShouldBe(output.Content);
            }

            DateTimeOffset now = DateTimeOffset.Parse(c_Now, CultureInfo.InvariantCulture);
            zip.Entries.ShouldAllBe(x => x.LastWriteTime.DateTime == now.DateTime);

            ZipArchiveEntry resultEntry = zip.GetEntry(@"result.json").ShouldNotBeNull();
            JobResponse result;
            await using (Stream stream = resultEntry.Open())
            {
                result = (await JsonSerializer.DeserializeAsync<JobResponse>(stream, JobEndpoints.JsonOptions)).ShouldNotBeNull();
            }

            result.ExitCode.ShouldBe(expected.ExitCode);
            result.Stdout.ShouldBe(expected.Stdout);
            result.Outputs.Select(x => (x.Kind, x.FileName, x.ContentType)).ShouldBe(expected.Outputs.Select(x => (x.Kind, x.FileName, x.ContentType)));
            result.Outputs.ShouldAllBe(x => x.Content == null);
        }

        [Fact]
        public async Task ListScenarios_Given_AnImport_Then_BadRequest()
        {
            // As zpp's --list-scenarios is only valid with --input.
            using var content = RunningServer.JobContent(TwoScenarios(), @"plan.xlsx", part: @"import");
            using HttpResponseMessage response = await Server.Client.PostAsync(@"/v1/scenarios", content);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await ProblemDetailAsync(response)).ShouldBe(Resource.ProjectPlan.Messages.Message_ServeScenariosNeedInput);
        }

        [Fact]
        public async Task ListScenarios_Given_BothAnInputAndAnImport_Then_BadRequest()
        {
            using var content = RunningServer.JobContent(TwoScenarios(), @"two-scenarios.zpp");
            content.Add(new ByteArrayContent(TwoScenarios()), @"import", @"plan.xlsx");
            using HttpResponseMessage response = await Server.Client.PostAsync(@"/v1/scenarios", content);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await ProblemDetailAsync(response)).ShouldBe(Resource.ProjectPlan.Messages.Message_ServeScenariosNeedInput);
        }

        [Fact]
        public async Task ListScenarios_Given_AFileThatIsNotAPlan_Then_SaysWhyAndFails()
        {
            using var content = RunningServer.JobContent(Encoding.UTF8.GetBytes(@"not a plan"), @"plan.zpp");
            using HttpResponseMessage response = await Server.Client.PostAsync(@"/v1/scenarios", content);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            ScenariosResponse scenarios = await RunningServer.ReadAsync<ScenariosResponse>(response);
            scenarios.ExitCode.ShouldBe((int)ExitCode.Failure);
            scenarios.Stderr.ShouldNotBeEmpty();
            scenarios.Scenarios.ShouldBeNull();
        }

        [Fact]
        public async Task GetInfo_Then_SaysWhatTheServerIs()
        {
            using HttpResponseMessage response = await Server.Client.GetAsync(@"/v1/info");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            InfoResponse info = await RunningServer.ReadAsync<InfoResponse>(response);
            info.Version.ShouldBe(Resource.ProjectPlan.Labels.Label_AppVersion);
            info.Culture.ShouldBe(CultureInfo.CurrentCulture.Name);
            info.TimeZone.ShouldBe(TimeZoneInfo.Local.Id);
            info.Limits.ShouldBe(s_Limits);
        }

        [Fact]
        public async Task Health_Given_AServerThatDoesNotWarmUp_Then_LiveButNeverReady()
        {
            using HttpResponseMessage live = await Server.Client.GetAsync(@"/health/live");
            using HttpResponseMessage ready = await Server.Client.GetAsync(@"/health/ready");

            live.StatusCode.ShouldBe(HttpStatusCode.OK);
            ready.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        }

        [Fact]
        public async Task RunJob_Given_AJobThatRunsOutOfTime_Then_GatewayTimeout()
        {
            // A limit of no time at all is one no job can keep to.
            await using RunningServer server = await RunningServer.StartAsync(
                m_Engine.JobRunner,
                new ServeSettings { Limits = s_Limits with { JobTimeoutSeconds = 0 } });

            using var job = RunningServer.JobContent(TwoScenarios(), @"two-scenarios.zpp");
            using HttpResponseMessage jobResponse = await server.Client.PostAsync(@"/v1/jobs", job);
            using var scenarios = RunningServer.JobContent(TwoScenarios(), @"two-scenarios.zpp");
            using HttpResponseMessage scenariosResponse = await server.Client.PostAsync(@"/v1/scenarios", scenarios);

            string detail = string.Format(Resource.ProjectPlan.Messages.Message_ServeJobTimedOut, 0);
            jobResponse.StatusCode.ShouldBe(HttpStatusCode.GatewayTimeout);
            (await ProblemDetailAsync(jobResponse)).ShouldBe(detail);
            scenariosResponse.StatusCode.ShouldBe(HttpStatusCode.GatewayTimeout);
            (await ProblemDetailAsync(scenariosResponse)).ShouldBe(detail);
        }

        [Fact]
        public async Task RunJob_Given_AJobWithinSmallerChartLimits_Then_RunsItAndRefusesAnyBigger()
        {
            await using RunningServer server = await RunningServer.StartAsync(
                m_Engine.JobRunner,
                new ServeSettings { Limits = s_Limits with { MaxChartWidth = 300, MaxChartHeight = 200 } });

            using var withinLimits = RunningServer.JobContent(TwoScenarios(), @"two-scenarios.zpp", new JobOptions
            {
                Gantt = new ChartOptions { Format = PlotExport.Png, Width = 300, Height = 200 },
            });
            using var tooWide = RunningServer.JobContent(TwoScenarios(), @"two-scenarios.zpp", new JobOptions
            {
                Gantt = new ChartOptions { Format = PlotExport.Png, Width = 301, Height = 200 },
            });

            using HttpResponseMessage ran = await server.Client.PostAsync(@"/v1/jobs", withinLimits);
            using HttpResponseMessage refused = await server.Client.PostAsync(@"/v1/jobs", tooWide);

            ran.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await RunningServer.ReadAsync<JobResponse>(ran)).Outputs.ShouldHaveSingleItem().Kind.ShouldBe(JobOutput.GanttChart);
            refused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }
    }
}
