using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;
using Zametek.Engine.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for the problems zpp serve answers with: each kind has a type that is its slug under the documentation's address,
    /// a title and a status of its own, which never change; the others are what their status says, with the type RFC 9110
    /// gives it; and every one has the trace id of the request.
    /// </summary>
    public class ProblemHelperTests
    {
        private static readonly Regex s_Slug = new(@"^[a-z]+(-[a-z]+)*$");

        private static string Kebab(string pascal)
        {
            return string.Concat(pascal.Select((c, i) => char.IsUpper(c) ? (i == 0 ? string.Empty : @"-") + char.ToLowerInvariant(c) : c.ToString()));
        }

        public static TheoryData<ProblemKind, int> Statuses => new()
        {
            { ProblemKind.MalformedRequest, 400 },
            { ProblemKind.ValidationFailed, 422 },
            { ProblemKind.ProjectNotReadable, 422 },
            { ProblemKind.CompilationFailed, 422 },
            { ProblemKind.CompilationTimedOut, 422 },
            { ProblemKind.ScenarioNotSelectable, 422 },
            { ProblemKind.OutputFailed, 500 },
            { ProblemKind.UnexpectedError, 500 },
            { ProblemKind.Busy, 503 },
            { ProblemKind.JobTimeout, 503 },
        };

        [Theory]
        [MemberData(nameof(Statuses))]
        public void GetStatus_Given_AKind_Then_ItsStatusThatNeverChanges(ProblemKind kind, int status)
        {
            ProblemHelper.GetStatus(kind).ShouldBe(status);
        }

        [Fact]
        public void Statuses_Given_EveryKind_Then_TheTableHasEach()
        {
            Statuses.Select(x => (ProblemKind)x[0]).ShouldBe(Enum.GetValues<ProblemKind>());
        }

        [Fact]
        public void GetSlug_Given_EveryKind_Then_ItsNameInKebabCase()
        {
            foreach (ProblemKind kind in Enum.GetValues<ProblemKind>())
            {
                ProblemHelper.GetSlug(kind).ShouldBe(Kebab(kind.ToString()));
                ProblemHelper.GetSlug(kind).ShouldMatch(s_Slug.ToString());
            }

            Enum.GetValues<ProblemKind>().Select(ProblemHelper.GetSlug).Distinct().Count().ShouldBe(Enum.GetValues<ProblemKind>().Length);
        }

        [Fact]
        public void GetType_Given_EveryKind_Then_ItsSlugUnderTheDocumentationsAddress()
        {
            ProblemHelper.TypeBase.ShouldBe(@"https://github.com/countincognito/Zametek.ProjectPlan/blob/develop/docs/API.md#");

            foreach (ProblemKind kind in Enum.GetValues<ProblemKind>())
            {
                ProblemHelper.GetType(kind).ShouldBe(ProblemHelper.TypeBase + ProblemHelper.GetSlug(kind));
            }
        }

        [Fact]
        public void GetTitle_Given_EveryKind_Then_ItsOwnAndNeverEmpty()
        {
            string[] titles = [.. Enum.GetValues<ProblemKind>().Select(ProblemHelper.GetTitle)];

            titles.ShouldAllBe(x => !string.IsNullOrWhiteSpace(x));
            titles.Distinct().Count().ShouldBe(titles.Length);
        }

        [Fact]
        public void TryGetKind_Given_TheTypeOfAKind_Then_ThatKind()
        {
            foreach (ProblemKind kind in Enum.GetValues<ProblemKind>())
            {
                ProblemHelper.TryGetKind(ProblemHelper.GetType(kind), out ProblemKind found).ShouldBeTrue();
                found.ShouldBe(kind);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData(@"")]
        [InlineData(@"about:blank")]
        [InlineData(@"https://tools.ietf.org/html/rfc9110#section-15.5.21")]
        [InlineData(@"https://github.com/countincognito/Zametek.ProjectPlan/blob/develop/docs/API.md#")]
        [InlineData(@"https://github.com/countincognito/Zametek.ProjectPlan/blob/develop/docs/API.md#unknown-kind")]
        [InlineData(@"https://github.com/countincognito/Zametek.ProjectPlan/blob/develop/docs/API.md#Busy")]
        [InlineData(@"https://github.com/countincognito/Zametek.ProjectPlan/blob/develop/docs/API.md#busy ")]
        [InlineData(@"HTTPS://GITHUB.COM/COUNTINCOGNITO/ZAMETEK.PROJECTPLAN/BLOB/DEVELOP/DOCS/API.MD#BUSY")]
        public void TryGetKind_Given_AnyOtherType_Then_NoKind(string? type)
        {
            ProblemHelper.TryGetKind(type, out ProblemKind kind).ShouldBeFalse();

            kind.ShouldBe(default);
        }

        private static DefaultHttpContext ContextWithTraceId(out string traceId)
        {
            var context = new DefaultHttpContext();
            traceId = RequestIdHelper.Get(context);
            return context;
        }

        [Fact]
        public void Create_Given_AKind_Then_AProblemOfItsTypeTitleAndStatusWithTheRequestsTraceId()
        {
            DefaultHttpContext context = ContextWithTraceId(out string traceId);

            ProblemResponse problem = ProblemHelper.Create(context, ProblemKind.CompilationFailed, @"It did not compile.");

            problem.Type.ShouldBe(ProblemHelper.GetType(ProblemKind.CompilationFailed));
            problem.Title.ShouldBe(ProblemHelper.GetTitle(ProblemKind.CompilationFailed));
            problem.Status.ShouldBe(422);
            problem.Detail.ShouldBe(@"It did not compile.");
            problem.TraceId.ShouldBe(traceId);
            problem.Errors.ShouldBeNull();
            problem.Metrics.ShouldBeNull();
            problem.Outputs.ShouldBeNull();
            problem.Console.ShouldBeNull();
        }

        [Fact]
        public void Create_Given_ExtensionsAndErrors_Then_TheProblemHasThem()
        {
            DefaultHttpContext context = ContextWithTraceId(out _);
            var errors = new[] { new ProblemError { Pointer = @"#/project", Code = @"x", Detail = @"y" } };
            var console = new ConsoleResponse(1, @"a", @"b", []);
            var metrics = new MetricsResponse { NetworkDuration = 5 };
            var outputs = new[] { new OutputResponse(JobOutput.Project, @"p.zpp", @"application/json", [1]) };

            ProblemResponse problem = ProblemHelper.Create(context, ProblemKind.OutputFailed, null, errors, console, metrics, outputs);

            problem.Detail.ShouldBeNull();
            problem.Errors.ShouldBe(errors);
            problem.Console.ShouldBe(console);
            problem.Metrics.ShouldBe(metrics);
            problem.Outputs.ShouldBe(outputs);
        }

        [Fact]
        public void Create_Given_ANullContext_Then_Throws()
        {
            Should.Throw<ArgumentNullException>(() => ProblemHelper.Create(null!, ProblemKind.Busy, null));
        }

        [Theory]
        [InlineData(400, @"https://tools.ietf.org/html/rfc9110#section-15.5.1", @"Bad Request")]
        [InlineData(401, @"https://tools.ietf.org/html/rfc9110#section-15.5.2", @"Unauthorized")]
        [InlineData(403, @"https://tools.ietf.org/html/rfc9110#section-15.5.4", @"Forbidden")]
        [InlineData(404, @"https://tools.ietf.org/html/rfc9110#section-15.5.5", @"Not Found")]
        [InlineData(405, @"https://tools.ietf.org/html/rfc9110#section-15.5.6", @"Method Not Allowed")]
        [InlineData(406, @"https://tools.ietf.org/html/rfc9110#section-15.5.7", @"Not Acceptable")]
        [InlineData(408, @"https://tools.ietf.org/html/rfc9110#section-15.5.9", @"Request Timeout")]
        [InlineData(413, @"https://tools.ietf.org/html/rfc9110#section-15.5.14", @"Payload Too Large")]
        [InlineData(414, @"https://tools.ietf.org/html/rfc9110#section-15.5.15", @"URI Too Long")]
        [InlineData(415, @"https://tools.ietf.org/html/rfc9110#section-15.5.16", @"Unsupported Media Type")]
        [InlineData(422, @"https://tools.ietf.org/html/rfc9110#section-15.5.21", @"Unprocessable Entity")]
        [InlineData(500, @"https://tools.ietf.org/html/rfc9110#section-15.6.1", @"Internal Server Error")]
        [InlineData(501, @"https://tools.ietf.org/html/rfc9110#section-15.6.2", @"Not Implemented")]
        [InlineData(503, @"https://tools.ietf.org/html/rfc9110#section-15.6.4", @"Service Unavailable")]
        public void CreateForStatus_Given_AStatus_Then_ItsTypeAndTitleAsRfc9110HasThem(int status, string type, string title)
        {
            DefaultHttpContext context = ContextWithTraceId(out string traceId);

            ProblemResponse problem = ProblemHelper.CreateForStatus(context, status, @"Why.");

            problem.Type.ShouldBe(type);
            problem.Title.ShouldBe(title);
            problem.Status.ShouldBe(status);
            problem.Detail.ShouldBe(@"Why.");
            problem.TraceId.ShouldBe(traceId);
            problem.Errors.ShouldBeNull();
        }

        [Fact]
        public void CreateForStatus_Given_AStatusWithNoSectionOfItsOwn_Then_AboutBlank()
        {
            DefaultHttpContext context = ContextWithTraceId(out _);

            ProblemResponse problem = ProblemHelper.CreateForStatus(context, 418);

            problem.Type.ShouldBe(@"about:blank");
            problem.Title.ShouldBe(@"I'm a teapot");
            problem.Detail.ShouldBeNull();
        }

        [Fact]
        public void CreateForStatus_Given_Errors_Then_TheProblemHasThem()
        {
            DefaultHttpContext context = ContextWithTraceId(out _);
            var errors = new[] { new ProblemError { Pointer = @"#/options", Code = @"tooLarge", Detail = @"must be at most 64 KB" } };

            ProblemHelper.CreateForStatus(context, 413, @"Too large.", errors).Errors.ShouldBe(errors);
        }

        [Fact]
        public async Task ToResult_Given_AProblem_Then_AnswersWithItsStatusAndTheProblemMediaType()
        {
            DefaultHttpContext context = ContextWithTraceId(out _);
            context.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
            using var body = new MemoryStream();
            context.Response.Body = body;
            ProblemResponse problem = ProblemHelper.Create(context, ProblemKind.JobTimeout, @"Too long.");

            await ProblemHelper.ToResult(problem).ExecuteAsync(context);

            context.Response.StatusCode.ShouldBe(503);
            context.Response.ContentType.ShouldStartWith(@"application/problem+json");
            ProblemHelper.MediaType.ShouldBe(@"application/problem+json");
            JsonSerializer.Deserialize<ProblemResponse>(body.ToArray(), JobJsonHelper.ServerOptions).ShouldBe(problem);
        }
    }
}
