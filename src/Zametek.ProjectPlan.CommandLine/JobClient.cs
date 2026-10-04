using Serilog;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Zametek.Common.ProjectPlan;
using Zametek.Engine.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    // zpp --server: zpp, with its run sent as a request to a zpp serve rather than run in this process. It opens the plan
    // and checks the outputs' names as zpp does, so that what zpp would refuse fails as it would, before anything is
    // sent; sends the plan, with zpp's options less the paths, asking for the console; and plays the console's transcript
    // back through zpp's own console and file sink, so that the run prints and writes what zpp would have, in the order zpp
    // would have, and ends with the exit code zpp would have - whether the server answered, or answered with a problem that
    // says why the job failed: a plan that does not compile, a file it cannot read, a scenario it cannot select. A server
    // that does not run the job - it cannot be reached, refuses the request, stays busy, or stops the job at its time limit
    // - fails the run with a ServerException. It never builds the engine. Like any client of the API it sends a
    // traceparent, tells a problem by its type, and tries a busy server again as it says to, with a little more patience
    // each time.
    internal static class JobClient
    {
        #region Fields

        // How long zpp keeps trying a server that is busy.
        public static readonly TimeSpan BusyLimit = TimeSpan.FromMinutes(2);

        private const string c_CompilePath = @"v1/projects/compile?include=console";
        private const string c_ScenariosPath = @"v1/projects/scenarios?include=console";
        private const string c_ProjectPart = @"project";
        private const string c_ImportPart = @"import";
        private const string c_OptionsPart = @"options";
        private const string c_Scheme = @"Bearer";
        private const string c_TraceParentHeader = @"traceparent";
        private const string c_JsonMediaType = @"application/json";

        // The name a plan whose own gives it no title is sent under.
        private const string c_StandInPlanName = @"plan";

        // A plan larger than this is sent only once the server has said it will take it, as curl sends one, so that a
        // server that will not - it is too large - says so cleanly rather than closing the connection part way.
        private const int c_ExpectContinueBytes = 1024 * 1024;

        // How long zpp waits for a busy server that does not say.
        private static readonly TimeSpan s_DefaultRetryAfter = TimeSpan.FromSeconds(5);

        // The shortest zpp waits between two tries, whatever the server says.
        private static readonly TimeSpan s_ShortestWait = TimeSpan.FromSeconds(1);

        // The longest zpp waits between two tries, however many there have been, unless the server asks for longer.
        private static readonly TimeSpan s_LongestBackOff = TimeSpan.FromSeconds(30);

        // How long zpp tries to connect to a server. Once it has, the server's own limits bound the job.
        private static readonly TimeSpan s_ConnectTimeout = TimeSpan.FromSeconds(30);

        #endregion

        #region Public Members

        // Runs the job on the server the settings name, and returns the exit code zpp ends the run with. busyLimit is how
        // long a busy server is waited for.
        public static async Task<ExitCode> RunAsync(
            Options options,
            ClientSettings settings,
            IJobConsole console,
            TimeSpan? busyLimit = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(console);

            TimeSpan limit = busyLimit ?? BusyLimit;

            // The plan, opened as zpp opens it.
            string inputFilename = options.InputFilename ?? options.ImportFilename ?? throw new InvalidOperationException();
            ProjectScenarioImportFormat? importFormat = options.ImportFilename is null
                ? null
                : FileFormatHelper.GetProjectScenarioImportFormat(inputFilename);

            byte[] plan;
            await using (FileStream input = importFormat is ProjectScenarioImportFormat format
                ? FileStreamHelper.OpenImportFile(inputFilename, format)
                : File.OpenRead(inputFilename))
            {
                using var buffer = new MemoryStream();
                await input.CopyToAsync(buffer, cancellationToken);
                plan = buffer.ToArray();
            }

            // The server names the outputs after the plan, as zpp does, so it is sent under its own name - or, if that
            // gives the project no title, which the server needs in order to name them, under a stand-in that does: zpp
            // names the files itself, whatever the server calls them.
            string planName = Path.GetFileName(inputFilename);
            string sentName = FileFormatHelper.GetProjectTitle(planName).Length > 0
                ? planName
                : c_StandInPlanName + Path.GetExtension(planName);
            using HttpClient client = CreateClient(settings);

            // One trace for the run, however many requests it takes.
            string traceId = ActivityTraceId.CreateRandom().ToHexString();

            Log.Information("Running {Plan} on {Server}", planName, settings.Server);

            if (options.ListScenarios)
            {
                ServerAnswer<ScenariosResponse> answer = await PostAsync<ScenariosResponse>(
                    client,
                    settings,
                    traceId,
                    c_ScenariosPath,
                    () => CreateContent(plan, sentName, c_ProjectPart, null),
                    limit,
                    cancellationToken);

                var noOutputs = new Dictionary<JobOutput, string>();

                return answer.Problem is not null
                    ? await ReplayProblemAsync(settings, answer, [], noOutputs, console)
                    : await ReplayAsync(settings, answer.Value!.Console, [], noOutputs, console, ExitCode.Success, answer.RequestId);
            }

            // Checked as zpp checks it, before anything is sent, so that a bad name fails the run before any file has
            // been written.
            if (options.ExportFilename is not null)
            {
                FileFormatHelper.GetProjectScenarioExportFormat(options.ExportFilename);
            }

            // Where zpp writes each output: named here, as zpp names it, whatever the server calls it.
            IReadOnlyDictionary<JobOutput, string> filenames = Program.BuildOutputFilenames(options, FileFormatHelper.GetProjectTitle(inputFilename));
            CompileOptions compileOptions = CompileOptionsHelper.FromOptions(options);

            ServerAnswer<CompileResponse> compiled = await PostAsync<CompileResponse>(
                client,
                settings,
                traceId,
                c_CompilePath,
                () => CreateContent(plan, sentName, importFormat is null ? c_ProjectPart : c_ImportPart, compileOptions),
                limit,
                cancellationToken);

            return compiled.Problem is not null
                ? await ReplayProblemAsync(settings, compiled, compiled.Problem.Outputs ?? [], filenames, console)
                : await ReplayAsync(settings, compiled.Value!.Console, compiled.Value.Outputs, filenames, console, ExitCode.Success, compiled.RequestId);
        }

        #endregion

        #region Private Members

        // What a server answered a request with: the answer, or the problem it was not answered with; and the request's id.
        private sealed record ServerAnswer<T>(
            T? Value,
            ProblemResponse? Problem,
            string RequestId);

        private static HttpClient CreateClient(ClientSettings settings)
        {
            var handler = new SocketsHttpHandler { ConnectTimeout = s_ConnectTimeout };

            if (settings.UnixSocket is string socket)
            {
                // A socket is connected to directly, never through a proxy the environment names for the web.
                handler.UseProxy = false;
                handler.ConnectCallback = async (_, cancellationToken) =>
                {
                    var connection = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

                    try
                    {
                        await connection.ConnectAsync(new UnixDomainSocketEndPoint(socket), cancellationToken);
                        return new NetworkStream(connection, ownsSocket: true);
                    }
                    catch (SocketException) when (!File.Exists(socket))
                    {
                        connection.Dispose();

                        // A socket that is not there - its server not started, say - in words that say so, as the
                        // system's do not: "Cannot assign requested address" on Linux, "No connection could be made
                        // because the target machine actively refused it" on Windows.
                        throw new IOException(string.Format(Resource.ProjectPlan.Messages.Message_ServerSocketNotThere, socket));
                    }
                    catch
                    {
                        connection.Dispose();
                        throw;
                    }
                };
            }

            // The server's own limits bound how long a job may take.
            return new HttpClient(handler)
            {
                BaseAddress = settings.BaseAddress,
                Timeout = Timeout.InfiniteTimeSpan,
            };
        }

        // The plan as the named part - project or import - under its own name, and the request's options, if any, as
        // JSON, declared as such.
        private static MultipartFormDataContent CreateContent(
            byte[] plan,
            string planName,
            string part,
            CompileOptions? options)
        {
            var content = new MultipartFormDataContent();
            var file = new ByteArrayContent(plan);
            file.Headers.ContentType = new MediaTypeHeaderValue(@"application/octet-stream");
            content.Add(file, part, planName);

            if (options is not null)
            {
                content.Add(new StringContent(JsonSerializer.Serialize(options, JobJsonHelper.ClientOptions), Encoding.UTF8, c_JsonMediaType), c_OptionsPart);
            }

            return content;
        }

        // Sends the request, and again while the server is busy, for as long as busyLimit allows - waiting what the server says
        // and a little more each time, with a little at random, as a client that every other waits with does not all come
        // back at once - and reads the answer, or the problem that is the answer.
        private static async Task<ServerAnswer<T>> PostAsync<T>(
            HttpClient client,
            ClientSettings settings,
            string traceId,
            string path,
            Func<HttpContent> createContent,
            TimeSpan busyLimit,
            CancellationToken cancellationToken)
        {
            var waiting = Stopwatch.StartNew();
            int tries = 0;

            while (true)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = createContent() };
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(c_JsonMediaType));
                request.Headers.Add(c_TraceParentHeader, $@"00-{traceId}-{ActivitySpanId.CreateRandom().ToHexString()}-00");

                if (request.Content.Headers.ContentLength > c_ExpectContinueBytes)
                {
                    request.Headers.ExpectContinue = true;
                }

                if (settings.ApiKey is string apiKey)
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue(c_Scheme, apiKey);
                }

                using HttpResponseMessage response = await SendAsync(client, request, settings, cancellationToken);
                string requestId = response.Headers.TryGetValues(ResponseHeadersMiddleware.RequestIdHeader, out IEnumerable<string>? ids)
                    ? ids.FirstOrDefault() ?? traceId
                    : traceId;

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        T value = await response.Content.ReadFromJsonAsync<T>(JobJsonHelper.ClientOptions, cancellationToken)
                            ?? throw NotReadable(settings);
                        return new ServerAnswer<T>(value, null, requestId);
                    }
                    catch (JsonException ex)
                    {
                        throw new ServerException(string.Format(Resource.ProjectPlan.Messages.Message_ServerAnswerNotValid, settings.Server, ex.Message));
                    }
                }

                ProblemResponse? problem = await ReadProblemAsync(response, cancellationToken);

                // Busy: the job did not run, and is the same job tried again. A job that ran out of time is a 503 as well,
                // which trying again would only run out of time again.
                if (response.StatusCode == HttpStatusCode.ServiceUnavailable
                    && !IsKind(problem, ProblemKind.JobTimeout))
                {
                    TimeSpan retryAfter = GetRetryAfter(response, tries);

                    if (waiting.Elapsed + retryAfter > busyLimit)
                    {
                        throw new ServerException(string.Format(Resource.ProjectPlan.Messages.Message_ServerStayedBusy, settings.Server, (int)busyLimit.TotalSeconds));
                    }

                    Log.Warning("The server at {Server} is busy: trying again in {Seconds} s", settings.Server, retryAfter.TotalSeconds);
                    await Task.Delay(retryAfter, cancellationToken);
                    tries++;
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new ServerException(settings.ApiKey is null
                        ? string.Format(
                            Resource.ProjectPlan.Messages.Message_ServerNeedsApiKey,
                            settings.Server,
                            ClientSettingsHelper.ApiKeyVariable,
                            Program.OptionLongName(nameof(Options.ApiKeyFile)))
                        : string.Format(Resource.ProjectPlan.Messages.Message_ServerRefusedApiKey, settings.Server));
                }

                // A job that ran and failed, and says so with the console: a project that does not compile, a file that cannot
                // be read, a scenario that cannot be selected, an output that could not be produced.
                if (problem?.Console is not null
                    && ProblemHelper.TryGetKind(problem.Type, out ProblemKind kind)
                    && GetExitCode(kind) is not null)
                {
                    return new ServerAnswer<T>(default, problem, requestId);
                }

                throw new ServerException(string.Format(
                    Resource.ProjectPlan.Messages.Message_ServerRefused,
                    settings.Server,
                    Describe(problem, response, requestId)));
            }
        }

        private static async Task<HttpResponseMessage> SendAsync(
            HttpClient client,
            HttpRequestMessage request,
            ClientSettings settings,
            CancellationToken cancellationToken)
        {
            try
            {
                return await client.SendAsync(request, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
            {
                // A server that could not be connected to, or that went away part way through - in the words of the
                // error at the root of it, which says why: a certificate this machine does not trust, say, where the
                // error that wraps it says only "see inner exception".
                throw new ServerException(string.Format(Resource.ProjectPlan.Messages.Message_ServerUnreachable, settings.Server, ex.GetBaseException().Message));
            }
        }

        // How long to wait before trying a busy server again: what it says - or zpp's own wait - and more each time that it
        // has to be said, up to a limit that it may exceed by saying more, and a little over, at random.
        private static TimeSpan GetRetryAfter(
            HttpResponseMessage response,
            int tries)
        {
            RetryConditionHeaderValue? retryAfter = response.Headers.RetryAfter;
            TimeSpan? said = retryAfter?.Delta
                ?? (retryAfter?.Date is DateTimeOffset date ? date - TimeProvider.System.GetUtcNow() : null);

            return GetBackOff(said, tries, Random.Shared.NextDouble());
        }

        // The wait before the try after this many: what the server says - or, if it says nothing that can be waited for,
        // zpp's own wait; and never less than a second, so that a server that says to come back at once is not asked again
        // and again in a tight loop - doubled for each try so far, up to 30 seconds, or what the server says if that is
        // more; and up to a quarter more, as the jitter, from 0 to 1, takes it.
        internal static TimeSpan GetBackOff(
            TimeSpan? said,
            int tries,
            double jitter)
        {
            TimeSpan given = said is TimeSpan wait && wait >= TimeSpan.Zero
                ? TimeSpan.FromTicks(Math.Max(wait.Ticks, s_ShortestWait.Ticks))
                : s_DefaultRetryAfter;

            TimeSpan ceiling = given > s_LongestBackOff ? given : s_LongestBackOff;

            // Doubled for up to ten tries, and without overflowing for a server that says a long time.
            long factor = 1L << Math.Min(tries, 10);
            TimeSpan backedOff = given.Ticks > ceiling.Ticks / factor ? ceiling : TimeSpan.FromTicks(given.Ticks * factor);

            return backedOff + TimeSpan.FromTicks((long)(backedOff.Ticks * 0.25 * jitter));
        }

        // The problem a response is, if it is one: problem details, which a proxy in front of the server, say, does not send.
        private static async Task<ProblemResponse?> ReadProblemAsync(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
        {
            try
            {
                return await response.Content.ReadFromJsonAsync<ProblemResponse>(JobJsonHelper.ClientOptions, cancellationToken);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
            {
                return null;
            }
        }

        private static bool IsKind(
            ProblemResponse? problem,
            ProblemKind kind)
        {
            return problem is not null
                && ProblemHelper.TryGetKind(problem.Type, out ProblemKind found)
                && found == kind;
        }

        // The exit code zpp ends a run with when the server says the job failed in this way, or null if the kind is not one
        // of a job that ran: a project that does not compile ends as it does here, a compilation out of time too, and a file
        // or a scenario that cannot be read or selected, an output that cannot be produced, or something unexpected, is a
        // failure.
        private static ExitCode? GetExitCode(ProblemKind kind)
        {
            return kind switch
            {
                ProblemKind.CompilationFailed => ExitCode.CompilationErrors,
                ProblemKind.CompilationTimedOut => ExitCode.CompilationTimeout,
                ProblemKind.ProjectNotReadable or ProblemKind.ScenarioNotSelectable or ProblemKind.OutputFailed or ProblemKind.UnexpectedError => ExitCode.Failure,
                _ => null,
            };
        }

        // Plays back the console of a job that ran to its end, and returns the exit code the run ends with.
        private static async Task<ExitCode> ReplayAsync(
            ClientSettings settings,
            ConsoleResponse? served,
            IReadOnlyList<OutputResponse> outputs,
            IReadOnlyDictionary<JobOutput, string> filenames,
            IJobConsole console,
            ExitCode expected,
            string requestId)
        {
            if (served is null
                || served.ExitCode != (int)expected
                || !JobTranscriptHelper.IsPlayable(served.Transcript, outputs, filenames))
            {
                throw NotReadable(settings);
            }

            Log.Information("Request {RequestId} ran on {Server}: exit code {ExitCode}", requestId, settings.Server, served.ExitCode);

            return await JobTranscriptHelper.PlayAsync(served.Transcript, outputs, expected, console, new FileJobSink(filenames, console));
        }

        // Plays back the console of a job that ran and failed, which the problem carries with the outputs - the others - that
        // an output that could not be produced was produced with, and returns the exit code the run ends with.
        private static async Task<ExitCode> ReplayProblemAsync<T>(
            ClientSettings settings,
            ServerAnswer<T> answer,
            IReadOnlyList<OutputResponse> outputs,
            IReadOnlyDictionary<JobOutput, string> filenames,
            IJobConsole console)
        {
            ProblemResponse problem = answer.Problem ?? throw new InvalidOperationException();

            ProblemHelper.TryGetKind(problem.Type, out ProblemKind kind);
            ExitCode expected = GetExitCode(kind) ?? throw new InvalidOperationException();

            return await ReplayAsync(settings, problem.Console, outputs, filenames, console, expected, answer.RequestId);
        }

        // Why a server did not run the job: what its problem details say - what is wrong, and where - or else its status;
        // and the request's id, which its log names the request by.
        private static string Describe(
            ProblemResponse? problem,
            HttpResponseMessage response,
            string requestId)
        {
            string reason = string.IsNullOrEmpty(problem?.Detail)
                ? $@"{(int)response.StatusCode} {response.ReasonPhrase}"
                : problem.Detail;

            // The errors read on from the sentence that says there are some, which has its full stop where they begin.
            if (problem?.Errors is { Count: > 0 } errors)
            {
                reason = $@"{reason.TrimEnd('.')}: {string.Join(@"; ", errors.Select(x => $@"{x.Pointer ?? x.Parameter} {x.Detail}"))}";
            }

            return string.Format(Resource.ProjectPlan.Messages.Message_ServerReasonWithRequestId, reason, requestId);
        }

        private static ServerException NotReadable(ClientSettings settings)
        {
            return new ServerException(string.Format(
                Resource.ProjectPlan.Messages.Message_ServerAnswerNotValid,
                settings.Server,
                Resource.ProjectPlan.Messages.Message_ServerAnswerDoesNotMatchJob));
        }

        #endregion
    }
}
