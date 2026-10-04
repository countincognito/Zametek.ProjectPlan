using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Zametek.Common.ProjectPlan;
using Zametek.Engine.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    // zpp serve's API. A request to compile a project - POST /v1/projects/compile - comes as multipart/form-data: the
    // project as a file named project, as zpp's --input takes it, or named import, as its --import does, with the options as
    // JSON in a part named options (see CompileOptions). It is answered with the project's metrics and the outputs it
    // produced (see CompileResponse): as JSON, or - when the request accepts application/zip - as a zip of the outputs, with
    // the rest in result.json. Asked to include the console, it also says what zpp would have printed and exited with.
    // POST /v1/projects/scenarios lists a project's scenarios, as --list-scenarios does, and GET /v1/info says what the
    // server is. What cannot be answered is a problem (see ProblemResponse), with the status that says why: a request that
    // cannot be understood is 400, one whose content is not valid, or whose project cannot be processed, is 422, and
    // whatever the server did not expect, or cannot do now, is 500 or 503.
    internal class ProjectEndpoints
    {
        #region Fields

        private const string c_ProjectPart = @"project";
        private const string c_ImportPart = @"import";
        private const string c_OptionsPart = @"options";
        private const string c_IncludeParameter = @"include";
        private const string c_ConsoleInclusion = @"console";
        private const string c_JsonMediaType = @"application/json";
        private const string c_ZipMediaType = @"application/zip";
        private const string c_AcceptHeader = @"Accept";
        private const string c_AcceptPostHeader = @"Accept-Post";
        private const string c_InfoCacheControl = @"private, max-age=60";
        private const string c_InfoContentType = @"application/json; charset=utf-8";
        private const string c_ResultFilename = @"result.json";

        // Where the parts of a request are, as a JSON pointer: the request, as OpenAPI models a multipart body, is an object
        // whose properties are its parts.
        private const string c_RequestPointer = @"#";

        // The earliest and latest times a zip can stamp on its files.
        private static readonly DateTime s_EarliestZipTime = new(1980, 1, 1);
        private static readonly DateTime s_LatestZipTime = new(2107, 12, 31, 23, 59, 58);

        private readonly JobRunner m_JobRunner;
        private readonly ServeLimits m_Limits;
        private readonly TimeProvider m_TimeProvider;
        private readonly ILogger<ProjectEndpoints> m_Logger;

        #endregion

        #region Ctors

        public ProjectEndpoints(
            JobRunner jobRunner,
            ServeLimits limits,
            TimeProvider timeProvider,
            ILogger<ProjectEndpoints> logger)
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

        // How requests are read and responses written (see JobJsonHelper).
        public static JsonSerializerOptions JsonOptions => JobJsonHelper.ServerOptions;

        #endregion

        #region Public Members

        public async Task CompileAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var stopwatch = Stopwatch.StartNew();
            Answer answer = await CompileAnswerAsync(context);
            Log(context, answer, stopwatch);
            await answer.Result.ExecuteAsync(context);
        }

        public async Task ListScenariosAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var stopwatch = Stopwatch.StartNew();
            Answer answer = await ListScenariosAnswerAsync(context);
            Log(context, answer, stopwatch);
            await answer.Result.ExecuteAsync(context);
        }

        // What the server is. It cannot change while it runs, so it has an ETag of its own, which a client that asks
        // again says it has - and is answered 304.
        public async Task GetInfoAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (AcceptHelper.Choose(context.Request, [c_JsonMediaType]) is null)
            {
                Answer refusal = NotAcceptable(context, [c_JsonMediaType]);
                Log(context, refusal, Stopwatch.StartNew());
                await refusal.Result.ExecuteAsync(context);
                return;
            }

            var info = new InfoResponse(
                Resource.ProjectPlan.Labels.Label_AppVersion,
                CultureInfo.CurrentCulture.Name,
                TimeZoneHelper.GetIanaId(TimeZoneInfo.Local),
                LimitsResponse.From(m_Limits));

            byte[] body = JsonSerializer.SerializeToUtf8Bytes(info, JsonOptions);
            var tag = new EntityTagHeaderValue($@"""{Convert.ToHexStringLower(SHA256.HashData(body))[..32]}""");

            IHeaderDictionary headers = context.Response.Headers;
            headers.ETag = tag.ToString();
            headers.CacheControl = c_InfoCacheControl;

            if (context.Request.GetTypedHeaders().IfNoneMatch.Any(x => x.Equals(EntityTagHeaderValue.Any) || x.Compare(tag, useStrongComparison: false)))
            {
                context.Response.StatusCode = StatusCodes.Status304NotModified;
                return;
            }

            context.Response.ContentType = c_InfoContentType;
            context.Response.ContentLength = body.Length;
            await context.Response.Body.WriteAsync(body, context.RequestAborted);
        }

        // What an endpoint takes, as OPTIONS says it: no content, the methods it answers, and - for a POST - the media type
        // of what it takes.
        public static Task GetOptionsAsync(
            HttpContext context,
            string allow,
            string? acceptPost = null)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(allow);

            context.Response.StatusCode = StatusCodes.Status204NoContent;
            context.Response.Headers.Allow = allow;

            if (acceptPost is not null)
            {
                context.Response.Headers[c_AcceptPostHeader] = acceptPost;
            }

            return Task.CompletedTask;
        }

        #endregion

        #region Private Members

        // What a request is answered with, and what the log says of it.
        private sealed record Answer(
            IResult Result,
            int Status,
            string? Problem = null,
            ExitCode? ExitCode = null);

        private async Task<Answer> CompileAnswerAsync(HttpContext context)
        {
            var problems = new ProblemCollector();
            bool includeConsole = ReadInclude(context.Request, problems);

            // What it is answered with depends on what the request accepts.
            context.Response.Headers.Vary = c_AcceptHeader;

            string? mediaType = AcceptHelper.Choose(context.Request, [c_JsonMediaType, c_ZipMediaType]);
            if (mediaType is null)
            {
                return NotAcceptable(context, [c_JsonMediaType, c_ZipMediaType]);
            }

            (IFormCollection? form, Answer? refusal) = await ReadFormAsync(context, Resource.ProjectPlan.Messages.Message_ServeRequestNotMultipartCompile);
            if (form is null)
            {
                return refusal!;
            }

            ReadParts(form, [c_ProjectPart, c_ImportPart, c_OptionsPart], [c_ProjectPart, c_ImportPart], problems);

            IFormFile? project = form.Files.GetFile(c_ProjectPart);
            IFormFile? import = form.Files.GetFile(c_ImportPart);

            if (project is null
                && import is null
                && !form.ContainsKey(c_ProjectPart)
                && !form.ContainsKey(c_ImportPart))
            {
                problems.AddInvalid(PartError(c_ProjectPart, ProblemCodes.Required, Resource.ProjectPlan.Messages.Message_ServeErrorProjectRequired));
            }

            if (project is not null
                && import is not null)
            {
                problems.AddInvalid(PartError(c_ImportPart, ProblemCodes.NotAllowed, Resource.ProjectPlan.Messages.Message_ServeErrorProjectOrImport));
            }

            // The one file sent as the plan, if one was.
            IFormFile? plan = (project is null) == (import is null) ? null : project ?? import;
            string part = import is not null ? c_ImportPart : c_ProjectPart;
            string projectTitle = string.Empty;
            ProjectScenarioImportFormat? importFormat = null;

            if (plan is not null)
            {
                // Only the name: a path that came with it says nothing about where the server is.
                string filename = Path.GetFileName(plan.FileName);
                projectTitle = FileFormatHelper.GetProjectTitle(filename);

                if (projectTitle.Length == 0)
                {
                    problems.AddInvalid(PartError(part, ProblemCodes.Required, Resource.ProjectPlan.Messages.Message_ServeErrorFileNeedsName));
                }

                if (import is not null)
                {
                    // Workbooks only: zpp serve does not import MS Project's files, which need the Java runtime that MPXJ
                    // brings with it.
                    if (!string.Equals(Path.GetExtension(filename).TrimStart('.'), Resource.ProjectPlan.Filters.Filter_ProjectXlsxFileExtension, StringComparison.OrdinalIgnoreCase))
                    {
                        return Refuse(
                            context,
                            StatusCodes.Status415UnsupportedMediaType,
                            string.Format(CultureInfo.CurrentCulture, Resource.ProjectPlan.Messages.Message_ServeCannotImport, filename),
                            [PartError(c_ImportPart, ProblemCodes.UnsupportedFormat, Resource.ProjectPlan.Messages.Message_ServeErrorMustBeAWorkbook)]);
                    }

                    importFormat = ProjectScenarioImportFormat.Xlsx;
                }
            }

            (string? optionsJson, Answer? tooLarge) = await ReadOptionsJsonAsync(context, form);
            if (tooLarge is not null)
            {
                return tooLarge;
            }

            CompileOptions? options = CompileOptionsHelper.Read(optionsJson, import is not null, m_Limits, problems);

            if (problems.HasErrors
                || plan is null
                || options is null)
            {
                return Invalid(context, problems);
            }

            await using Stream stream = plan.OpenReadStream();
            JobRequest request = CompileOptionsHelper.ToJobRequest(options, stream, importFormat, m_Limits);

            // The time the job's files are stamped with in a zip: the time it runs at.
            DateTimeOffset runsAt = request.Now ?? m_TimeProvider.GetLocalNow();

            var console = new BufferedConsole();
            var sink = new MemoryJobSink(console);
            JobResult result;
            ExitCode exitCode;

            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(m_Limits.JobTimeoutSeconds)))
            using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, timeout.Token))
            {
                try
                {
                    result = await m_JobRunner.RunAsync(request, sink, cancellation.Token);
                    exitCode = await JobConsoleHelper.WriteResultAsync(console, result, options.MetricsFormat);
                }
                catch (OperationCanceledException) when (HasTimedOut(context, timeout))
                {
                    return TimedOut(context);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !cancellation.IsCancellationRequested)
                {
                    // Whatever zpp would have printed and exited with. A job the caller gave up on, nobody hears about.
                    return await FailedAsync(context, ex, console, includeConsole, part);
                }
            }

            ConsoleResponse? consoleResponse = includeConsole ? ToConsoleResponse(console, exitCode) : null;
            List<OutputResponse> outputs = [.. sink.Outputs.Select(x => ToOutputResponse(x.Output, x.Content, options, projectTitle))];

            switch (result.Status)
            {
                case JobStatus.CompilationErrors:
                    return CompilationFailed(context, result, part, exitCode, consoleResponse);
                case JobStatus.CompletedWithErrors:
                    return OutputFailed(context, result, options, sink, outputs, exitCode, consoleResponse);
            }

            var response = new CompileResponse(
                MetricsResponse.From(result.Metrics ?? throw new InvalidOperationException()),
                outputs,
                consoleResponse);

            return new Answer(
                mediaType == c_ZipMediaType ? Zip(response, projectTitle, runsAt) : Results.Json(response, JsonOptions),
                StatusCodes.Status200OK,
                ExitCode: exitCode);
        }

        private async Task<Answer> ListScenariosAnswerAsync(HttpContext context)
        {
            var problems = new ProblemCollector();
            bool includeConsole = ReadInclude(context.Request, problems);

            if (AcceptHelper.Choose(context.Request, [c_JsonMediaType]) is null)
            {
                return NotAcceptable(context, [c_JsonMediaType]);
            }

            (IFormCollection? form, Answer? refusal) = await ReadFormAsync(context, Resource.ProjectPlan.Messages.Message_ServeRequestNotMultipartScenarios);
            if (form is null)
            {
                return refusal!;
            }

            // A project's, as --list-scenarios is only valid with --input.
            ReadParts(form, [c_ProjectPart], [c_ProjectPart], problems);

            IFormFile? project = form.Files.GetFile(c_ProjectPart);

            if (project is null
                && !form.ContainsKey(c_ProjectPart))
            {
                problems.AddInvalid(PartError(c_ProjectPart, ProblemCodes.Required, Resource.ProjectPlan.Messages.Message_ServeErrorProjectRequiredForScenarios));
            }

            if (problems.HasErrors
                || project is null)
            {
                return Invalid(context, problems);
            }

            await using Stream stream = project.OpenReadStream();

            var console = new BufferedConsole();
            IReadOnlyList<ScenarioSummary> scenarios;

            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(m_Limits.JobTimeoutSeconds)))
            using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, timeout.Token))
            {
                try
                {
                    scenarios = await m_JobRunner.ListScenariosAsync(stream, cancellation.Token);
                    await JobConsoleHelper.WriteScenariosAsync(console, scenarios);
                }
                catch (OperationCanceledException) when (HasTimedOut(context, timeout))
                {
                    return TimedOut(context);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !cancellation.IsCancellationRequested)
                {
                    return await FailedAsync(context, ex, console, includeConsole, c_ProjectPart);
                }
            }

            return new Answer(
                Results.Json(new ScenariosResponse(scenarios, includeConsole ? ToConsoleResponse(console, ExitCode.Success) : null), JsonOptions),
                StatusCodes.Status200OK,
                ExitCode: ExitCode.Success);
        }

        // Whether the request asks for the console, which it does with include=console. Anything else it asks to include, and
        // any other parameter, is a problem with the request: a misspelt one must not be ignored.
        private static bool ReadInclude(
            HttpRequest request,
            ProblemCollector problems)
        {
            bool includeConsole = false;

            foreach (string key in request.Query.Keys)
            {
                if (!string.Equals(key, c_IncludeParameter, StringComparison.Ordinal))
                {
                    problems.AddMalformed(new ProblemError
                    {
                        Parameter = key,
                        Code = ProblemCodes.UnknownProperty,
                        Detail = Resource.ProjectPlan.Messages.Message_ServeErrorParameterNotKnown,
                    });
                    continue;
                }

                foreach (string? value in request.Query[key])
                {
                    foreach (string inclusion in (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (string.Equals(inclusion, c_ConsoleInclusion, StringComparison.Ordinal))
                        {
                            includeConsole = true;
                        }
                        else
                        {
                            problems.AddMalformed(new ProblemError
                            {
                                Parameter = c_IncludeParameter,
                                Code = ProblemCodes.NotAllowed,
                                Detail = string.Format(CultureInfo.CurrentCulture, Resource.ProjectPlan.Messages.Message_ServeErrorIncludeNotKnown, inclusion),
                            });
                        }
                    }
                }
            }

            return includeConsole;
        }

        // The request's parts, or why they cannot be read.
        private async Task<(IFormCollection? Form, Answer? Refusal)> ReadFormAsync(
            HttpContext context,
            string notMultipart)
        {
            if (!context.Request.HasFormContentType)
            {
                return (null, Refuse(context, StatusCodes.Status415UnsupportedMediaType, notMultipart));
            }

            try
            {
                return (await context.Request.ReadFormAsync(context.RequestAborted), null);
            }
            catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
            {
                return (null, Refuse(context, StatusCodes.Status413PayloadTooLarge, string.Format(CultureInfo.CurrentCulture, Resource.ProjectPlan.Messages.Message_ServeRequestTooLarge, m_Limits.MaxUploadMegabytes)));
            }
            catch (Exception ex) when (ex is BadHttpRequestException or InvalidDataException)
            {
                // What the framework says of a body it cannot read is its own: the detail says it in ours.
                return (null, Problem(
                    context,
                    ProblemHelper.Create(
                        context,
                        ProblemKind.MalformedRequest,
                        Resource.ProjectPlan.Messages.Message_ServeRequestNotReadable,
                        [new ProblemError { Pointer = c_RequestPointer, Code = ProblemCodes.InvalidFormat, Detail = Resource.ProjectPlan.Messages.Message_ServeErrorMustBeMultipart }])));
            }
        }

        // The parts that are not ones the endpoint takes, and the parts that are files and were sent as fields.
        private static void ReadParts(
            IFormCollection form,
            IReadOnlyList<string> known,
            IReadOnlyList<string> files,
            ProblemCollector problems)
        {
            foreach (string name in form.Keys.Concat(form.Files.Select(x => x.Name)).Distinct(StringComparer.Ordinal))
            {
                if (!known.Contains(name))
                {
                    problems.AddInvalid(PartError(name, ProblemCodes.UnknownProperty, string.Format(CultureInfo.CurrentCulture, Resource.ProjectPlan.Messages.Message_ServeErrorPartNotKnown, name)));
                }
                else if (files.Contains(name)
                    && form.ContainsKey(name))
                {
                    problems.AddInvalid(PartError(name, ProblemCodes.WrongType, Resource.ProjectPlan.Messages.Message_ServeErrorMustBeAFile));
                }
            }
        }

        // The JSON of the options - zpp's defaults when there is none - or a refusal, when the part is larger than options
        // are.
        private async Task<(string? Json, Answer? Refusal)> ReadOptionsJsonAsync(
            HttpContext context,
            IFormCollection form)
        {
            string? json;

            if (form.Files.GetFile(c_OptionsPart) is IFormFile file)
            {
                if (file.Length > CompileOptionsHelper.MaxOptionsBytes)
                {
                    return (null, OptionsTooLarge(context));
                }

                using var reader = new StreamReader(file.OpenReadStream());
                json = await reader.ReadToEndAsync(context.RequestAborted);
            }
            else
            {
                json = form.TryGetValue(c_OptionsPart, out StringValues value) ? value.ToString() : null;

                if (json is not null
                    && Encoding.UTF8.GetByteCount(json) > CompileOptionsHelper.MaxOptionsBytes)
                {
                    return (null, OptionsTooLarge(context));
                }
            }

            return (json, null);
        }

        private static ProblemError PartError(
            string part,
            string code,
            string detail)
        {
            return new ProblemError { Pointer = CompileOptionsHelper.GetPointer(c_RequestPointer, part), Code = code, Detail = detail };
        }

        private static Answer OptionsTooLarge(HttpContext context)
        {
            return Refuse(
                context,
                StatusCodes.Status413PayloadTooLarge,
                string.Format(CultureInfo.CurrentCulture, Resource.ProjectPlan.Messages.Message_ServeOptionsTooLarge, CompileOptionsHelper.MaxOptionsBytes / 1024),
                [PartError(c_OptionsPart, ProblemCodes.TooLarge, string.Format(CultureInfo.CurrentCulture, Resource.ProjectPlan.Messages.Message_ServeErrorOptionsTooLarge, CompileOptionsHelper.MaxOptionsBytes / 1024))]);
        }

        // Whether the job stopped because it ran out of time, rather than because the caller went away.
        private static bool HasTimedOut(
            HttpContext context,
            CancellationTokenSource timeout)
        {
            return timeout.IsCancellationRequested
                && !context.RequestAborted.IsCancellationRequested;
        }

        #endregion

        #region Answers

        // A request that cannot be answered as it asks to be: the problem lists every error found in it, and is the more
        // general of the statuses of those there are - 400 when any is about the request as it was sent.
        private static Answer Invalid(
            HttpContext context,
            ProblemCollector problems)
        {
            int count = problems.Errors.Count;
            string detail = count == 1
                ? Resource.ProjectPlan.Messages.Message_ServeRequestHasAProblem
                : string.Format(CultureInfo.CurrentCulture, Resource.ProjectPlan.Messages.Message_ServeRequestHasProblems, count);

            return Problem(
                context,
                ProblemHelper.Create(
                    context,
                    problems.IsMalformed ? ProblemKind.MalformedRequest : ProblemKind.ValidationFailed,
                    detail,
                    problems.Errors));
        }

        private static Answer Refuse(
            HttpContext context,
            int status,
            string detail,
            IReadOnlyList<ProblemError>? errors = null)
        {
            return Problem(context, ProblemHelper.CreateForStatus(context, status, detail, errors));
        }

        private static Answer NotAcceptable(
            HttpContext context,
            IReadOnlyList<string> offered)
        {
            return Problem(
                context,
                ProblemHelper.CreateForStatus(
                    context,
                    StatusCodes.Status406NotAcceptable,
                    string.Format(CultureInfo.CurrentCulture, Resource.ProjectPlan.Messages.Message_ServeNotAcceptable, string.Join(@", ", offered))));
        }

        private static Answer Problem(
            HttpContext context,
            ProblemResponse problem,
            ExitCode? exitCode = null)
        {
            // What the server tells the caller to do about it: wait, for a problem that is a matter of waiting.
            if (problem.Status == StatusCodes.Status503ServiceUnavailable)
            {
                context.Response.Headers.RetryAfter = JobServer.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            }

            return new Answer(
                ProblemHelper.ToResult(problem),
                problem.Status,
                ProblemHelper.TryGetKind(problem.Type, out ProblemKind kind) ? ProblemHelper.GetSlug(kind) : null,
                exitCode);
        }

        private Answer TimedOut(HttpContext context)
        {
            m_Logger.LogWarning("{Path}: stopped after running for {JobTimeoutSeconds} s", context.Request.Path, m_Limits.JobTimeoutSeconds);

            return Problem(
                context,
                ProblemHelper.Create(
                    context,
                    ProblemKind.JobTimeout,
                    string.Format(CultureInfo.CurrentCulture, Resource.ProjectPlan.Messages.Message_ServeJobTimedOut, m_Limits.JobTimeoutSeconds)));
        }

        // A job that stopped on an exception, as the problem it is: a file that cannot be read, a scenario that cannot be
        // selected, a compilation that ran out of time - each as zpp says it on stderr - or something nothing expected, which
        // is logged here, with the request's trace id, and said only generally to the caller, whom the exception's own words
        // are not for.
        private async Task<Answer> FailedAsync(
            HttpContext context,
            Exception exception,
            BufferedConsole console,
            bool includeConsole,
            string part)
        {
            ProblemKind kind;
            string detail;
            List<ProblemError> errors = [];
            ExitCode exitCode;

            switch (exception)
            {
                case ProjectNotReadableException:
                    exitCode = await JobConsoleHelper.WriteFailureAsync(console, exception);
                    kind = ProblemKind.ProjectNotReadable;
                    detail = part == c_ImportPart
                        ? Resource.ProjectPlan.Messages.Message_ServeWorkbookNotReadable
                        : Resource.ProjectPlan.Messages.Message_ServeProjectNotReadable;
                    errors.Add(PartError(part, ProblemCodes.Unreadable, detail));
                    break;
                case ScenarioSelectionException selection:
                    exitCode = await JobConsoleHelper.WriteFailureAsync(console, exception);
                    kind = ProblemKind.ScenarioNotSelectable;
                    (string code, detail) = DescribeSelection(selection);
                    errors.Add(new ProblemError
                    {
                        Pointer = CompileOptionsHelper.GetPointer(CompileOptionsHelper.OptionsPointer, @"scenario"),
                        Code = code,
                        Detail = detail,
                    });
                    break;
                case GraphCompilationTimeoutException:
                    exitCode = await JobConsoleHelper.WriteFailureAsync(console, exception);
                    kind = ProblemKind.CompilationTimedOut;
                    detail = Resource.ProjectPlan.Messages.Message_ServeCompilationTimedOut;
                    break;
                default:
                    m_Logger.LogError(exception, "{Path}: failed unexpectedly", context.Request.Path);
                    await console.WriteErrorLineAsync(string.Format(CultureInfo.CurrentCulture, Resource.ProjectPlan.Messages.Message_ServeUnexpectedErrorConsole, RequestIdHelper.Get(context)));
                    exitCode = ExitCode.Failure;
                    kind = ProblemKind.UnexpectedError;
                    detail = Resource.ProjectPlan.Messages.Message_ServeUnexpectedError;
                    break;
            }

            return Problem(
                context,
                ProblemHelper.Create(
                    context,
                    kind,
                    detail,
                    errors.Count > 0 ? errors : null,
                    includeConsole ? ToConsoleResponse(console, exitCode) : null),
                exitCode);
        }

        // What a scenario that cannot be selected is, as a code, and what is wrong with it, in the API's words and not zpp's,
        // which point at an option of its own.
        private static (string Code, string Detail) DescribeSelection(ScenarioSelectionException exception)
        {
            return exception.Failure switch
            {
                ScenarioSelectionFailure.NoMatch => (ProblemCodes.NotFound, string.Format(CultureInfo.CurrentCulture, Resource.ProjectPlan.Messages.Message_NoScenarioMatchesSelector, exception.Selector)),
                ScenarioSelectionFailure.SeveralMatches => (ProblemCodes.Ambiguous, string.Format(CultureInfo.CurrentCulture, Resource.ProjectPlan.Messages.Message_SeveralScenariosMatchSelector, exception.Selector, exception.MatchCount)),
                ScenarioSelectionFailure.NoScenarioData => (ProblemCodes.HasNoData, string.Format(CultureInfo.CurrentCulture, Resource.ProjectPlan.Messages.Message_ScenarioHasNoScenarioData, exception.Selector)),
                _ => (ProblemCodes.NotFound, exception.Message),
            };
        }

        // A project that did not compile: each of the compiler's errors, with its own code, where the project is. Its message
        // is the compiler's, built with the system's line end, which the response says as it says every other: with \n.
        private static Answer CompilationFailed(
            HttpContext context,
            JobResult result,
            string part,
            ExitCode exitCode,
            ConsoleResponse? console)
        {
            List<ProblemError> errors =
            [
                .. result.CompilationErrors.Select(x => PartError(part, x.Code, NewLineHelper.NormalizeNewLines(x.Message.TrimEnd()))),
            ];

            string detail = errors.Count == 1
                ? Resource.ProjectPlan.Messages.Message_ServeProjectHasACompilationError
                : string.Format(CultureInfo.CurrentCulture, Resource.ProjectPlan.Messages.Message_ServeProjectHasCompilationErrors, errors.Count);

            return Problem(
                context,
                ProblemHelper.Create(context, ProblemKind.CompilationFailed, detail, errors, console),
                exitCode);
        }

        // A project that compiled, whose outputs the job went on to produce - those it could - one or more of which could
        // not be: the problem has the metrics and the outputs that were produced, which a caller may still want.
        private static Answer OutputFailed(
            HttpContext context,
            JobResult result,
            CompileOptions options,
            MemoryJobSink sink,
            IReadOnlyList<OutputResponse> outputs,
            ExitCode exitCode,
            ConsoleResponse? console)
        {
            HashSet<JobOutput> produced = [.. sink.Outputs.Select(x => x.Output)];
            string outputsPointer = CompileOptionsHelper.GetPointer(CompileOptionsHelper.OptionsPointer, @"outputs");

            List<ProblemError> errors =
            [
                .. CompileOptionsHelper.GetRequestedOutputs(options)
                    .Where(x => !produced.Contains(x))
                    .Select(x => new ProblemError
                    {
                        Pointer = CompileOptionsHelper.GetPointer(outputsPointer, CompileOptionsHelper.GetOutputName(x)),
                        Code = ProblemCodes.Failed,
                        Detail = string.Format(CultureInfo.CurrentCulture, Resource.ProjectPlan.Messages.Message_ServeOutputFailed, Words(CompileOptionsHelper.GetOutputName(x))),
                    }),
            ];

            string detail = errors.Count switch
            {
                0 => Resource.ProjectPlan.Messages.Message_ServeAnOutputFailed,
                1 => errors[0].Detail,
                _ => string.Format(CultureInfo.CurrentCulture, Resource.ProjectPlan.Messages.Message_ServeOutputsFailed, errors.Count),
            };

            return Problem(
                context,
                ProblemHelper.Create(
                    context,
                    ProblemKind.OutputFailed,
                    detail,
                    errors.Count > 0 ? errors : null,
                    console,
                    MetricsResponse.From(result.Metrics ?? throw new InvalidOperationException()),
                    outputs),
                exitCode);
        }

        // An output's name - ganttChart - as words: gantt chart.
        private static string Words(string name)
        {
            var words = new StringBuilder();

            foreach (char c in name)
            {
                if (char.IsUpper(c))
                {
                    words.Append(' ');
                }

                words.Append(char.ToLowerInvariant(c));
            }

            return words.ToString();
        }

        private static ConsoleResponse ToConsoleResponse(
            BufferedConsole console,
            ExitCode exitCode)
        {
            return new ConsoleResponse((int)exitCode, console.Output, console.Error, console.Transcript);
        }

        private static OutputResponse ToOutputResponse(
            JobOutput output,
            byte[] content,
            CompileOptions options,
            string projectTitle)
        {
            string filename = CompileOptionsHelper.BuildOutputFilename(output, options, projectTitle);
            return new OutputResponse(output, filename, CompileOptionsHelper.GetContentType(filename), content);
        }

        // The outputs, each in the file zpp would have written, with the rest of the response in result.json. Every
        // file is stamped with the time the job ran at - the time now gave it, if it was given one - rather than the
        // time the zip was made.
        private static IResult Zip(
            CompileResponse response,
            string projectTitle,
            DateTimeOffset runsAt)
        {
            DateTimeOffset lastWriteTime = runsAt.DateTime < s_EarliestZipTime
                ? new DateTimeOffset(s_EarliestZipTime, runsAt.Offset)
                : runsAt.DateTime > s_LatestZipTime ? new DateTimeOffset(s_LatestZipTime, runsAt.Offset) : runsAt;

            using var stream = new MemoryStream();

            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (OutputResponse output in response.Outputs)
                {
                    AddZipEntry(zip, output.FileName, lastWriteTime, output.Content ?? []);
                }

                CompileResponse result = response with { Outputs = [.. response.Outputs.Select(x => x with { Content = null })] };
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

        // One line of the log for a request: what it was answered with - the problem, if it was one - and, if a job ran, how it
        // ended. The request's trace id is on it, with every line of the log.
        private void Log(
            HttpContext context,
            Answer answer,
            Stopwatch stopwatch)
        {
            if (answer.ExitCode is ExitCode exitCode)
            {
                m_Logger.LogInformation(
                    "{Method} {Path}: {StatusCode} {Problem}, exit code {ExitCode}, after {ElapsedMilliseconds} ms",
                    context.Request.Method,
                    context.Request.Path,
                    answer.Status,
                    answer.Problem ?? @"ok",
                    (int)exitCode,
                    stopwatch.ElapsedMilliseconds);
            }
            else
            {
                m_Logger.LogInformation(
                    "{Method} {Path}: {StatusCode} {Problem}, after {ElapsedMilliseconds} ms",
                    context.Request.Method,
                    context.Request.Path,
                    answer.Status,
                    answer.Problem ?? @"refused",
                    stopwatch.ElapsedMilliseconds);
            }
        }

        #endregion
    }
}
