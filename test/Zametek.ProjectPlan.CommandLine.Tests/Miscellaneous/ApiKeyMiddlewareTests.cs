using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for zpp serve's API key: a request to its API carries it as
    /// Authorization: Bearer key, or is turned away before it goes any further;
    /// anything else - the health checks, and the description of the API - needs none.
    /// </summary>
    public class ApiKeyMiddlewareTests
    {
        private const string c_ApiKey = @"s3cr3t-k3y";

        [Theory]
        [InlineData(@"Bearer s3cr3t-k3y", true)]
        [InlineData(@"bearer s3cr3t-k3y", true)]
        [InlineData(@"Bearer   s3cr3t-k3y  ", true)]
        [InlineData(@"Bearer s3cr3t-k3", false)]
        [InlineData(@"Bearer s3cr3t-k3yy", false)]
        [InlineData(@"Bearer S3CR3T-K3Y", false)]
        [InlineData(@"Basic s3cr3t-k3y", false)]
        [InlineData(@"Bearer ", false)]
        [InlineData(@"Bearer", false)]
        [InlineData(@"s3cr3t-k3y", false)]
        [InlineData(@"", false)]
        public void IsAuthorized_Given_AnAuthorizationHeader_Then_WhetherItCarriesTheKey(string authorization, bool isAuthorized)
        {
            var middleware = new ApiKeyMiddleware(_ => Task.CompletedTask, c_ApiKey, NullLogger<ApiKeyMiddleware>.Instance);

            middleware.IsAuthorized(authorization).ShouldBe(isAuthorized);
        }

        [Fact]
        public async Task InvokeAsync_Given_ARequestToTheApiWithoutTheKey_Then_TurnsItAway()
        {
            bool passedOn = false;
            var middleware = new ApiKeyMiddleware(_ => { passedOn = true; return Task.CompletedTask; }, c_ApiKey, NullLogger<ApiKeyMiddleware>.Instance);
            await using ServiceProvider services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
            var context = new DefaultHttpContext { RequestServices = services };
            context.Request.Path = @"/v1/projects/compile";
            context.Response.Body = new MemoryStream();

            await middleware.InvokeAsync(context);

            passedOn.ShouldBeFalse();
            context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
            context.Response.Headers.WWWAuthenticate.ToString().ShouldBe(@"Bearer");
        }

        [Fact]
        public async Task InvokeAsync_Given_ARequestToTheApiWithTheKey_Then_PassesItOn()
        {
            bool passedOn = false;
            var middleware = new ApiKeyMiddleware(_ => { passedOn = true; return Task.CompletedTask; }, c_ApiKey, NullLogger<ApiKeyMiddleware>.Instance);
            var context = new DefaultHttpContext();
            context.Request.Path = @"/v1/projects/compile";
            context.Request.Headers.Authorization = @"Bearer " + c_ApiKey;

            await middleware.InvokeAsync(context);

            passedOn.ShouldBeTrue();
        }

        [Theory]
        [InlineData(@"", @"no API key")]
        [InlineData(@"Bearer wrong-key-wrong-key-wrong-key-xx", @"API key not accepted")]
        [InlineData(@"Basic abcdef", @"API key not accepted")]
        public async Task InvokeAsync_Given_ARefusedRequest_Then_LogsItAsAWarningWithoutTheKey(string authorization, string reason)
        {
            var logger = new RecordingLogger<ApiKeyMiddleware>();
            var middleware = new ApiKeyMiddleware(_ => Task.CompletedTask, c_ApiKey, logger);
            await using ServiceProvider services = new ServiceCollection().AddLogging().BuildServiceProvider();
            var context = new DefaultHttpContext { RequestServices = services };
            context.Request.Method = HttpMethods.Post;
            context.Request.Path = @"/v1/projects/compile";
            context.Response.Body = new MemoryStream();

            if (authorization.Length > 0)
            {
                context.Request.Headers.Authorization = authorization;
            }

            await middleware.InvokeAsync(context);

            logger.Entries.ShouldHaveSingleItem().ShouldBe((LogLevel.Warning, $@"POST /v1/projects/compile: refused, {reason}"));
        }

        [Fact]
        public async Task InvokeAsync_Given_APathThatCouldEndALineOfTheLog_Then_LogsItEscaped()
        {
            var logger = new RecordingLogger<ApiKeyMiddleware>();
            var middleware = new ApiKeyMiddleware(_ => Task.CompletedTask, c_ApiKey, logger);
            await using ServiceProvider services = new ServiceCollection().AddLogging().BuildServiceProvider();
            var context = new DefaultHttpContext { RequestServices = services };
            context.Request.Method = HttpMethods.Get;
            context.Request.Path = "/v1/a\nWARN fake line";
            context.Response.Body = new MemoryStream();

            await middleware.InvokeAsync(context);

            string message = logger.Entries.ShouldHaveSingleItem().Message;
            message.ShouldBe(@"GET /v1/a%0AWARN%20fake%20line: refused, no API key");
            message.ShouldNotContain('\n');
        }

        [Fact]
        public async Task InvokeAsync_Given_ARequestThatCarriesTheKey_Then_LogsNothing()
        {
            var logger = new RecordingLogger<ApiKeyMiddleware>();
            var middleware = new ApiKeyMiddleware(_ => Task.CompletedTask, c_ApiKey, logger);
            var context = new DefaultHttpContext();
            context.Request.Path = @"/v1/projects/compile";
            context.Request.Headers.Authorization = @"Bearer " + c_ApiKey;

            await middleware.InvokeAsync(context);

            logger.Entries.ShouldBeEmpty();
        }

        [Fact]
        public async Task InvokeAsync_Given_ARequestOutsideTheApi_Then_LogsNothing()
        {
            var logger = new RecordingLogger<ApiKeyMiddleware>();
            var middleware = new ApiKeyMiddleware(_ => Task.CompletedTask, c_ApiKey, logger);
            var context = new DefaultHttpContext();
            context.Request.Path = @"/health/ready";

            await middleware.InvokeAsync(context);

            logger.Entries.ShouldBeEmpty();
        }

        [Fact]
        public void New_Given_NoLogger_Then_Throws()
        {
            Should.Throw<ArgumentNullException>(() => new ApiKeyMiddleware(_ => Task.CompletedTask, c_ApiKey, null!));
        }

        [Theory]
        [InlineData(@"/health/live")]
        [InlineData(@"/health/ready")]
        [InlineData(@"/v1x/projects/compile")]
        public async Task InvokeAsync_Given_ARequestOutsideTheApi_Then_PassesItOnWithoutAKey(string path)
        {
            bool passedOn = false;
            var middleware = new ApiKeyMiddleware(_ => { passedOn = true; return Task.CompletedTask; }, c_ApiKey, NullLogger<ApiKeyMiddleware>.Instance);
            var context = new DefaultHttpContext();
            context.Request.Path = path;

            await middleware.InvokeAsync(context);

            passedOn.ShouldBeTrue();
        }

        [Theory]
        [InlineData(@"/v1/openapi")]
        [InlineData(@"/V1/OpenApi")]
        public async Task InvokeAsync_Given_TheDescriptionOfTheApi_Then_PassesItOnWithoutAKey(string path)
        {
            bool passedOn = false;
            var middleware = new ApiKeyMiddleware(_ => { passedOn = true; return Task.CompletedTask; }, c_ApiKey, NullLogger<ApiKeyMiddleware>.Instance);
            var context = new DefaultHttpContext();
            context.Request.Path = path;

            await middleware.InvokeAsync(context);

            passedOn.ShouldBeTrue();
        }

        [Theory]
        [InlineData(@"/v1/openapi/")]
        [InlineData(@"/v1/openapi.yaml")]
        [InlineData(@"/v1/openapix")]
        [InlineData(@"/v1/openapi/info")]
        [InlineData(@"/v1/info/openapi")]
        [InlineData(@"/v1")]
        public async Task InvokeAsync_Given_APathThatOnlyLooksLikeTheDescription_Then_TurnsItAwayWithoutAKey(string path)
        {
            bool passedOn = false;
            var middleware = new ApiKeyMiddleware(_ => { passedOn = true; return Task.CompletedTask; }, c_ApiKey, NullLogger<ApiKeyMiddleware>.Instance);
            await using ServiceProvider services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
            var context = new DefaultHttpContext { RequestServices = services };
            context.Request.Path = path;
            context.Response.Body = new MemoryStream();

            await middleware.InvokeAsync(context);

            passedOn.ShouldBeFalse();
            context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        }
    }
}
