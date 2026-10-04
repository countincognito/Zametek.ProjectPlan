using Shouldly;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Engine.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests that hold the description of zpp serve's API - docs/openapi.yaml - and the reference beside it - docs/API.md - to the
    /// server. The description has the routes the server maps, and no others. Each response it describes is one the server gives,
    /// as it says: the status, the headers, the type of the body and a body that fits its schema, with no member in it that the
    /// schema does not name; and each response the server gives to what these tests send it is one the description has. Each
    /// kind of problem, each code, each metric, each option and each limit is in the description as it is in the code, and each
    /// kind of problem has its section in the reference, at the address its type names.
    /// </summary>
    [Collection(ProgramExitCodeTests.CollectionName)]
    public class OpenApiContractTests
        : IClassFixture<EngineFixture>
    {
        private const string c_ApiKey = @"s3cr3t-k3y";

        private static readonly TimeSpan s_Settle = TimeSpan.FromMilliseconds(250);

        private readonly EngineFixture m_Engine;

        public OpenApiContractTests(EngineFixture engine)
        {
            m_Engine = engine;
        }

        private static OpenApiDescription Description => OpenApiDescription.Instance;

        private static byte[] Asset(string filename)
        {
            return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, @"Assets", filename));
        }

        private static string[] ReferenceLines()
        {
            return File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, @"Docs", @"API.md"));
        }

        // Options as a request sends them, which the description of the options says are valid: they are not sent if it does not.
        private static string AsTheDescriptionHasIt(string options)
        {
            using JsonDocument document = JsonDocument.Parse(options);
            IReadOnlyList<string> errors = Description.Validate(JsonNode.Parse(@"{""$ref"":""#/components/schemas/CompileOptions""}")!, document.RootElement);

            string.Join(Environment.NewLine, errors).ShouldBeEmpty();
            return options;
        }

        // The part of the reference under a heading - from the line after it to the next heading of its level or above.
        private static string[] Section(string heading)
        {
            string[] lines = ReferenceLines();
            int start = Array.IndexOf(lines, heading);
            start.ShouldBeGreaterThanOrEqualTo(0, $@"{heading} is not in the reference.");
            int level = heading.IndexOf(' ');

            int end = start + 1;
            while (end < lines.Length
                && !(lines[end].StartsWith('#') && lines[end].IndexOf(' ') <= level))
            {
                end++;
            }

            return lines[(start + 1)..end];
        }

        // The members of a JSON object - what the server writes, or reads - by name.
        private static string[] Names(object value, JsonSerializerOptions options)
        {
            return [.. ((JsonObject)JsonSerializer.SerializeToNode(value, value.GetType(), options)!).Select(x => x.Key)];
        }

        private static string[] Names(JsonObject schema)
        {
            return [.. schema[@"properties"]!.AsObject().Select(x => x.Key)];
        }

        private static JsonObject Schema(string component)
        {
            return Description.Components(@"schemas")[component]!.AsObject();
        }

        // What an enumeration is called in the API: each member as the server writes it.
        private static string[] Values<T>()
            where T : struct, Enum
        {
            return [.. Enum.GetValues<T>().Select(x => JsonSerializer.SerializeToNode(x, ProjectEndpoints.JsonOptions)!.GetValue<string>())];
        }

        private static string[] Enumerated(JsonNode schema)
        {
            return [.. schema[@"enum"]!.AsArray().Where(x => x is not null).Select(x => x!.GetValue<string>())];
        }

        [Fact]
        public async Task Routes_Given_TheServer_Then_TheDescriptionHasEachOneAndNoOther()
        {
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner);

            IEnumerable<(string Method, string Path)> described = Description.Operations.Select(x => (x.Method, x.Path));

            server.Routes.ShouldBe(described, ignoreOrder: true);
        }

        [Fact]
        public async Task Responses_Given_EverySituationTheDescriptionHasAResponseFor_Then_EachIsAsDescribedAndNoneIsLeftOut()
        {
            var observations = new Observations(Description);

            await ObserveWhatAServerAnswersAsync(observations);
            await ObserveWhatAServerThatNeedsAKeyAnswersAsync(observations);
            await ObserveWhatAServerThatIsFullAnswersAsync(observations);
            await ObserveWhatAServerThatFailsAnswersAsync(observations);
            await ObserveWhatAServerThatWarmsUpAnswersAsync(observations);

            string.Join(Environment.NewLine, observations.Problems).ShouldBeEmpty();

            // Each response the description has was seen, and each kind of problem it can be - those of the others have the type their
            // status has.
            IEnumerable<string> described = Description.Operations.SelectMany(operation => operation.Operation[@"responses"]!.AsObject()
                .Select(response => $@"{operation.Method} {operation.Path} {response.Key}"));
            observations.Responses.ShouldBe(described, ignoreOrder: true);
            observations.Types.Where(x => x.StartsWith(ProblemHelper.TypeBase, StringComparison.Ordinal))
                .ShouldBe(Enum.GetValues<ProblemKind>().Select(ProblemHelper.GetType), ignoreOrder: true);
        }

        // A server as zpp serve builds it, and everything that can be asked of it that it answers as a client may ask it again,
        // and does not fail, nor is full.
        private async Task ObserveWhatAServerAnswersAsync(Observations observations)
        {
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner);
            HttpClient client = server.Client;

            using HttpResponseMessage info = await observations.SendAndKeepAsync(@"info", client, Get(@"/v1/info"), HttpStatusCode.OK);
            string etag = info.Headers.ETag.ShouldNotBeNull().ToString();
            await observations.SendAsync(@"info, as it was", client, Get(@"/v1/info", etag), HttpStatusCode.NotModified);
            await observations.SendAsync(@"info, as HTML", client, Get(@"/v1/info", accept: @"text/html"), HttpStatusCode.NotAcceptable);
            await observations.SendAsync(@"info, the headers", client, new HttpRequestMessage(HttpMethod.Head, @"/v1/info"), HttpStatusCode.OK);

            foreach (string path in new[] { @"/v1/info", @"/v1/projects/compile", @"/v1/projects/scenarios" })
            {
                await observations.SendAsync($@"what {path} takes", client, new HttpRequestMessage(HttpMethod.Options, path), HttpStatusCode.NoContent);
            }

            await observations.SendAsync(@"live", client, Get(@"/health/live"), HttpStatusCode.OK);
            await observations.SendAsync(@"not ready", client, Get(@"/health/ready"), HttpStatusCode.ServiceUnavailable);

            const string outputs = @"{""outputs"":{""project"":{},""ganttChart"":{""format"":""png"",""width"":300,""height"":200},""arrowGraph"":{""format"":""svg""}}}";

            // Every option, with a value of the kind that it takes - a compile timeout with a fraction of a second - and every output.
            const string everything = @"{""scenario"":""alpha"",""baseTheme"":""dark"",""metricsFormat"":""json"",""compileTimeout"":""PT1.5S"",""now"":""2026-10-03T09:00:00+01:00"",""outputs"":{""project"":{},""scenarioExport"":{},""ganttChart"":{""format"":""webp"",""width"":300,""height"":200},""arrowGraph"":{""format"":""graphml""},""vertexGraph"":{""format"":""dot""},""resourceChart"":{""format"":""bmp"",""width"":300,""height"":200},""earnedValueChart"":{""format"":""svg"",""width"":300,""height"":200},""scenarioChart"":{""format"":""jpeg"",""width"":300,""height"":200}}}";

            await observations.SendAsync(@"compile", client, Post(@"/v1/projects/compile", Plan()), HttpStatusCode.OK);
            await observations.SendAsync(@"compile, with every option", client, Post(@"/v1/projects/compile", Plan(options: AsTheDescriptionHasIt(everything))), HttpStatusCode.OK);
            await observations.SendAsync(@"compile, with outputs", client, Post(@"/v1/projects/compile", Plan(options: outputs)), HttpStatusCode.OK);

            // What the answer can include, as the description says it can: each is taken.
            foreach (string include in Enumerated(Description.Components(@"parameters")[@"Include"]![@"schema"]![@"items"]!))
            {
                await observations.SendAsync($@"compile, with {include}", client, Post($@"/v1/projects/compile?include={include}", Plan(options: outputs)), HttpStatusCode.OK);
                await observations.SendAsync($@"scenarios, with {include}", client, Post($@"/v1/projects/scenarios?include={include}", Plan()), HttpStatusCode.OK);
            }

            await observations.SendAsync(@"compile, as a zip", client, Post(@"/v1/projects/compile", Plan(options: outputs), @"application/zip"), HttpStatusCode.OK);
            await observations.SendAsync(@"compile, the include is not one", client, Post(@"/v1/projects/compile?include=metrics", Plan()), HttpStatusCode.BadRequest);
            await observations.SendAsync(@"compile, options that are not JSON", client, Post(@"/v1/projects/compile", Plan(options: @"{")), HttpStatusCode.BadRequest);
            await observations.SendAsync(@"compile, as HTML", client, Post(@"/v1/projects/compile", Plan(), @"text/html"), HttpStatusCode.NotAcceptable);
            await observations.SendAsync(@"compile, as JSON", client, Post(@"/v1/projects/compile", new StringContent(@"{}", Encoding.UTF8, @"application/json")), HttpStatusCode.UnsupportedMediaType);
            await observations.SendAsync(@"compile, a project to import", client, Post(@"/v1/projects/compile", Plan(part: @"import")), HttpStatusCode.UnsupportedMediaType);
            await observations.SendAsync(@"compile, options that are not valid", client, Post(@"/v1/projects/compile", Plan(options: @"{""compileTimeout"":""PT0S"",""outputs"":{""ev"":{}}}")), HttpStatusCode.UnprocessableEntity);
            await observations.SendAsync(@"compile, a file that is not a project", client, Post(@"/v1/projects/compile", Plan(plan: Encoding.UTF8.GetBytes(@"not a project"))), HttpStatusCode.UnprocessableEntity);
            await observations.SendAsync(@"compile, a project that does not compile", client, Post(@"/v1/projects/compile?include=console", Plan(@"broken-dependency.zpp")), HttpStatusCode.UnprocessableEntity);
            await observations.SendAsync(@"compile, a scenario that is not there", client, Post(@"/v1/projects/compile", Plan(options: @"{""scenario"":""Gamma""}")), HttpStatusCode.UnprocessableEntity);
            await observations.SendAsync(@"compile, options that are too large", client, Post(@"/v1/projects/compile", Plan(options: $@"{{""scenario"":""{new string('a', CompileOptionsHelper.MaxOptionsBytes)}""}}")), HttpStatusCode.RequestEntityTooLarge);

            await observations.SendAsync(@"scenarios", client, Post(@"/v1/projects/scenarios", Plan()), HttpStatusCode.OK);
            await observations.SendAsync(@"scenarios, the include is not one", client, Post(@"/v1/projects/scenarios?include=metrics", Plan()), HttpStatusCode.BadRequest);
            await observations.SendAsync(@"scenarios, as a zip", client, Post(@"/v1/projects/scenarios", Plan(), @"application/zip"), HttpStatusCode.NotAcceptable);
            await observations.SendAsync(@"scenarios, as JSON", client, Post(@"/v1/projects/scenarios", new StringContent(@"{}", Encoding.UTF8, @"application/json")), HttpStatusCode.UnsupportedMediaType);
            await observations.SendAsync(@"scenarios, a file that is not a project", client, Post(@"/v1/projects/scenarios", Plan(plan: Encoding.UTF8.GetBytes(@"not a project"))), HttpStatusCode.UnprocessableEntity);
        }

        private async Task ObserveWhatAServerThatNeedsAKeyAnswersAsync(Observations observations)
        {
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner, new ServeSettings { ApiKey = c_ApiKey });
            HttpClient client = server.Client;

            await observations.SendAsync(@"info, without the key", client, Get(@"/v1/info"), HttpStatusCode.Unauthorized);
            await observations.SendAsync(@"info, the headers, without the key", client, new HttpRequestMessage(HttpMethod.Head, @"/v1/info"), HttpStatusCode.Unauthorized);
            await observations.SendAsync(@"compile, without the key", client, Post(@"/v1/projects/compile", Plan()), HttpStatusCode.Unauthorized);
            await observations.SendAsync(@"scenarios, without the key", client, Post(@"/v1/projects/scenarios", Plan()), HttpStatusCode.Unauthorized);

            foreach (string path in new[] { @"/v1/info", @"/v1/projects/compile", @"/v1/projects/scenarios" })
            {
                await observations.SendAsync($@"what {path} takes, without the key", client, new HttpRequestMessage(HttpMethod.Options, path), HttpStatusCode.Unauthorized);
            }

            HttpRequestMessage withTheKey = Get(@"/v1/info");
            withTheKey.Headers.TryAddWithoutValidation(@"Authorization", $@"Bearer {c_ApiKey}");
            await observations.SendAsync(@"info, with the key", client, withTheKey, HttpStatusCode.OK);
        }

        private async Task ObserveWhatAServerThatIsFullAnswersAsync(Observations observations)
        {
            // Too large a request.
            await using (RunningServer small = await RunningServer.StartAsync(m_Engine.JobRunner, new ServeSettings { Limits = new ServeLimits { MaxUploadMegabytes = 1 } }))
            {
                foreach (string path in new[] { @"/v1/projects/compile", @"/v1/projects/scenarios" })
                {
                    HttpRequestMessage request = Post(path, Plan(plan: new byte[2 * 1024 * 1024]));
                    request.Headers.ExpectContinue = true;
                    await observations.SendAsync($@"{path}, too large", small.Client, request, HttpStatusCode.RequestEntityTooLarge);
                }
            }

            // As many jobs as the limits allow, and the next turned away.
            await using (RunningServer full = await RunningServer.StartAsync(m_Engine.JobRunner, new ServeSettings { Limits = new ServeLimits { MaxJobs = 1, MaxQueue = 0 } }))
            {
                using HeldContent held = await HeldContent.CreateAsync(Plan());
                Task<HttpResponseMessage> running = full.Client.PostAsync(@"/v1/projects/compile", held);
                await held.Started;
                await Task.Delay(s_Settle);

                await observations.SendAsync(@"compile, the server is full", full.Client, Post(@"/v1/projects/compile", Plan()), HttpStatusCode.ServiceUnavailable);
                await observations.SendAsync(@"scenarios, the server is full", full.Client, Post(@"/v1/projects/scenarios", Plan()), HttpStatusCode.ServiceUnavailable);

                held.Release();
                using HttpResponseMessage ran = await running;
                await observations.ObserveAsync(@"compile, held and released", @"POST", @"/v1/projects/compile", ran, HttpStatusCode.OK);
            }

            // A limit of no time at all is one no job can keep to.
            await using RunningServer slow = await RunningServer.StartAsync(m_Engine.JobRunner, new ServeSettings { Limits = new ServeLimits { JobTimeoutSeconds = 0 } });
            await observations.SendAsync(@"compile, out of time", slow.Client, Post(@"/v1/projects/compile", Plan()), HttpStatusCode.ServiceUnavailable);
            await observations.SendAsync(@"scenarios, out of time", slow.Client, Post(@"/v1/projects/scenarios", Plan()), HttpStatusCode.ServiceUnavailable);
        }

        private static async Task ObserveWhatAServerThatFailsAnswersAsync(Observations observations)
        {
            await using (FailingEngine engine = FailingEngine.WithAGanttChartThatCannotBeDrawn())
            await using (RunningServer server = await RunningServer.StartAsync(engine.JobRunner))
            {
                await observations.SendAsync(
                    @"compile, a chart that cannot be drawn",
                    server.Client,
                    Post(@"/v1/projects/compile?include=console", Plan(options: @"{""outputs"":{""project"":{},""ganttChart"":{""format"":""png"",""width"":300,""height"":200}}}")),
                    HttpStatusCode.InternalServerError);
            }

            await using (FailingEngine engine = FailingEngine.ThatFailsUnexpectedly())
            await using (RunningServer server = await RunningServer.StartAsync(engine.JobRunner))
            {
                await observations.SendAsync(@"compile, a failure nobody expected", server.Client, Post(@"/v1/projects/compile?include=console", Plan()), HttpStatusCode.InternalServerError);
                await observations.SendAsync(@"scenarios, a failure nobody expected", server.Client, Post(@"/v1/projects/scenarios", Plan()), HttpStatusCode.InternalServerError);
            }

            await using (FailingEngine engine = FailingEngine.WithACompilationThatRunsOutOfTime())
            await using (RunningServer server = await RunningServer.StartAsync(engine.JobRunner))
            {
                await observations.SendAsync(@"compile, a compilation that runs out of time", server.Client, Post(@"/v1/projects/compile?include=console", Plan()), HttpStatusCode.UnprocessableEntity);
            }
        }

        private async Task ObserveWhatAServerThatWarmsUpAnswersAsync(Observations observations)
        {
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner, new ServeSettings(), warmUp: true);

            HttpStatusCode status = HttpStatusCode.ServiceUnavailable;
            for (int attempt = 0; attempt < 600 && status != HttpStatusCode.OK; attempt++)
            {
                using HttpResponseMessage ready = await server.Client.GetAsync(@"/health/ready");
                status = ready.StatusCode;

                if (status != HttpStatusCode.OK)
                {
                    await Task.Delay(100);
                }
            }

            await observations.SendAsync(@"ready", server.Client, Get(@"/health/ready"), HttpStatusCode.OK);
        }

        [Fact]
        public void Examples_Given_TheDescription_Then_EachFitsItsSchemaAndNoneIsLeftUnused()
        {
            var used = new HashSet<string>();
            var problems = new List<string>();

            foreach ((string method, string path, JsonObject operation) in Description.Operations)
            {
                foreach ((string status, JsonNode? responseNode) in operation[@"responses"]!.AsObject())
                {
                    JsonObject response = Description.Resolve(responseNode!).AsObject();

                    foreach ((string mediaType, JsonNode? media) in response[@"content"]?.AsObject() ?? [])
                    {
                        foreach ((string name, JsonNode? exampleNode) in media![@"examples"]?.AsObject() ?? [])
                        {
                            used.Add(name);
                            JsonObject example = Description.Resolve(exampleNode!).AsObject();
                            JsonNode schema = media[@"schema"] ?? throw new InvalidOperationException($@"{method} {path} {status} {mediaType} has examples and no schema.");

                            using JsonDocument value = JsonDocument.Parse(example[@"value"]!.ToJsonString());
                            problems.AddRange(Description.Validate(schema, value.RootElement).Select(x => $@"{method} {path} {status} {mediaType} {name}: {x}"));
                        }
                    }
                }
            }

            string.Join(Environment.NewLine, problems).ShouldBeEmpty();

            // Every example has a response it is the example of: by the name it has there, which says which situation it is of.
            var all = Description.Components(@"examples").Select(x => x.Key).ToHashSet();
            var referenced = new HashSet<string>(
                Description.Root.ToJsonString().Split(@"#/components/examples/").Skip(1).Select(x => x[..x.IndexOf('"')]));
            referenced.ShouldBe(all, ignoreOrder: true);
            used.Count.ShouldBeGreaterThanOrEqualTo(all.Count);
        }

        [Theory]
        [InlineData(@"PT5S", true)]
        [InlineData(@"PT0S", true)]
        [InlineData(@"PT2M", true)]
        [InlineData(@"PT1H30M", true)]
        [InlineData(@"P1D", true)]
        [InlineData(@"P1DT2H", true)]
        [InlineData(@"P2DT3H4M5.678S", true)]
        [InlineData(@"PT0.001S", true)]
        [InlineData(@"PT1.5S", true)]
        [InlineData(@"P", false)]
        [InlineData(@"PT", false)]
        [InlineData(@"P1DT", false)]
        [InlineData(@"5S", false)]
        [InlineData(@"PT5", false)]
        [InlineData(@"P1Y", false)]
        [InlineData(@"P1M", false)]
        [InlineData(@"P1W", false)]
        [InlineData(@"PT-5S", false)]
        [InlineData(@"pt5s", false)]
        [InlineData(@"PT5.S", false)]
        [InlineData(@"PT.5S", false)]
        [InlineData(@"PT5S ", false)]
        [InlineData(@"PT5M3H", false)]
        public void Durations_Given_AText_Then_TheDescriptionAndTheServerTakeTheSame(string text, bool isDuration)
        {
            IsoDurationHelper.TryParse(text, out _).ShouldBe(isDuration);
            new Regex(Schema(@"Duration")[@"pattern"]!.GetValue<string>()).IsMatch(text).ShouldBe(isDuration);
        }

        [Fact]
        public void Responses_Given_TheDescription_Then_EachSaysWhatTheGuideRequiresOfIt()
        {
            var problems = new List<string>();

            foreach ((string method, string path, JsonObject operation) in Description.Operations)
            {
                foreach ((string status, JsonNode? node) in operation[@"responses"]!.AsObject())
                {
                    JsonObject headers = Description.Resolve(node!).AsObject()[@"headers"]?.AsObject() ?? [];
                    string name = $@"{method} {path} {status}";

                    // HDR-8: every response carries the id of its request.
                    RequireHeader(headers, @"Request-Id", name, problems);

                    // HDR-6: a server that cannot take a request now says when to come back; STS-6: a request that needs a key is told how to give it.
                    if (status == @"503"
                        && path.StartsWith(@"/v1/", StringComparison.Ordinal))
                    {
                        RequireHeader(headers, @"Retry-After", name, problems);
                    }

                    if (status == @"401")
                    {
                        RequireHeader(headers, @"WWW-Authenticate", name, problems);
                    }
                }
            }

            string.Join(Environment.NewLine, problems).ShouldBeEmpty();
        }

        // A header that a response has, and always has, which is the description's saying that it is required.
        private static void RequireHeader(
            JsonObject headers,
            string name,
            string response,
            List<string> problems)
        {
            if (!headers.TryGetPropertyValue(name, out JsonNode? header))
            {
                problems.Add($@"{response}: has no {name}");
            }
            else if (Description.Resolve(header!).AsObject()[@"required"]?.GetValue<bool>() != true)
            {
                problems.Add($@"{response}: does not require {name}");
            }
        }

        [Fact]
        public void ProblemKinds_Given_TheCode_Then_EachIsDescribedExampledAndDocumented()
        {
            foreach (ProblemKind kind in Enum.GetValues<ProblemKind>())
            {
                string name = $@"{kind}Problem";
                JsonObject own = Schema(name)[@"allOf"]![1]!.AsObject();

                JsonNode type = own[@"properties"]![@"type"]![@"const"]!;
                JsonNode status = own[@"properties"]![@"status"]![@"const"]!;
                type.GetValue<string>().ShouldBe(ProblemHelper.GetType(kind), name);
                status.GetValue<int>().ShouldBe(ProblemHelper.GetStatus(kind), name);

                JsonObject example = Description.Components(@"examples")[kind.ToString()]!.AsObject()[@"value"]!.AsObject();
                example[@"type"]!.GetValue<string>().ShouldBe(ProblemHelper.GetType(kind), name);
                example[@"status"]!.GetValue<int>().ShouldBe(ProblemHelper.GetStatus(kind), name);
                example[@"title"]!.GetValue<string>().ShouldBe(ProblemHelper.GetTitle(kind), name);
            }

            // The problems the description has are those, and the sections of the reference are those, under their slugs.
            Description.Components(@"schemas").Select(x => x.Key).Where(x => x.EndsWith(@"Problem", StringComparison.Ordinal) && x is not (@"Problem" or @"StatusProblem"))
                .ShouldBe(Enum.GetValues<ProblemKind>().Select(x => $@"{x}Problem"), ignoreOrder: true);

            string[] slugs = [.. Section(@"### The kinds of problem").Where(x => x.StartsWith(@"#### ", StringComparison.Ordinal)).Select(x => x[5..].Trim())];
            slugs.ShouldBe(Enum.GetValues<ProblemKind>().Select(ProblemHelper.GetSlug), ignoreOrder: true);
        }

        [Fact]
        public void Codes_Given_TheCode_Then_TheDescriptionAndTheReferenceListEachOne()
        {
            string[] codes = [.. typeof(ProblemCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(x => x.IsLiteral).Select(x => (string)x.GetRawConstantValue()!)];
            codes.ShouldNotBeEmpty();

            JsonArray anyOf = Schema(@"ProblemError")[@"properties"]![@"code"]![@"anyOf"]!.AsArray();
            Enumerated(anyOf[0]!).ShouldBe(codes, ignoreOrder: true);

            string[] reference = [.. Section(@"### Codes")
                .Where(x => x.StartsWith(@"| `", StringComparison.Ordinal))
                .SelectMany(x => Regex.Matches(x.Split('|')[1], @"`([A-Za-z]+)`").Select(m => m.Groups[1].Value))];
            reference.ShouldBe(codes, ignoreOrder: true);

            // The compiler's own, which the second schema names by what they look like.
            var pattern = new Regex(anyOf[1]![@"pattern"]!.GetValue<string>());
            Type compilerCodes = typeof(GraphCompilationErrorModel).GetProperty(nameof(GraphCompilationErrorModel.ErrorCode))!.PropertyType;
            Enum.GetNames(compilerCodes).ShouldAllBe(x => pattern.IsMatch(x));
        }

        [Fact]
        public void Schemas_Given_WhatTheServerWrites_Then_EachNamesTheMembersTheServerWrites()
        {
            JsonSerializerOptions written = ProjectEndpoints.JsonOptions;

            var metrics = new MetricsResponse();
            string[] metricNames = Names(metrics, written);
            Names(Schema(@"Metrics")).ShouldBe(metricNames, ignoreOrder: true);
            Schema(@"Metrics")[@"required"]!.AsArray().Select(x => x!.GetValue<string>()).ShouldBe(metricNames, ignoreOrder: true);

            var transcript = new JobTranscriptEntry { Kind = JobTranscriptKind.Display, Text = @"t", HasErrors = true, Index = 0 };
            var console = new ConsoleResponse(0, string.Empty, string.Empty, [transcript]);
            var output = new OutputResponse(JobOutput.Project, @"plan.zpp", @"application/json", [1]);
            var error = new ProblemError { Pointer = @"#/project", Parameter = @"include", Code = @"unreadable", Detail = @"d" };
            var problem = new ProblemResponse { Type = @"t", Title = @"t", Status = 400, Detail = @"d", TraceId = @"i", Errors = [error], Metrics = metrics, Outputs = [output], Console = console };
            var limits = LimitsResponse.From(new ServeLimits());

            Names(Schema(@"Console")).ShouldBe(Names(console, written), ignoreOrder: true);
            Names(Schema(@"TranscriptEntry")).ShouldBe(Names(transcript, written), ignoreOrder: true);
            Names(Schema(@"Output")).ShouldBe(Names(output, written), ignoreOrder: true);
            Names(Schema(@"ProblemError")).ShouldBe(Names(error, written), ignoreOrder: true);
            Names(Schema(@"Limits")).ShouldBe(Names(limits, written), ignoreOrder: true);
            Names(Schema(@"InfoResponse")).ShouldBe(Names(new InfoResponse(@"v", @"c", @"z", limits), written), ignoreOrder: true);
            Names(Schema(@"Scenario")).ShouldBe(Names(new ScenarioSummary(@"p", Guid.Empty, true, true), written), ignoreOrder: true);
            Names(Schema(@"ScenariosResponse")).ShouldBe(Names(new ScenariosResponse([], console), written), ignoreOrder: true);
            Names(Schema(@"CompileResponse")).ShouldBe(Names(new CompileResponse(metrics, [output], console), written), ignoreOrder: true);

            // A problem is the members every one has, and those of the kind that has more.
            string[] problemNames = [.. Names(Schema(@"Problem")), .. Names(Schema(@"OutputFailedProblem")[@"allOf"]![1]!.AsObject()).Where(x => x is not (@"type" or @"status"))];
            problemNames.ShouldBe(Names(problem, written), ignoreOrder: true);

            Schema(@"Metrics")[@"properties"]![@"projectFinishDate"]![@"format"]!.GetValue<string>().ShouldBe(@"date");
        }

        [Fact]
        public void Schemas_Given_WhatTheServerReads_Then_EachNamesTheMembersAndTheValuesItTakes()
        {
            JsonSerializerOptions read = JobJsonHelper.ServerOptions;

            var options = new CompileOptions
            {
                Scenario = @"s",
                BaseTheme = BaseTheme.Dark,
                MetricsFormat = MetricsExport.Json,
                CompileTimeout = TimeSpan.FromSeconds(1),
                Now = @"2026-10-03T09:00:00+01:00",
                Outputs = new OutputsOptions
                {
                    Project = new ProjectOptions(),
                    ScenarioExport = new ScenarioExportOptions(),
                    GanttChart = new ChartOptions { Width = 1, Height = 1 },
                    ArrowGraph = new GraphOptions(),
                    VertexGraph = new GraphOptions(),
                    ResourceChart = new ChartOptions { Width = 1, Height = 1 },
                    EarnedValueChart = new ChartOptions { Width = 1, Height = 1 },
                    ScenarioChart = new ChartOptions { Width = 1, Height = 1 },
                },
            };

            Names(Schema(@"CompileOptions")).ShouldBe(Names(options, read), ignoreOrder: true);
            Names(Schema(@"OutputsOptions")).ShouldBe(Names(options.Outputs!, read), ignoreOrder: true);
            Names(Schema(@"ChartOptions")).ShouldBe(Names(new ChartOptions { Width = 1, Height = 1 }, read), ignoreOrder: true);
            Names(Schema(@"GraphOptions")).ShouldBe(Names(new GraphOptions(), read), ignoreOrder: true);
            Schema(@"ChartOptions")[@"required"]!.AsArray().Select(x => x!.GetValue<string>()).ShouldBe([@"width", @"height"], ignoreOrder: true);

            Enumerated(Schema(@"CompileOptions")[@"properties"]![@"baseTheme"]!).ShouldBe([.. Values<BaseTheme>()], ignoreOrder: true);
            Enumerated(Schema(@"CompileOptions")[@"properties"]![@"metricsFormat"]!).ShouldBe([.. Values<MetricsExport>()], ignoreOrder: true);
            Enumerated(Schema(@"ChartOptions")[@"properties"]![@"format"]!).ShouldBe([.. Values<PlotExport>()], ignoreOrder: true);
            Enumerated(Schema(@"GraphOptions")[@"properties"]![@"format"]!).ShouldBe([.. Values<GraphExport>()], ignoreOrder: true);
            Enumerated(Schema(@"TranscriptEntry")[@"properties"]![@"kind"]!).ShouldBe([.. Values<JobTranscriptKind>()], ignoreOrder: true);

            // An output is asked for by the name it is answered with.
            string[] outputs = [.. Values<JobOutput>()];
            Enumerated(Schema(@"Output")[@"properties"]![@"kind"]!).ShouldBe(outputs, ignoreOrder: true);
            Names(Schema(@"OutputsOptions")).ShouldBe(outputs, ignoreOrder: true);
        }

        private static HttpRequestMessage Get(
            string path,
            string? ifNoneMatch = null,
            string? accept = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);

            if (ifNoneMatch is not null)
            {
                request.Headers.TryAddWithoutValidation(@"If-None-Match", ifNoneMatch);
            }

            if (accept is not null)
            {
                request.Headers.TryAddWithoutValidation(@"Accept", accept);
            }

            return request;
        }

        private static HttpRequestMessage Post(
            string path,
            HttpContent content,
            string? accept = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = content };

            if (accept is not null)
            {
                request.Headers.TryAddWithoutValidation(@"Accept", accept);
            }

            return request;
        }

        // A request to compile or list a project: the plan - the two scenarios' project, unless another is given - as the
        // part that is named, and the options, if any, as JSON.
        private static MultipartFormDataContent Plan(
            string asset = @"two-scenarios.zpp",
            string? options = null,
            string part = @"project",
            byte[]? plan = null)
        {
            MultipartFormDataContent content = RunningServer.CompileContent(plan ?? Asset(asset), asset, null, part);

            if (options is not null)
            {
                content.Add(new StringContent(options, Encoding.UTF8, @"application/json"), @"options");
            }

            return content;
        }

        // What the server answered to each request a test sent it, held to the description of the API.
        private sealed class Observations
        {
            private readonly OpenApiDescription m_Description;
            private readonly List<string> m_Problems = [];
            private readonly HashSet<string> m_Responses = [];
            private readonly HashSet<string> m_Types = [];

            public Observations(OpenApiDescription description)
            {
                m_Description = description;
            }

            // What is wrong, each in the words of the request that shows it.
            public IReadOnlyList<string> Problems => m_Problems;

            // Each response seen, as the method, the path and the status.
            public IReadOnlyCollection<string> Responses => m_Responses;

            // The type of each problem seen.
            public IReadOnlyCollection<string> Types => m_Types;

            public async Task SendAsync(
                string situation,
                HttpClient client,
                HttpRequestMessage request,
                HttpStatusCode expected)
            {
                using HttpResponseMessage _ = await SendAndKeepAsync(situation, client, request, expected);
            }

            // The response, for a test that goes on to use what it said: it is the test's to dispose.
            public async Task<HttpResponseMessage> SendAndKeepAsync(
                string situation,
                HttpClient client,
                HttpRequestMessage request,
                HttpStatusCode expected)
            {
                using (request)
                {
                    // As the request has it before the client makes it of the server's address.
                    string path = request.RequestUri!.OriginalString.Split('?')[0];
                    HttpResponseMessage response = await client.SendAsync(request);
                    await ObserveAsync(situation, request.Method.Method, path, response, expected);
                    return response;
                }
            }

            public async Task ObserveAsync(
                string situation,
                string method,
                string path,
                HttpResponseMessage response,
                HttpStatusCode expected)
            {
                byte[] body = await response.Content.ReadAsByteArrayAsync();
                int status = (int)response.StatusCode;
                m_Responses.Add($@"{method} {path} {status}");

                if (response.StatusCode != expected)
                {
                    m_Problems.Add($@"{situation}: answered {status}, not {(int)expected}: {Encoding.UTF8.GetString(body)}");
                    return;
                }

                JsonObject? described = m_Description.FindResponse(method, path, status);

                if (described is null)
                {
                    m_Problems.Add($@"{situation}: {method} {path} {status} is not described");
                    return;
                }

                CheckHeaders(situation, response, described);
                CheckBody(situation, response, body, described, isHead: method == @"HEAD");
            }

            private void CheckHeaders(
                string situation,
                HttpResponseMessage response,
                JsonObject described)
            {
                foreach ((string name, JsonNode? headerNode) in described[@"headers"]?.AsObject() ?? [])
                {
                    JsonObject header = m_Description.Resolve(headerNode!).AsObject();
                    string? value = GetHeader(response, name);

                    if (value is null)
                    {
                        if (header[@"required"]?.GetValue<bool>() == true)
                        {
                            m_Problems.Add($@"{situation}: has no {name}, which is required");
                        }

                        continue;
                    }

                    JsonObject schema = header[@"schema"]!.AsObject();

                    if (schema[@"const"] is JsonNode constant
                        && value != constant.GetValue<string>())
                    {
                        m_Problems.Add($@"{situation}: {name} is {value}, not {constant.GetValue<string>()}");
                    }

                    if (schema[@"pattern"] is JsonNode pattern
                        && !Regex.IsMatch(value, pattern.GetValue<string>()))
                    {
                        m_Problems.Add($@"{situation}: {name} is {value}, which is not as {pattern.GetValue<string>()}");
                    }

                    if (schema[@"type"]?.GetValue<string>() == @"integer"
                        && !(int.TryParse(value, out int number) && number >= (schema[@"minimum"]?.GetValue<int>() ?? int.MinValue)))
                    {
                        m_Problems.Add($@"{situation}: {name} is {value}, which is not the whole number it is described as");
                    }
                }
            }

            private void CheckBody(
                string situation,
                HttpResponseMessage response,
                byte[] body,
                JsonObject described,
                bool isHead)
            {
                JsonObject? content = described[@"content"]?.AsObject();

                if (content is null)
                {
                    if (body.Length > 0)
                    {
                        m_Problems.Add($@"{situation}: has a body, and is described as having none");
                    }

                    return;
                }

                string? mediaType = response.Content.Headers.ContentType?.MediaType;

                if (mediaType is null
                    || content[mediaType] is not JsonObject media)
                {
                    m_Problems.Add($@"{situation}: is {mediaType}, which is not among {string.Join(@", ", content.Select(x => x.Key))}");
                    return;
                }

                // The answer to a HEAD has the headers of an answer to a GET - its type among them - and no body to hold to its schema.
                if (isHead)
                {
                    if (body.Length > 0)
                    {
                        m_Problems.Add($@"{situation}: has a body, which an answer to a HEAD never does");
                    }

                    return;
                }

                JsonNode schema = media[@"schema"]!;

                switch (mediaType)
                {
                    case @"application/json":
                    case @"application/problem+json":
                        CheckJson(situation, schema, body, response, isProblem: mediaType == @"application/problem+json");
                        break;
                    case @"application/zip":
                        // The zip holds the files, and result.json, which is the JSON answer less their contents.
                        using (var archive = new ZipArchive(new MemoryStream(body)))
                        {
                            ZipArchiveEntry? result = archive.GetEntry(@"result.json");

                            if (result is null)
                            {
                                m_Problems.Add($@"{situation}: has no result.json");
                                break;
                            }

                            using var stream = new MemoryStream();
                            using (Stream entry = result.Open())
                            {
                                entry.CopyTo(stream);
                            }

                            CheckJson(situation, content[@"application/json"]![@"schema"]!, stream.ToArray(), response, isProblem: false);
                        }

                        break;
                    case @"text/plain":
                        string text = Encoding.UTF8.GetString(body);
                        string expected = schema[@"const"]!.GetValue<string>();

                        if (text != expected)
                        {
                            m_Problems.Add($@"{situation}: says {text}, not {expected}");
                        }

                        break;
                    default:
                        m_Problems.Add($@"{situation}: is {mediaType}, which these tests cannot read");
                        break;
                }
            }

            private void CheckJson(
                string situation,
                JsonNode schema,
                byte[] body,
                HttpResponseMessage response,
                bool isProblem)
            {
                JsonDocument document;

                try
                {
                    document = JsonDocument.Parse(body);
                }
                catch (JsonException ex)
                {
                    m_Problems.Add($@"{situation}: is not JSON: {ex.Message}");
                    return;
                }

                using (document)
                {
                    m_Problems.AddRange(m_Description.Validate(schema, document.RootElement).Select(x => $@"{situation}: {x}"));

                    if (isProblem)
                    {
                        JsonElement root = document.RootElement;
                        m_Types.Add(root.GetProperty(@"type").GetString()!);

                        // The request's id is its trace id.
                        if (root.GetProperty(@"traceId").GetString() != GetHeader(response, @"Request-Id"))
                        {
                            m_Problems.Add($@"{situation}: its traceId is not its Request-Id");
                        }
                    }
                }
            }

            // A header as the server wrote it, which the client does not: it reads Cache-Control into its parts, and writes them in
            // an order of its own.
            private static string? GetHeader(
                HttpResponseMessage response,
                string name)
            {
                if (response.Headers.NonValidated.TryGetValues(name, out HeaderStringValues values)
                    || response.Content.Headers.NonValidated.TryGetValues(name, out values))
                {
                    return string.Join(@", ", values);
                }

                return null;
            }
        }
    }
}
