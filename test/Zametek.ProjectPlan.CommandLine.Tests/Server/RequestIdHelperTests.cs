using Microsoft.AspNetCore.Http;
using Shouldly;
using System.Diagnostics;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for the id of a request: the trace id it carries - the one its caller gave it, or the one the server gave it -
    /// as 32 hexadecimal characters, the same each time it is asked for.
    /// </summary>
    public class RequestIdHelperTests
    {
        private const string c_HexTraceId = @"^[0-9a-f]{32}$";

        [Fact]
        public void Get_Given_ARequestInATrace_Then_ThatTraceId()
        {
            using var activity = new Activity(@"request");
            activity.SetIdFormat(ActivityIdFormat.W3C);
            activity.Start();

            RequestIdHelper.Get(new DefaultHttpContext()).ShouldBe(activity.TraceId.ToHexString());
            RequestIdHelper.Get(new DefaultHttpContext()).ShouldMatch(c_HexTraceId);
        }

        [Fact]
        public void Get_Given_ARequestNotInATrace_Then_AnIdOfItsOwnThatItKeeps()
        {
            Activity.Current.ShouldBeNull();
            var context = new DefaultHttpContext();

            string id = RequestIdHelper.Get(context);

            id.ShouldMatch(c_HexTraceId);
            RequestIdHelper.Get(context).ShouldBe(id);
        }

        [Fact]
        public void Get_Given_TwoRequestsNotInATrace_Then_AnIdForEach()
        {
            Activity.Current.ShouldBeNull();

            RequestIdHelper.Get(new DefaultHttpContext()).ShouldNotBe(RequestIdHelper.Get(new DefaultHttpContext()));
        }

        [Fact]
        public void Get_Given_AnActivityThatIsNotW3C_Then_AnIdOfItsOwnAllTheSame()
        {
            // A hierarchical id has no trace id: the request gets one, and a request that is not in a trace has 32 hex characters.
            using var activity = new Activity(@"request");
            activity.SetIdFormat(ActivityIdFormat.Hierarchical);
            activity.Start();
            var context = new DefaultHttpContext();

            string id = RequestIdHelper.Get(context);

            id.ShouldMatch(c_HexTraceId);
            RequestIdHelper.Get(context).ShouldBe(id);
        }

        [Fact]
        public void Get_Given_ANullContext_Then_Throws()
        {
            Should.Throw<ArgumentNullException>(() => RequestIdHelper.Get(null!));
        }

        private const string c_TraceId = @"4bf92f3577b34da6a3ce929d0e0e4736";
        private const string c_SpanId = @"00f067aa0ba902b7";

        [Fact]
        public void StartTrace_Given_AValidTraceparent_Then_ATraceInTheCallersTraceWhoseParentIsTheCallersSpan()
        {
            var context = new DefaultHttpContext();
            context.Request.Headers[@"traceparent"] = $@"00-{c_TraceId}-{c_SpanId}-01";
            context.Request.Headers[@"tracestate"] = @"vendor=value";

            using Activity? trace = RequestIdHelper.StartTrace(context.Request);

            trace.ShouldNotBeNull();
            Activity.Current.ShouldBeSameAs(trace);
            trace.IdFormat.ShouldBe(ActivityIdFormat.W3C);
            trace.TraceId.ToHexString().ShouldBe(c_TraceId);
            trace.ParentSpanId.ToHexString().ShouldBe(c_SpanId);
            trace.SpanId.ToHexString().ShouldNotBe(c_SpanId);
            trace.ActivityTraceFlags.ShouldBe(ActivityTraceFlags.Recorded);
            trace.TraceStateString.ShouldBe(@"vendor=value");
            RequestIdHelper.Get(context).ShouldBe(c_TraceId);
        }

        [Fact]
        public void StartTrace_Given_NoTraceparent_Then_ATraceOfItsOwn()
        {
            var context = new DefaultHttpContext();

            using Activity? trace = RequestIdHelper.StartTrace(context.Request);

            trace.ShouldNotBeNull();
            Activity.Current.ShouldBeSameAs(trace);
            trace.IdFormat.ShouldBe(ActivityIdFormat.W3C);
            trace.TraceId.ToHexString().ShouldMatch(c_HexTraceId);
            trace.ParentSpanId.ToHexString().ShouldBe(default(ActivitySpanId).ToHexString());
            RequestIdHelper.Get(context).ShouldBe(trace.TraceId.ToHexString());
        }

        [Theory]
        [InlineData(@"")]
        [InlineData(@"garbage")]
        [InlineData(@"00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7")]
        [InlineData(@"00-00000000000000000000000000000000-00f067aa0ba902b7-01")]
        [InlineData(@"00-4bf92f3577b34da6a3ce929d0e0e4736-0000000000000000-01")]
        [InlineData(@"ff-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01")]
        [InlineData(@"00-4BF92F3577B34DA6A3CE929D0E0E4736-00f067aa0ba902b7-01")]
        [InlineData(@"00-4bf92f3577b34da6a3ce929d0e0e473-00f067aa0ba902b7-01")]
        [InlineData(@"00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01-extra")]
        public void StartTrace_Given_ATraceparentThatIsNotValid_Then_ATraceOfItsOwnThatDoesNotUseIt(string traceparent)
        {
            var context = new DefaultHttpContext();
            context.Request.Headers[@"traceparent"] = traceparent;

            using Activity? trace = RequestIdHelper.StartTrace(context.Request);

            trace.ShouldNotBeNull();
            trace.TraceId.ToHexString().ShouldMatch(c_HexTraceId);
            trace.TraceId.ToHexString().ShouldNotBe(c_TraceId);
            trace.ParentSpanId.ToHexString().ShouldBe(default(ActivitySpanId).ToHexString());
        }

        [Fact]
        public void StartTrace_Given_ATraceTheHostStarted_Then_NothingToStartAndItsTraceKept()
        {
            using var host = new Activity(@"host");
            host.SetIdFormat(ActivityIdFormat.W3C);
            host.Start();
            var context = new DefaultHttpContext();
            context.Request.Headers[@"traceparent"] = $@"00-{c_TraceId}-{c_SpanId}-01";

            RequestIdHelper.StartTrace(context.Request).ShouldBeNull();

            Activity.Current.ShouldBeSameAs(host);
            RequestIdHelper.Get(context).ShouldBe(host.TraceId.ToHexString());
        }

        [Fact]
        public void StartTrace_Given_AHostTraceThatIsNotW3C_Then_ATraceOfItsOwnInTheW3CFormat()
        {
            using var host = new Activity(@"host");
            host.SetIdFormat(ActivityIdFormat.Hierarchical);
            host.Start();

            using Activity? trace = RequestIdHelper.StartTrace(new DefaultHttpContext().Request);

            trace.ShouldNotBeNull();
            trace.IdFormat.ShouldBe(ActivityIdFormat.W3C);
            trace.TraceId.ToHexString().ShouldMatch(c_HexTraceId);
        }

        [Fact]
        public void StartTrace_Given_ATraceThatEnds_Then_TheHostsTraceIsCurrentAgain()
        {
            Activity.Current.ShouldBeNull();

            using (Activity? trace = RequestIdHelper.StartTrace(new DefaultHttpContext().Request))
            {
                Activity.Current.ShouldBeSameAs(trace);
            }

            Activity.Current.ShouldBeNull();
        }

        [Fact]
        public void StartTrace_Given_NoRequest_Then_Throws()
        {
            Should.Throw<ArgumentNullException>(() => RequestIdHelper.StartTrace(null!));
        }
    }
}
