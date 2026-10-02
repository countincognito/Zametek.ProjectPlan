using Serilog;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Zametek.Common.ProjectPlan;
using Zametek.Engine.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    // zpp --server: zpp, with its run sent as a job to a zpp serve rather than run in this process. It opens the plan
    // and checks the outputs' names as zpp does, so that what zpp would refuse fails as it would, before anything is
    // sent; sends the plan, with zpp's options less the paths; and plays the answer's transcript back through zpp's own
    // console and file sink, so that the run prints and writes what zpp would have, in the order zpp would have, and
    // ends with the exit code zpp would have. A server that does not run the job - it cannot be reached, refuses the
    // request, stays busy, or stops the job at its time limit - fails the run with a ServerException. It never builds
    // the engine.
    internal static class JobClient
    {
        #region Fields

        // How long zpp keeps trying a server that is busy.
        public static readonly TimeSpan BusyLimit = TimeSpan.FromMinutes(2);

        private const string c_JobsPath = @"v1/jobs";
        private const string c_ScenariosPath = @"v1/scenarios";
        private const string c_InputPart = @"input";
        private const string c_ImportPart = @"import";
        private const string c_OptionsPart = @"options";
        private const string c_Scheme = @"Bearer";
        private const string c_DetailProperty = @"detail";

        // The name a plan whose own gives it no title is sent under.
        private const string c_StandInPlanName = @"plan";

        // A plan larger than this is sent only once the server has said it will take it, as curl sends one, so that a
        // server that will not - it is too large - says so cleanly rather than closing the connection part way.
        private const int c_ExpectContinueBytes = 1024 * 1024;

        // How long zpp waits for a busy server that does not say.
        private static readonly TimeSpan s_DefaultRetryAfter = TimeSpan.FromSeconds(5);

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

            Log.Information("Running {Plan} on {Server}", planName, settings.Server);

            if (options.ListScenarios)
            {
                ScenariosResponse scenarios = await PostAsync<ScenariosResponse>(
                    client,
                    settings,
                    c_ScenariosPath,
                    () => CreateContent(plan, sentName, c_InputPart, null),
                    limit,
                    cancellationToken);

                var noOutputs = new Dictionary<JobOutput, string>();

                return !JobTranscriptHelper.IsPlayable(scenarios.Transcript, [], noOutputs)
                    || scenarios.ExitCode is not ((int)ExitCode.Success or (int)ExitCode.Failure)
                    ? throw NotReadable(settings)
                    : await JobTranscriptHelper.PlayAsync(scenarios.Transcript, [], (ExitCode)scenarios.ExitCode, console, new FileJobSink(noOutputs, console));
            }

            // Checked as zpp checks it, before anything is sent, so that a bad name fails the run before any file has
            // been written.
            if (options.ExportFilename is not null)
            {
                FileFormatHelper.GetProjectScenarioExportFormat(options.ExportFilename);
            }

            // Where zpp writes each output: named here, as zpp names it, whatever the server calls it.
            IReadOnlyDictionary<JobOutput, string> filenames = Program.BuildOutputFilenames(options, FileFormatHelper.GetProjectTitle(inputFilename));
            JobOptions jobOptions = JobOptionsHelper.FromOptions(options);

            JobResponse response = await PostAsync<JobResponse>(
                client,
                settings,
                c_JobsPath,
                () => CreateContent(plan, sentName, importFormat is null ? c_InputPart : c_ImportPart, jobOptions),
                limit,
                cancellationToken);

            Log.Information("Job {JobId} ran on {Server}: exit code {ExitCode}", response.JobId, settings.Server, response.ExitCode);

            return !JobTranscriptHelper.IsPlayable(response.Transcript, response.Outputs, filenames)
                || response.ExitCode is not ((int)ExitCode.Success or (int)ExitCode.Failure or (int)ExitCode.CompilationErrors or (int)ExitCode.CompilationTimeout)
                ? throw NotReadable(settings)
                : await JobTranscriptHelper.PlayAsync(response.Transcript, response.Outputs, (ExitCode)response.ExitCode, console, new FileJobSink(filenames, console));
        }

        #endregion

        #region Private Members

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

        // The plan as the named part - input or import - under its own name, and the job's options, if any, as JSON.
        private static MultipartFormDataContent CreateContent(
            byte[] plan,
            string planName,
            string part,
            JobOptions? options)
        {
            var content = new MultipartFormDataContent();
            var file = new ByteArrayContent(plan);
            file.Headers.ContentType = new MediaTypeHeaderValue(@"application/octet-stream");
            content.Add(file, part, planName);

            if (options is not null)
            {
                content.Add(new StringContent(JsonSerializer.Serialize(options, JobJsonHelper.ClientOptions)), c_OptionsPart);
            }

            return content;
        }

        // Sends the request, and again while the server is busy, for as long as busyLimit allows, and reads the answer.
        private static async Task<T> PostAsync<T>(
            HttpClient client,
            ClientSettings settings,
            string path,
            Func<HttpContent> createContent,
            TimeSpan busyLimit,
            CancellationToken cancellationToken)
        {
            var waiting = Stopwatch.StartNew();

            while (true)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = createContent() };

                if (request.Content.Headers.ContentLength > c_ExpectContinueBytes)
                {
                    request.Headers.ExpectContinue = true;
                }

                if (settings.ApiKey is string apiKey)
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue(c_Scheme, apiKey);
                }

                using HttpResponseMessage response = await SendAsync(client, request, settings, cancellationToken);

                if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    TimeSpan retryAfter = GetRetryAfter(response);

                    if (waiting.Elapsed + retryAfter > busyLimit)
                    {
                        throw new ServerException(string.Format(Resource.ProjectPlan.Messages.Message_ServerStayedBusy, settings.Server, (int)busyLimit.TotalSeconds));
                    }

                    Log.Warning("The server at {Server} is busy: trying again in {Seconds} s", settings.Server, retryAfter.TotalSeconds);
                    await Task.Delay(retryAfter, cancellationToken);
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

                if (!response.IsSuccessStatusCode)
                {
                    throw new ServerException(string.Format(
                        Resource.ProjectPlan.Messages.Message_ServerRefused,
                        settings.Server,
                        await GetReasonAsync(response, cancellationToken)));
                }

                try
                {
                    return await response.Content.ReadFromJsonAsync<T>(JobJsonHelper.ClientOptions, cancellationToken)
                        ?? throw NotReadable(settings);
                }
                catch (JsonException ex)
                {
                    throw new ServerException(string.Format(Resource.ProjectPlan.Messages.Message_ServerAnswerNotValid, settings.Server, ex.Message));
                }
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

        // How long a busy server says to wait before trying again.
        private static TimeSpan GetRetryAfter(HttpResponseMessage response)
        {
            RetryConditionHeaderValue? retryAfter = response.Headers.RetryAfter;
            TimeSpan? wait = retryAfter?.Delta
                ?? (retryAfter?.Date is DateTimeOffset date ? date - TimeProvider.System.GetUtcNow() : null);

            return wait is TimeSpan given && given >= TimeSpan.Zero
                ? given
                : s_DefaultRetryAfter;
        }

        // Why a server did not run the job: what its problem details say, or else its status.
        private static async Task<string> GetReasonAsync(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
        {
            try
            {
                using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

                if (problem.RootElement.ValueKind == JsonValueKind.Object
                    && problem.RootElement.TryGetProperty(c_DetailProperty, out JsonElement detail)
                    && detail.ValueKind == JsonValueKind.String
                    && detail.GetString() is string text
                    && text.Length > 0)
                {
                    return text;
                }
            }
            catch (JsonException)
            {
                // Not problem details - from a proxy in front of the server, say - so the status says why.
            }

            return $@"{(int)response.StatusCode} {response.ReasonPhrase}";
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
