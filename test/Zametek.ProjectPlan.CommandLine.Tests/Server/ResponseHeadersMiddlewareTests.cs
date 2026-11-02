using Microsoft.AspNetCore.Http;
using Shouldly;
using System.Diagnostics;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for what the server does to every request before whatever answers it: puts it in its trace for as long as it takes -
    /// the one its caller gave it, or one of its own - and ends the trace when it is answered. The headers that every response
    /// carries are pinned through the server's responses (ProjectProblemsTests), where there is a response to start.
    /// </summary>
    public class ResponseHeadersMiddlewareTests
    {
        private const string c_TraceId = @"4bf92f3577b34da6a3ce929d0e0e4736";

        [Fact]
        public async Task InvokeAsync_Given_ARequestWithATraceparent_Then_ItIsInThatTraceWhileItIsAnsweredAndTheTraceEndsWithIt()
        {
            Activity? during = null;
            var middleware = new ResponseHeadersMiddleware(_ =>
            {
                during = Activity.Current;
                return Task.CompletedTask;
            });
            var context = new DefaultHttpContext();
            context.Request.Headers[@"traceparent"] = $@"00-{c_TraceId}-00f067aa0ba902b7-01";

            await middleware.InvokeAsync(context);

            Activity trace = during.ShouldNotBeNull();
            trace.TraceId.ToHexString().ShouldBe(c_TraceId);
            trace.IsStopped.ShouldBeTrue();
            Activity.Current.ShouldBeNull();
        }

        [Fact]
        public async Task InvokeAsync_Given_ARequestWithoutATraceparent_Then_ItIsInATraceOfItsOwnWhichEndsWithIt()
        {
            Activity? during = null;
            var middleware = new ResponseHeadersMiddleware(_ =>
            {
                during = Activity.Current;
                return Task.CompletedTask;
            });

            await middleware.InvokeAsync(new DefaultHttpContext());

            Activity trace = during.ShouldNotBeNull();
            trace.TraceId.ToHexString().ShouldMatch(@"^[0-9a-f]{32}$");
            trace.TraceId.ToHexString().ShouldNotBe(c_TraceId);
            trace.IsStopped.ShouldBeTrue();
        }

        [Fact]
        public async Task InvokeAsync_Given_ARequestThatFails_Then_TheTraceEndsAllTheSame()
        {
            Activity? during = null;
            var middleware = new ResponseHeadersMiddleware(_ =>
            {
                during = Activity.Current;
                throw new InvalidOperationException();
            });

            await Should.ThrowAsync<InvalidOperationException>(() => middleware.InvokeAsync(new DefaultHttpContext()));

            during.ShouldNotBeNull().IsStopped.ShouldBeTrue();
        }

        [Fact]
        public async Task InvokeAsync_Given_ARequestInATraceTheHostStarted_Then_ItStaysInThatTraceAndTheHostEndsIt()
        {
            using var host = new Activity(@"host");
            host.SetIdFormat(ActivityIdFormat.W3C);
            host.Start();
            Activity? during = null;
            var middleware = new ResponseHeadersMiddleware(_ =>
            {
                during = Activity.Current;
                return Task.CompletedTask;
            });

            await middleware.InvokeAsync(new DefaultHttpContext());

            during.ShouldBeSameAs(host);
            host.IsStopped.ShouldBeFalse();
        }

        [Fact]
        public void New_Given_NoNext_Then_Throws()
        {
            Should.Throw<ArgumentNullException>(() => new ResponseHeadersMiddleware(null!));
        }

        [Fact]
        public async Task InvokeAsync_Given_NoContext_Then_Throws()
        {
            var middleware = new ResponseHeadersMiddleware(_ => Task.CompletedTask);

            await Should.ThrowAsync<ArgumentNullException>(() => middleware.InvokeAsync(null!));
        }
    }
}
