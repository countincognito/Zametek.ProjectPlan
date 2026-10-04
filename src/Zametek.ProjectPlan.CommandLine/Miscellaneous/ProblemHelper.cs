using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using System.Text.Json;

namespace Zametek.ProjectPlan.CommandLine
{
    // The problems zpp serve answers with, as RFC 9457 has them: a type, a title, the status, what is wrong, and the trace
    // id of the request. Those of ProblemKind have a type of their own, which docs/API.md documents under its slug and
    // which is never changed - a client branches on it - and a title that is the same each time; the others - an unknown
    // path, a body that is too large - are what their status says, with the type RFC 9110 gives it.
    internal static class ProblemHelper
    {
        // What a problem is written as.
        public const string MediaType = @"application/problem+json";

        // The base of the types of the problems of ProblemKind, which each add its slug: where docs/API.md documents it.
        public const string TypeBase = @"https://github.com/countincognito/Zametek.ProjectPlan/blob/develop/docs/API.md#";

        // The base of the types RFC 9110 gives the statuses.
        private const string c_StatusTypeBase = @"https://tools.ietf.org/html/rfc9110#section-";

        // Each kind's slug, which is also its heading in docs/API.md, and its status.
        private static readonly Dictionary<ProblemKind, (string Slug, int Status)> s_Kinds = new()
        {
            [ProblemKind.MalformedRequest] = (@"malformed-request", StatusCodes.Status400BadRequest),
            [ProblemKind.ValidationFailed] = (@"validation-failed", StatusCodes.Status422UnprocessableEntity),
            [ProblemKind.ProjectNotReadable] = (@"project-not-readable", StatusCodes.Status422UnprocessableEntity),
            [ProblemKind.CompilationFailed] = (@"compilation-failed", StatusCodes.Status422UnprocessableEntity),
            [ProblemKind.CompilationTimedOut] = (@"compilation-timed-out", StatusCodes.Status422UnprocessableEntity),
            [ProblemKind.ScenarioNotSelectable] = (@"scenario-not-selectable", StatusCodes.Status422UnprocessableEntity),
            [ProblemKind.OutputFailed] = (@"output-failed", StatusCodes.Status500InternalServerError),
            [ProblemKind.UnexpectedError] = (@"unexpected-error", StatusCodes.Status500InternalServerError),
            [ProblemKind.Busy] = (@"busy", StatusCodes.Status503ServiceUnavailable),
            [ProblemKind.JobTimeout] = (@"job-timeout", StatusCodes.Status503ServiceUnavailable),
        };

        // The sections of RFC 9110 that describe the statuses zpp serve can answer with.
        private static readonly Dictionary<int, string> s_StatusSections = new()
        {
            [StatusCodes.Status400BadRequest] = @"15.5.1",
            [StatusCodes.Status401Unauthorized] = @"15.5.2",
            [StatusCodes.Status403Forbidden] = @"15.5.4",
            [StatusCodes.Status404NotFound] = @"15.5.5",
            [StatusCodes.Status405MethodNotAllowed] = @"15.5.6",
            [StatusCodes.Status406NotAcceptable] = @"15.5.7",
            [StatusCodes.Status408RequestTimeout] = @"15.5.9",
            [StatusCodes.Status413PayloadTooLarge] = @"15.5.14",
            [StatusCodes.Status414UriTooLong] = @"15.5.15",
            [StatusCodes.Status415UnsupportedMediaType] = @"15.5.16",
            [StatusCodes.Status422UnprocessableEntity] = @"15.5.21",
            [StatusCodes.Status500InternalServerError] = @"15.6.1",
            [StatusCodes.Status501NotImplemented] = @"15.6.2",
            [StatusCodes.Status503ServiceUnavailable] = @"15.6.4",
        };

        public static string GetSlug(ProblemKind kind)
        {
            return s_Kinds[kind].Slug;
        }

        public static string GetType(ProblemKind kind)
        {
            return TypeBase + s_Kinds[kind].Slug;
        }

        public static int GetStatus(ProblemKind kind)
        {
            return s_Kinds[kind].Status;
        }

        public static string GetTitle(ProblemKind kind)
        {
            return kind switch
            {
                ProblemKind.MalformedRequest => Resource.ProjectPlan.Messages.Message_ServeTitleMalformedRequest,
                ProblemKind.ValidationFailed => Resource.ProjectPlan.Messages.Message_ServeTitleValidationFailed,
                ProblemKind.ProjectNotReadable => Resource.ProjectPlan.Messages.Message_ServeTitleProjectNotReadable,
                ProblemKind.CompilationFailed => Resource.ProjectPlan.Messages.Message_ServeTitleCompilationFailed,
                ProblemKind.CompilationTimedOut => Resource.ProjectPlan.Messages.Message_ServeTitleCompilationTimedOut,
                ProblemKind.ScenarioNotSelectable => Resource.ProjectPlan.Messages.Message_ServeTitleScenarioNotSelectable,
                ProblemKind.OutputFailed => Resource.ProjectPlan.Messages.Message_ServeTitleOutputFailed,
                ProblemKind.UnexpectedError => Resource.ProjectPlan.Messages.Message_ServeTitleUnexpectedError,
                ProblemKind.Busy => Resource.ProjectPlan.Messages.Message_ServeTitleBusy,
                ProblemKind.JobTimeout => Resource.ProjectPlan.Messages.Message_ServeTitleJobTimeout,
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
            };
        }

        // The kind a problem's type names, if it names one: a client branches on it, and takes any other as its status says.
        public static bool TryGetKind(
            string? type,
            out ProblemKind kind)
        {
            foreach ((ProblemKind candidate, (string slug, _)) in s_Kinds)
            {
                if (string.Equals(type, TypeBase + slug, StringComparison.Ordinal))
                {
                    kind = candidate;
                    return true;
                }
            }

            kind = default;
            return false;
        }

        // A problem of a kind, for the request: with the extensions it has, and none that it has not.
        public static ProblemResponse Create(
            HttpContext context,
            ProblemKind kind,
            string? detail,
            IReadOnlyList<ProblemError>? errors = null,
            ConsoleResponse? console = null,
            MetricsResponse? metrics = null,
            IReadOnlyList<OutputResponse>? outputs = null)
        {
            ArgumentNullException.ThrowIfNull(context);

            return new ProblemResponse
            {
                Type = GetType(kind),
                Title = GetTitle(kind),
                Status = GetStatus(kind),
                Detail = detail,
                TraceId = RequestIdHelper.Get(context),
                Errors = errors,
                Metrics = metrics,
                Outputs = outputs,
                Console = console,
            };
        }

        // A problem that is what its status says: the title is the status's, and the type RFC 9110's section on it.
        public static ProblemResponse CreateForStatus(
            HttpContext context,
            int status,
            string? detail = null,
            IReadOnlyList<ProblemError>? errors = null)
        {
            ArgumentNullException.ThrowIfNull(context);

            return new ProblemResponse
            {
                Type = s_StatusSections.TryGetValue(status, out string? section) ? c_StatusTypeBase + section : @"about:blank",
                Title = ReasonPhrases.GetReasonPhrase(status),
                Status = status,
                Detail = detail,
                TraceId = RequestIdHelper.Get(context),
                Errors = errors,
            };
        }

        // The problem as the answer to the request.
        public static IResult ToResult(ProblemResponse problem)
        {
            ArgumentNullException.ThrowIfNull(problem);
            return Results.Json(problem, JobJsonHelper.ServerOptions, MediaType, problem.Status);
        }

        // Answers a request that failed in a way nothing expected: the problem says only that, and the exception - which
        // the exception handler has already logged, with the request's trace id - is not in it.
        public static async Task WriteUnexpectedErrorAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            await ToResult(Create(context, ProblemKind.UnexpectedError, Resource.ProjectPlan.Messages.Message_ServeUnexpectedError)).ExecuteAsync(context);
        }

        // Answers a request that nothing answered with a body - an unknown path, a method the path does not take - with a
        // problem, as the framework would answer a status with a page.
        public static async Task WriteStatusAsync(StatusCodeContext statusContext)
        {
            ArgumentNullException.ThrowIfNull(statusContext);

            HttpContext context = statusContext.HttpContext;
            await ToResult(CreateForStatus(context, context.Response.StatusCode)).ExecuteAsync(context);
        }
    }
}
