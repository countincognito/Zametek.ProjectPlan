using Shouldly;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Diagnostics.Metrics;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for how zpp serve's API is carried: what an OPTIONS says, what /v1/info is answered with - its members, its ETag
    /// and the 304 that a client that has it is answered with, HEAD, and what it refuses to be asked for - how what is answered
    /// as JSON is compressed, what a probe is answered with, what no response is without, and the protocols it takes over
    /// https.
    /// </summary>
    public class ProjectTransportTests
        : IClassFixture<EngineFixture>, IAsyncLifetime
    {
        private static readonly ServeLimits s_Limits = new() { MaxJobs = 4, MaxQueue = 8 };

        private readonly EngineFixture m_Engine;
        private RunningServer? m_Server;

        public ProjectTransportTests(EngineFixture engine)
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

        private async Task<HttpResponseMessage> SendAsync(
            HttpMethod method,
            string path,
            Action<HttpRequestHeaders>? headers = null,
            HttpContent? content = null)
        {
            using var request = new HttpRequestMessage(method, path) { Content = content };
            headers?.Invoke(request.Headers);
            return await Server.Client.SendAsync(request);
        }

        private static async Task<byte[]> DecompressAsync(
            HttpResponseMessage response,
            string encoding)
        {
            await using Stream stream = await response.Content.ReadAsStreamAsync();
            await using Stream decompressed = encoding switch
            {
                @"gzip" => new GZipStream(stream, CompressionMode.Decompress),
                @"br" => new BrotliStream(stream, CompressionMode.Decompress),
                _ => throw new InvalidOperationException(encoding),
            };
            using var bytes = new MemoryStream();
            await decompressed.CopyToAsync(bytes);
            return bytes.ToArray();
        }

        [Theory]
        [InlineData(@"/v1/projects/compile", @"POST, OPTIONS", @"multipart/form-data")]
        [InlineData(@"/v1/projects/scenarios", @"POST, OPTIONS", @"multipart/form-data")]
        [InlineData(@"/v1/info", @"GET, HEAD, OPTIONS", null)]
        public async Task Options_Given_AnEndpoint_Then_NoContentAndWhatItTakes(string path, string allow, string? acceptPost)
        {
            using HttpResponseMessage response = await SendAsync(HttpMethod.Options, path);

            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            response.Content.Headers.Allow.ShouldBe([.. allow.Split(',', StringSplitOptions.TrimEntries)]);

            if (acceptPost is null)
            {
                response.Headers.Contains(@"Accept-Post").ShouldBeFalse();
            }
            else
            {
                response.Headers.GetValues(@"Accept-Post").ShouldBe([acceptPost]);
            }

            (await response.Content.ReadAsByteArrayAsync()).ShouldBeEmpty();
            response.Headers.GetValues(@"X-Content-Type-Options").ShouldBe([@"nosniff"]);
            response.Headers.GetValues(ResponseHeadersMiddleware.RequestIdHeader).ShouldHaveSingleItem().ShouldMatch(@"^[0-9a-f]{32}$");
        }

        [Fact]
        public async Task Options_Given_AServerThatHasAKey_Then_AsksForItAsAnythingElseOfTheApiDoes()
        {
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner, new ServeSettings { ApiKey = @"0123456789abcdef0123456789abcdef" });

            using HttpResponseMessage response = await server.Client.SendAsync(new HttpRequestMessage(HttpMethod.Options, @"/v1/info"));

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Info_Given_AGet_Then_TheServersFactsWithItsLimitsAsISO8601DurationsAndAnIanaZone()
        {
            using HttpResponseMessage response = await SendAsync(HttpMethod.Get, @"/v1/info");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe(@"application/json");
            string json = await response.Content.ReadAsStringAsync();
            using JsonDocument document = JsonDocument.Parse(json);
            document.RootElement.EnumerateObject().Select(x => x.Name).ShouldBe([@"version", @"culture", @"timeZone", @"limits"]);
            document.RootElement.GetProperty(@"timeZone").GetString().ShouldBe(TimeZoneHelper.GetIanaId(TimeZoneInfo.Local));
            document.RootElement.GetProperty(@"limits").EnumerateObject().Select(x => x.Name)
                .ShouldBe([@"maxJobs", @"maxQueue", @"maxUploadMegabytes", @"maxChartWidth", @"maxChartHeight", @"jobTimeout", @"maxCompileTimeout"]);

            InfoResponse info = await RunningServer.ReadAsync<InfoResponse>(response);
            info.Limits.ShouldBe(LimitsResponse.From(s_Limits));
            info.Limits.JobTimeout.ShouldBe(TimeSpan.FromSeconds(120));
            json.ShouldContain(@"""jobTimeout"":""PT2M""");
            json.ShouldContain(@"""maxCompileTimeout"":""PT1M""");
        }

        [Fact]
        public async Task Info_Given_AGet_Then_AStrongETagAndAShortPrivateLifeInACache()
        {
            using HttpResponseMessage response = await SendAsync(HttpMethod.Get, @"/v1/info");

            response.Headers.ETag.ShouldNotBeNull().IsWeak.ShouldBeFalse();
            response.Headers.ETag.Tag.ShouldMatch(@"^""[0-9a-f]{32}""$");
            CacheControlHeaderValue cacheControl = response.Headers.CacheControl.ShouldNotBeNull();
            cacheControl.Private.ShouldBeTrue();
            cacheControl.NoStore.ShouldBeFalse();
            cacheControl.MaxAge.ShouldBe(TimeSpan.FromSeconds(60));
            response.Content.Headers.Contains(@"Expires").ShouldBeFalse();
        }

        [Fact]
        public async Task Info_Given_TwoGets_Then_TheSameETag()
        {
            using HttpResponseMessage first = await SendAsync(HttpMethod.Get, @"/v1/info");
            using HttpResponseMessage second = await SendAsync(HttpMethod.Get, @"/v1/info");

            second.Headers.ETag.ShouldBe(first.Headers.ETag);
        }

        [Fact]
        public async Task Info_Given_ServersWithOtherLimits_Then_OtherETags()
        {
            await using RunningServer other = await RunningServer.StartAsync(m_Engine.JobRunner, new ServeSettings { Limits = s_Limits with { MaxJobs = 5 } });

            using HttpResponseMessage first = await SendAsync(HttpMethod.Get, @"/v1/info");
            using HttpResponseMessage second = await other.Client.GetAsync(@"/v1/info");

            second.Headers.ETag.ShouldNotBe(first.Headers.ETag);
        }

        [Theory]
        [InlineData(@"{0}", HttpStatusCode.NotModified)]
        [InlineData(@"W/{0}", HttpStatusCode.NotModified)]
        [InlineData(@"*", HttpStatusCode.NotModified)]
        [InlineData(@"""other"", {0}", HttpStatusCode.NotModified)]
        [InlineData(@"{0}, ""other""", HttpStatusCode.NotModified)]
        [InlineData(@"""other""", HttpStatusCode.OK)]
        [InlineData(@"""0123456789abcdef0123456789abcdef""", HttpStatusCode.OK)]
        public async Task Info_Given_IfNoneMatch_Then_NotModifiedWhenItHasTheETag(string ifNoneMatch, HttpStatusCode expected)
        {
            using HttpResponseMessage first = await SendAsync(HttpMethod.Get, @"/v1/info");
            string tag = first.Headers.ETag.ShouldNotBeNull().Tag;

            using HttpResponseMessage response = await SendAsync(
                HttpMethod.Get,
                @"/v1/info",
                headers => headers.TryAddWithoutValidation(@"If-None-Match", string.Format(ifNoneMatch, tag)));

            response.StatusCode.ShouldBe(expected);

            if (expected == HttpStatusCode.NotModified)
            {
                (await response.Content.ReadAsByteArrayAsync()).ShouldBeEmpty();
                response.Headers.ETag.ShouldBe(first.Headers.ETag);
                response.Headers.CacheControl.ShouldNotBeNull().MaxAge.ShouldBe(TimeSpan.FromSeconds(60));
                response.Headers.GetValues(ResponseHeadersMiddleware.RequestIdHeader).ShouldHaveSingleItem().ShouldMatch(@"^[0-9a-f]{32}$");
            }
        }

        [Fact]
        public async Task Info_Given_AHead_Then_TheHeadersOfAGetAndNoBody()
        {
            using HttpResponseMessage get = await SendAsync(HttpMethod.Get, @"/v1/info");
            using HttpResponseMessage head = await SendAsync(HttpMethod.Head, @"/v1/info");

            head.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await head.Content.ReadAsByteArrayAsync()).ShouldBeEmpty();
            head.Headers.ETag.ShouldBe(get.Headers.ETag);
            head.Content.Headers.ContentType.ShouldBe(get.Content.Headers.ContentType);
            head.Content.Headers.ContentLength.ShouldBe((await get.Content.ReadAsByteArrayAsync()).Length);
        }

        [Theory]
        [InlineData(@"text/html")]
        [InlineData(@"application/zip")]
        public async Task Info_Given_AnAcceptItCannotMeet_Then_NotAcceptable(string accept)
        {
            using HttpResponseMessage response = await SendAsync(HttpMethod.Get, @"/v1/info", headers => headers.TryAddWithoutValidation(@"Accept", accept));
            ProblemResponse problem = await RunningServer.ReadProblemAsync(response);

            response.StatusCode.ShouldBe(HttpStatusCode.NotAcceptable);
            problem.Detail.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeNotAcceptable, @"application/json"));
        }

        [Theory]
        [InlineData(@"gzip")]
        [InlineData(@"br")]
        public async Task Compression_Given_AClientThatAcceptsIt_Then_JsonIsCompressedAndVariesWithTheEncoding(string encoding)
        {
            using HttpResponseMessage plain = await SendAsync(HttpMethod.Get, @"/v1/info");
            using HttpResponseMessage response = await SendAsync(HttpMethod.Get, @"/v1/info", headers => headers.TryAddWithoutValidation(@"Accept-Encoding", encoding));

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.Content.Headers.ContentEncoding.ShouldBe([encoding]);
            response.Headers.Vary.ShouldContain(@"Accept-Encoding");
            response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe(@"application/json");
            (await DecompressAsync(response, encoding)).ShouldBe(await plain.Content.ReadAsByteArrayAsync());
        }

        [Fact]
        public async Task Compression_Given_Https_Then_JsonIsCompressedThereToo()
        {
            string directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $@"zpp-tls-{Guid.NewGuid():N}")).FullName;

            try
            {
                using X509Certificate2 certificate = TestCertificates.CreateSelfSigned(directory);
                await using RunningServer server = await RunningServer.StartAsync(
                    m_Engine.JobRunner,
                    new ServeSettings
                    {
                        Listen = [ServeSettingsHelper.ParseListenAddress(@"https://127.0.0.1:0")],
                        Certificate = certificate,
                    },
                    handler: TestCertificates.Trusting(certificate));

                using var request = new HttpRequestMessage(HttpMethod.Get, @"/v1/info");
                request.Headers.TryAddWithoutValidation(@"Accept-Encoding", @"gzip");
                using HttpResponseMessage response = await server.Client.SendAsync(request);

                server.Address.ShouldStartWith(@"https://");
                response.Content.Headers.ContentEncoding.ShouldBe([@"gzip"]);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public async Task Tls_Given_AClientThatTakesAnyProtocol_Then_ServedOverTls12OrTls13()
        {
            string directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $@"zpp-tls-{Guid.NewGuid():N}")).FullName;

            try
            {
                using X509Certificate2 certificate = TestCertificates.CreateSelfSigned(directory);
                await using RunningServer server = await RunningServer.StartAsync(
                    m_Engine.JobRunner,
                    new ServeSettings
                    {
                        Listen = [ServeSettingsHelper.ParseListenAddress(@"https://127.0.0.1:0")],
                        Certificate = certificate,
                    },
                    handler: TestCertificates.Trusting(certificate));
                var address = new Uri(server.Address);

                using var tcp = new TcpClient();
                await tcp.ConnectAsync(address.Host, address.Port);
                await using var ssl = new SslStream(tcp.GetStream(), leaveInnerStreamOpen: false, (_, _, _, _) => true);
                await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = @"localhost" });

                ssl.SslProtocol.ShouldBeOneOf(SslProtocols.Tls12, SslProtocols.Tls13);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public async Task Compression_Given_AClientThatDoesNotAcceptIt_Then_JsonIsNot()
        {
            using HttpResponseMessage response = await SendAsync(HttpMethod.Get, @"/v1/info");

            response.Content.Headers.ContentEncoding.ShouldBeEmpty();
        }

        [Fact]
        public async Task Compression_Given_AProblem_Then_ItIsCompressedToo()
        {
            using var content = new MultipartFormDataContent();
            using HttpResponseMessage response = await SendAsync(
                HttpMethod.Post,
                @"/v1/projects/compile",
                headers => headers.TryAddWithoutValidation(@"Accept-Encoding", @"gzip"),
                content);

            // A body with no parts at all cannot be read as multipart/form-data.
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            response.Content.Headers.ContentEncoding.ShouldBe([@"gzip"]);
            response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe(@"application/problem+json");
            ProblemResponse problem = JsonSerializer.Deserialize<ProblemResponse>(await DecompressAsync(response, @"gzip"), ProjectEndpoints.JsonOptions).ShouldNotBeNull();
            problem.Type.ShouldBe(ProblemHelper.GetType(ProblemKind.MalformedRequest));
        }

        [Fact]
        public async Task Compression_Given_AZip_Then_ItIsNotCompressedAgain()
        {
            using var content = RunningServer.CompileContent(TwoScenarios(), @"two-scenarios.zpp");
            using HttpResponseMessage response = await SendAsync(
                HttpMethod.Post,
                @"/v1/projects/compile",
                headers =>
                {
                    headers.TryAddWithoutValidation(@"Accept", @"application/zip");
                    headers.TryAddWithoutValidation(@"Accept-Encoding", @"gzip, br");
                },
                content);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe(@"application/zip");
            response.Content.Headers.ContentEncoding.ShouldBeEmpty();
            using var zip = new ZipArchive(await response.Content.ReadAsStreamAsync(), ZipArchiveMode.Read);
            zip.Entries.Select(x => x.FullName).ShouldBe([@"result.json"]);
        }

        [Theory]
        [InlineData(@"/health/live")]
        [InlineData(@"/health/ready")]
        public async Task Health_Given_AProbe_Then_NoStoreAloneAndNothingTheHealthChecksAdd(string path)
        {
            using HttpResponseMessage response = await SendAsync(HttpMethod.Get, path);

            response.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue();
            response.Headers.CacheControl.NoCache.ShouldBeFalse();
            response.Headers.Contains(@"Pragma").ShouldBeFalse();
            response.Content.Headers.Contains(@"Expires").ShouldBeFalse();
            response.Headers.GetValues(@"X-Content-Type-Options").ShouldBe([@"nosniff"]);
            response.Headers.GetValues(ResponseHeadersMiddleware.RequestIdHeader).ShouldHaveSingleItem().ShouldMatch(@"^[0-9a-f]{32}$");
        }

        [Fact]
        public async Task Every_Given_AnythingButInfo_Then_NoStore()
        {
            using HttpResponseMessage notFound = await SendAsync(HttpMethod.Get, @"/nothing");
            using HttpResponseMessage options = await SendAsync(HttpMethod.Options, @"/v1/projects/compile");
            using HttpResponseMessage problem = await SendAsync(HttpMethod.Post, @"/v1/projects/compile", content: new MultipartFormDataContent());

            foreach (HttpResponseMessage response in new[] { notFound, options, problem })
            {
                response.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue();
            }
        }

        [Fact]
        public async Task Metrics_Given_ARequest_Then_ItsDurationIsRecordedUnderTheRouteTemplateAndNotThePath()
        {
            // What a collector groups the requests by: the route's template, so that a path that varies does not make a series
            // of its own. The meter is the process's, so that the requests of other tests are in it too: one of them has the route.
            var measurements = new List<(string? Route, int? Status)>();
            using var listener = new MeterListener();
            listener.InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == @"Microsoft.AspNetCore.Hosting"
                    && instrument.Name == @"http.server.request.duration")
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            };
            listener.SetMeasurementEventCallback<double>((_, _, tags, _) =>
            {
                string? route = null;
                int? status = null;

                foreach (KeyValuePair<string, object?> tag in tags)
                {
                    if (tag.Key == @"http.route")
                    {
                        route = tag.Value as string;
                    }
                    else if (tag.Key == @"http.response.status_code")
                    {
                        status = tag.Value as int?;
                    }
                }

                lock (measurements)
                {
                    measurements.Add((route, status));
                }
            });
            listener.Start();

            using HttpResponseMessage response = await SendAsync(HttpMethod.Get, @"/v1/info");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            // It is recorded when the request is done, which is a moment after its response is.
            for (int attempt = 0; attempt < 100; attempt++)
            {
                lock (measurements)
                {
                    if (measurements.Contains((@"/v1/info", 200)))
                    {
                        return;
                    }
                }

                await Task.Delay(50);
            }

            lock (measurements)
            {
                measurements.ShouldContain((@"/v1/info", 200));
            }
        }

        [Fact]
        public void CreateHttpsOptions_Given_ACertificate_Then_ServedWithItOverTheProtocolsItTakes()
        {
            string directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $@"zpp-tls-{Guid.NewGuid():N}")).FullName;

            try
            {
                using X509Certificate2 certificate = TestCertificates.CreateSelfSigned(directory);

                var options = JobServer.CreateHttpsOptions(certificate);

                options.ServerCertificate.ShouldBeSameAs(certificate);
                options.SslProtocols.ShouldBe(JobServer.TlsProtocols);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void Tls_Given_TheProtocolsItTakes_Then_Tls12AndTls13AndNothingOlder()
        {
            JobServer.TlsProtocols.ShouldBe(SslProtocols.Tls12 | SslProtocols.Tls13);

            // Named here to say that they are not taken.
#pragma warning disable SYSLIB0039 // TLS 1.0 and 1.1 are obsolete.
            JobServer.TlsProtocols.HasFlag(SslProtocols.Tls11).ShouldBeFalse();
            JobServer.TlsProtocols.HasFlag(SslProtocols.Tls).ShouldBeFalse();
#pragma warning restore SYSLIB0039
        }
    }
}
