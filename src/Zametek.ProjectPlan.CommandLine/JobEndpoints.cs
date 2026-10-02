using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zametek.Common.ProjectPlan;
using Zametek.Engine.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    // zpp serve's API. A job - POST /v1/jobs - comes as multipart/form-data: the plan as a file named input, as zpp's
    // --input takes it, or named import, as its --import does, with the job's options as a part named options (see
    // JobOptions). It is answered with what zpp would have exited with and printed, and the outputs the job produced (see
    // JobResponse): as JSON, or - when the request accepts application/zip - as a zip of the outputs, with the rest in
    // result.json. POST /v1/scenarios lists a project's scenarios, as --list-scenarios does, and GET /v1/info says what
    // the server is. A request zpp would refuse as a usage error is refused with 400, and anything else the server will
    // not run, with the status that says why.
    internal class JobEndpoints
    {
        #region Fields

        // The header that carries the id the server gives a job, which its log names the job by.
        public const string JobIdHeader = @"Zpp-Job-Id";

        private const string c_InputPart = @"input";
        private const string c_ImportPart = @"import";
        private const string c_OptionsPart = @"options";
        private const string c_ZipMediaType = @"application/zip";
        private const string c_ResultFilename = @"result.json";
        private const string c_JobIdProperty = @"JobId";

        // The earliest and latest times a zip can stamp on its files.
        private static readonly DateTime s_EarliestZipTime = new(1980, 1, 1);
        private static readonly DateTime s_LatestZipTime = new(2107, 12, 31, 23, 59, 58);

        private readonly JobRunner m_JobRunner;
        private readonly ServeLimits m_Limits;
        private readonly TimeProvider m_TimeProvider;
        private readonly ILogger<JobEndpoints> m_Logger;

        #endregion

        #region Ctors

        public JobEndpoints(
            JobRunner jobRunner,
            ServeLimits limits,
            TimeProvider timeProvider,
            ILogger<JobEndpoints> logger)
        {
            ArgumentNullException.ThrowIfNull(jobRunner);
            ArgumentNullException.ThrowIfNull(limits);
            ArgumentNullException.ThrowIfNull(timeProvider);
            ArgumentNullException.ThrowIfNull(logger);
            m_JobRunner = jobRunner;
            m_Limits = limits;
            m_TimeProvider = timeProvider;
            m_Logger = logger;
        }

        #endregion

        #region Properties

        // How requests are read and responses written: camelCase, enums by name - in any case, never by number - and a
        // job's options strictly, so that an option misspelt, or a number in quotes, is refused rather than ignored.
        public static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions();

        #endregion

        #region Public Members

        public async Task RunJobAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            string jobId = StartJob(context);
            using IDisposable? scope = m_Logger.BeginScope(new Dictionary<string, object> { [c_JobIdProperty] = jobId });

            IResult result = await RunJobAsync(context, jobId);
            await result.ExecuteAsync(context);
        }

        public async Task ListScenariosAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            string jobId = StartJob(context);
            using IDisposable? scope = m_Logger.BeginScope(new Dictionary<string, object> { [c_JobIdProperty] = jobId });

            IResult result = await ListScenariosAsync(context, jobId);
            await result.ExecuteAsync(context);
        }

        public async Task GetInfoAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var info = new InfoResponse(
                Resource.ProjectPlan.Labels.Label_AppVersion,
                CultureInfo.CurrentCulture.Name,
                TimeZoneInfo.Local.Id,
                m_Limits);

            await Results.Json(info, JsonOptions).ExecuteAsync(context);
        }

        #endregion

        #region Private Members

        private async Task<IResult> RunJobAsync(
            HttpContext context,
            string jobId)
        {
            (IFormCollection? form, IResult? refusal) = await ReadFormAsync(context, jobId);
            if (form is null)
            {
                return refusal!;
            }

            IFormFile? input = form.Files.GetFile(c_InputPart);
            IFormFile? import = form.Files.GetFile(c_ImportPart);

            if ((input is null) == (import is null))
            {
                return Refuse(context, jobId, StatusCodes.Status400BadRequest, Resource.ProjectPlan.Messages.Message_ServeRequestNeedsPlan);
            }

            IFormFile plan = input ?? import!;

            // Only the name: a path that came with it says nothing about where the server is.
            string filename = Path.GetFileName(plan.FileName);
            string projectTitle = SettingServiceBase.GetProjectTitle(filename);

            if (projectTitle.Length == 0)
            {
                return Refuse(context, jobId, StatusCodes.Status400BadRequest, Resource.ProjectPlan.Messages.Message_ServePlanNeedsName);
            }

            ProjectScenarioImportFormat? importFormat = null;

            if (import is not null)
            {
                // Workbooks only: zpp serve does not import MS Project's files, which need the Java runtime that MPXJ
                // brings with it.
                if (!string.Equals(Path.GetExtension(filename).TrimStart('.'), Resource.ProjectPlan.Filters.Filter_ProjectXlsxFileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    return Refuse(context, jobId, StatusCodes.Status415UnsupportedMediaType, string.Format(Resource.ProjectPlan.Messages.Message_ServeCannotImport, filename));
                }

                importFormat = ProjectScenarioImportFormat.Xlsx;
            }

            (JobOptions? options, string? unreadable) = await ReadOptionsAsync(form);
            if (options is null)
            {
                return Refuse(context, jobId, StatusCodes.Status400BadRequest, unreadable!);
            }

            string? invalid = JobOptionsHelper.Validate(options, import is not null, m_Limits);
            if (invalid is not null)
            {
                return Refuse(context, jobId, StatusCodes.Status400BadRequest, invalid);
            }

            await using Stream stream = plan.OpenReadStream();
            JobRequest request = JobOptionsHelper.ToJobRequest(options, stream, importFormat, m_Limits);

            // The time the job's files are stamped with in a zip: the time it runs at.
            DateTimeOffset runsAt = request.Now ?? m_TimeProvider.GetLocalNow();

            var console = new BufferedConsole();
            var sink = new MemoryJobSink(console);
            var stopwatch = Stopwatch.StartNew();
            JsonElement? metrics = null;
            ExitCode exitCode;

            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(m_Limits.JobTimeoutSeconds)))
            using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, timeout.Token))
            {
                try
                {
                    JobResult result = await m_JobRunner.RunAsync(request, sink, cancellation.Token);
                    exitCode = await JobConsoleHelper.WriteResultAsync(console, result, options.MetricsFormat);
                    metrics = ToJson(result.Metrics);
                }
                catch (OperationCanceledException) when (HasTimedOut(context, timeout))
                {
                    return TimedOut(context, jobId);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !cancellation.IsCancellationRequested)
                {
                    // Whatever zpp would have printed and exited with. A job the caller gave up on, nobody hears about.
                    exitCode = await JobConsoleHelper.WriteFailureAsync(console, ex);
                }
            }

            m_Logger.LogInformation("Job {JobId} ({Path}): exit code {ExitCode} after {ElapsedMilliseconds} ms", jobId, context.Request.Path, (int)exitCode, stopwatch.ElapsedMilliseconds);

            List<JobResponseOutput> outputs = [.. sink.Outputs.Select(x => ToResponseOutput(x.Output, x.Content, options, projectTitle))];
            var response = new JobResponse(jobId, (int)exitCode, console.Output, console.Error, metrics, outputs);

            return AcceptsZip(context.Request)
                ? Zip(response, projectTitle, runsAt)
                : Results.Json(response, JsonOptions);
        }

        private async Task<IResult> ListScenariosAsync(
            HttpContext context,
            string jobId)
        {
            (IFormCollection? form, IResult? refusal) = await ReadFormAsync(context, jobId);
            if (form is null)
            {
                return refusal!;
            }

            // A project's, as --list-scenarios is only valid with --input.
            IFormFile? input = form.Files.GetFile(c_InputPart);

            if (input is null
                || form.Files.GetFile(c_ImportPart) is not null)
            {
                return Refuse(context, jobId, StatusCodes.Status400BadRequest, Resource.ProjectPlan.Messages.Message_ServeScenariosNeedInput);
            }

            await using Stream stream = input.OpenReadStream();

            var console = new BufferedConsole();
            var stopwatch = Stopwatch.StartNew();
            IReadOnlyList<ScenarioSummary>? scenarios = null;
            ExitCode exitCode;

            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(m_Limits.JobTimeoutSeconds)))
            using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, timeout.Token))
            {
                try
                {
                    scenarios = await m_JobRunner.ListScenariosAsync(stream, cancellation.Token);
                    await JobConsoleHelper.WriteScenariosAsync(console, scenarios);
                    exitCode = ExitCode.Success;
                }
                catch (OperationCanceledException) when (HasTimedOut(context, timeout))
                {
                    return TimedOut(context, jobId);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !cancellation.IsCancellationRequested)
                {
                    exitCode = await JobConsoleHelper.WriteFailureAsync(console, ex);
                }
            }

            m_Logger.LogInformation("Job {JobId} ({Path}): exit code {ExitCode} after {ElapsedMilliseconds} ms", jobId, context.Request.Path, (int)exitCode, stopwatch.ElapsedMilliseconds);

            return Results.Json(new ScenariosResponse(jobId, (int)exitCode, console.Output, console.Error, scenarios), JsonOptions);
        }

        // Gives the request a job's id, and says it in the response.
        private static string StartJob(HttpContext context)
        {
            string jobId = Guid.NewGuid().ToString(@"N");
            context.Response.Headers[JobIdHeader] = jobId;
            return jobId;
        }

        // The request's parts, or why they cannot be read.
        private async Task<(IFormCollection? Form, IResult? Refusal)> ReadFormAsync(
            HttpContext context,
            string jobId)
        {
            if (!context.Request.HasFormContentType)
            {
                return (null, Refuse(context, jobId, StatusCodes.Status415UnsupportedMediaType, Resource.ProjectPlan.Messages.Message_ServeRequestNotMultipart));
            }

            try
            {
                return (await context.Request.ReadFormAsync(context.RequestAborted), null);
            }
            catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
            {
                return (null, Refuse(context, jobId, StatusCodes.Status413PayloadTooLarge, string.Format(Resource.ProjectPlan.Messages.Message_ServeRequestTooLarge, m_Limits.MaxUploadMegabytes)));
            }
            catch (Exception ex) when (ex is BadHttpRequestException or InvalidDataException)
            {
                return (null, Refuse(context, jobId, StatusCodes.Status400BadRequest, string.Format(Resource.ProjectPlan.Messages.Message_ServeRequestNotReadable, ex.Message)));
            }
        }

        // The job's options - zpp's defaults when there are none - or why they are not valid.
        private static async Task<(JobOptions? Options, string? Invalid)> ReadOptionsAsync(IFormCollection form)
        {
            string? json;

            if (form.Files.GetFile(c_OptionsPart) is IFormFile file)
            {
                using var reader = new StreamReader(file.OpenReadStream());
                json = await reader.ReadToEndAsync();
            }
            else
            {
                json = form.TryGetValue(c_OptionsPart, out StringValues value) ? value.ToString() : null;
            }

            if (json is null)
            {
                return (new JobOptions(), null);
            }

            try
            {
                return (JsonSerializer.Deserialize<JobOptions>(json, JsonOptions) ?? new JobOptions(), null);
            }
            catch (JsonException ex)
            {
                return (null, string.Format(Resource.ProjectPlan.Messages.Message_ServeOptionsNotValid, ex.Message));
            }
        }

        private IResult Refuse(
            HttpContext context,
            string jobId,
            int statusCode,
            string detail)
        {
            m_Logger.LogInformation("Job {JobId} ({Path}): refused with {StatusCode}: {Detail}", jobId, context.Request.Path, statusCode, detail);
            return Results.Problem(detail: detail, statusCode: statusCode);
        }

        // Whether the job stopped because it ran out of time, rather than because the caller went away.
        private static bool HasTimedOut(
            HttpContext context,
            CancellationTokenSource timeout)
        {
            return timeout.IsCancellationRequested
                && !context.RequestAborted.IsCancellationRequested;
        }

        private IResult TimedOut(
            HttpContext context,
            string jobId)
        {
            m_Logger.LogWarning("Job {JobId} ({Path}): stopped after running for {JobTimeoutSeconds} s", jobId, context.Request.Path, m_Limits.JobTimeoutSeconds);

            return Results.Problem(
                detail: string.Format(Resource.ProjectPlan.Messages.Message_ServeJobTimedOut, m_Limits.JobTimeoutSeconds),
                statusCode: StatusCodes.Status504GatewayTimeout);
        }

        // The metrics as zpp writes them with --metrics-format json.
        private static JsonElement? ToJson(JobMetrics? metrics)
        {
            if (metrics is null)
            {
                return null;
            }

            using JsonDocument document = JsonDocument.Parse(JobConsoleHelper.BuildMetricsJson(metrics));
            return document.RootElement.Clone();
        }

        private static JobResponseOutput ToResponseOutput(
            JobOutput output,
            byte[] content,
            JobOptions options,
            string projectTitle)
        {
            string filename = JobOptionsHelper.BuildOutputFilename(output, options, projectTitle);
            return new JobResponseOutput(output, filename, JobOptionsHelper.GetContentType(filename), content);
        }

        private static bool AcceptsZip(HttpRequest request)
        {
            return request.GetTypedHeaders().Accept
                .Any(x => x.MediaType.Equals(c_ZipMediaType, StringComparison.OrdinalIgnoreCase)
                    && (x.Quality ?? 1) > 0);
        }

        // The outputs, each in the file zpp would have written, with the rest of the response in result.json. Every
        // file is stamped with the time the job ran at - the time now gave it, if it was given one - rather than the
        // time the zip was made.
        private static IResult Zip(
            JobResponse response,
            string projectTitle,
            DateTimeOffset runsAt)
        {
            DateTimeOffset lastWriteTime = runsAt.DateTime < s_EarliestZipTime
                ? new DateTimeOffset(s_EarliestZipTime, runsAt.Offset)
                : runsAt.DateTime > s_LatestZipTime ? new DateTimeOffset(s_LatestZipTime, runsAt.Offset) : runsAt;

            using var stream = new MemoryStream();

            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (JobResponseOutput output in response.Outputs)
                {
                    AddZipEntry(zip, output.FileName, lastWriteTime, output.Content ?? []);
                }

                JobResponse result = response with { Outputs = [.. response.Outputs.Select(x => x with { Content = null })] };
                AddZipEntry(zip, c_ResultFilename, lastWriteTime, JsonSerializer.SerializeToUtf8Bytes(result, JsonOptions));
            }

            return Results.File(stream.ToArray(), c_ZipMediaType, $@"{projectTitle}.zip");
        }

        private static void AddZipEntry(
            ZipArchive zip,
            string name,
            DateTimeOffset lastWriteTime,
            byte[] content)
        {
            ZipArchiveEntry entry = zip.CreateEntry(name);
            entry.LastWriteTime = lastWriteTime;

            using Stream entryStream = entry.Open();
            entryStream.Write(content);
        }

        private static JsonSerializerOptions CreateJsonOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,

                // The text zpp prints as it prints it - quotes and all - as the problem details write theirs. A response
                // is JSON for its caller, never HTML, so nothing needs escaping that JSON does not escape.
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };

            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
            return options;
        }

        #endregion
    }
}
