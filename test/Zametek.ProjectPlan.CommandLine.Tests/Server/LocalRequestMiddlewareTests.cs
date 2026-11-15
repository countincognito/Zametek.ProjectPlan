using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using System.Net;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for what a zpp serve with no API key takes: a request for this computer that does not come from a web page.
    /// A request with an Origin header comes from a page in a browser, and a request whose Host is not this computer can
    /// come from a page through a name of its own. Both are turned away with 403, before they can take the place of a job.
    /// </summary>
    public class LocalRequestMiddlewareTests
    {
        private static LocalRequestMiddleware Middleware(RequestDelegate? next = null, params string[] allowedHosts) =>
            new(next ?? (_ => Task.CompletedTask), allowedHosts, NullLogger<LocalRequestMiddleware>.Instance);

        [Theory]
        [InlineData(@"localhost")]
        [InlineData(@"localhost:9770")]
        [InlineData(@"LOCALHOST:9770")]
        [InlineData(@"zpp.localhost:9770")]
        [InlineData(@"127.0.0.1:9770")]
        [InlineData(@"127.0.0.2")]
        [InlineData(@"[::1]:9770")]
        [InlineData(@"")]
        public void IsAllowedHost_Given_ThisComputer_Then_True(string host)
        {
            Middleware().IsAllowedHost(new HostString(host)).ShouldBeTrue();
        }

        [Theory]
        [InlineData(@"zpp.example.com")]
        [InlineData(@"zpp.example.com:9770")]
        [InlineData(@"localhost.example.com")]
        [InlineData(@"notlocalhost")]
        [InlineData(@"10.0.0.5:9770")]
        [InlineData(@"192.168.1.20")]
        [InlineData(@"[2001:db8::1]:9770")]
        public void IsAllowedHost_Given_AnotherName_Then_False(string host)
        {
            Middleware().IsAllowedHost(new HostString(host)).ShouldBeFalse();
        }

        [Theory]
        [InlineData(@"plan.example.com")]
        [InlineData(@"plan.example.com:443")]
        [InlineData(@"PLAN.EXAMPLE.COM")]
        public void IsAllowedHost_Given_ANameThatTheServerWasToldToTake_Then_True(string host)
        {
            LocalRequestMiddleware middleware = Middleware(null, @"plan.example.com");

            middleware.IsAllowedHost(new HostString(host)).ShouldBeTrue();
            middleware.IsAllowedHost(new HostString(@"other.example.com")).ShouldBeFalse();
        }

        [Theory]
        [InlineData(@"http://evil.example.com")]
        [InlineData(@"http://localhost:9770")]
        [InlineData(@"null")]
        public async Task InvokeAsync_Given_ARequestWithAnOrigin_Then_TurnsItAway(string origin)
        {
            bool passedOn = false;
            LocalRequestMiddleware middleware = Middleware(_ => { passedOn = true; return Task.CompletedTask; });
            await using ServiceProvider services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
            DefaultHttpContext context = Request(services, @"localhost:9770");
            context.Request.Headers.Origin = origin;

            await middleware.InvokeAsync(context);

            passedOn.ShouldBeFalse();
            context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        }

        [Fact]
        public async Task InvokeAsync_Given_ARequestForAnotherName_Then_TurnsItAway()
        {
            bool passedOn = false;
            LocalRequestMiddleware middleware = Middleware(_ => { passedOn = true; return Task.CompletedTask; });
            await using ServiceProvider services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
            DefaultHttpContext context = Request(services, @"rebound.example.com:9770");

            await middleware.InvokeAsync(context);

            passedOn.ShouldBeFalse();
            context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        }

        [Fact]
        public async Task InvokeAsync_Given_ARequestForThisComputerWithNoOrigin_Then_PassesItOn()
        {
            bool passedOn = false;
            LocalRequestMiddleware middleware = Middleware(_ => { passedOn = true; return Task.CompletedTask; });
            await using ServiceProvider services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
            DefaultHttpContext context = Request(services, @"localhost:9770");

            await middleware.InvokeAsync(context);

            passedOn.ShouldBeTrue();
        }

        [Fact]
        public async Task InvokeAsync_Given_ARequestOnASocket_Then_PassesItOnWhateverItNames()
        {
            // A connection on a Unix domain socket has no address, and a browser cannot make one.
            bool passedOn = false;
            LocalRequestMiddleware middleware = Middleware(_ => { passedOn = true; return Task.CompletedTask; });
            await using ServiceProvider services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
            DefaultHttpContext context = Request(services, @"anything.example.com");
            context.Connection.LocalIpAddress = null;
            context.Request.Headers.Origin = @"http://evil.example.com";

            await middleware.InvokeAsync(context);

            passedOn.ShouldBeTrue();
        }

        [Theory]
        [InlineData(@"http://evil.example.com", @"localhost:9770", @"it has an Origin header")]
        [InlineData(null, @"rebound.example.com", @"its Host is not this computer")]
        public async Task InvokeAsync_Given_ARefusedRequest_Then_LogsItAsAWarningWithoutTheHeader(string? origin, string host, string reason)
        {
            var logger = new RecordingLogger<LocalRequestMiddleware>();
            var middleware = new LocalRequestMiddleware(_ => Task.CompletedTask, [], logger);
            await using ServiceProvider services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
            DefaultHttpContext context = Request(services, host);

            if (origin is not null)
            {
                context.Request.Headers.Origin = origin;
            }

            await middleware.InvokeAsync(context);

            (LogLevel level, string message) = logger.Entries.ShouldHaveSingleItem();
            level.ShouldBe(LogLevel.Warning);
            message.ShouldBe(@"POST /v1/projects/compile: refused, " + reason);
        }

        // A POST of a job that came in on a port of this computer.
        private static DefaultHttpContext Request(ServiceProvider services, string host)
        {
            var context = new DefaultHttpContext { RequestServices = services };
            context.Connection.LocalIpAddress = IPAddress.Loopback;
            context.Request.Method = HttpMethods.Post;
            context.Request.Path = @"/v1/projects/compile";
            context.Request.Host = new HostString(host);
            context.Response.Body = new MemoryStream();
            return context;
        }
    }
}
