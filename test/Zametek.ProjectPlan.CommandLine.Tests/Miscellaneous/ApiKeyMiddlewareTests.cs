using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for zpp serve's API key: a request to its API carries it as
    /// Authorization: Bearer key, or is turned away before it goes any further;
    /// anything else - the health checks - needs none.
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
            var middleware = new ApiKeyMiddleware(_ => Task.CompletedTask, c_ApiKey);

            middleware.IsAuthorized(authorization).ShouldBe(isAuthorized);
        }

        [Fact]
        public async Task InvokeAsync_Given_ARequestToTheApiWithoutTheKey_Then_TurnsItAway()
        {
            bool passedOn = false;
            var middleware = new ApiKeyMiddleware(_ => { passedOn = true; return Task.CompletedTask; }, c_ApiKey);
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
            var middleware = new ApiKeyMiddleware(_ => { passedOn = true; return Task.CompletedTask; }, c_ApiKey);
            var context = new DefaultHttpContext();
            context.Request.Path = @"/v1/projects/compile";
            context.Request.Headers.Authorization = @"Bearer " + c_ApiKey;

            await middleware.InvokeAsync(context);

            passedOn.ShouldBeTrue();
        }

        [Theory]
        [InlineData(@"/health/live")]
        [InlineData(@"/health/ready")]
        [InlineData(@"/v1x/jobs")]
        public async Task InvokeAsync_Given_ARequestOutsideTheApi_Then_PassesItOnWithoutAKey(string path)
        {
            bool passedOn = false;
            var middleware = new ApiKeyMiddleware(_ => { passedOn = true; return Task.CompletedTask; }, c_ApiKey);
            var context = new DefaultHttpContext();
            context.Request.Path = path;

            await middleware.InvokeAsync(context);

            passedOn.ShouldBeTrue();
        }
    }
}
