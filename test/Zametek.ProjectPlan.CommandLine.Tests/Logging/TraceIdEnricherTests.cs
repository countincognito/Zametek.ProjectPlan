using Serilog;
using Serilog.Core;
using Serilog.Events;
using Shouldly;
using System.Diagnostics;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for the id of the request that each log event carries while it is being served: the trace id the response
    /// says, for a program, and as a prefix for a person.
    /// </summary>
    public class TraceIdEnricherTests
    {
        private sealed class CapturingSink
            : ILogEventSink
        {
            public List<LogEvent> Events { get; } = [];

            public void Emit(LogEvent logEvent)
            {
                Events.Add(logEvent);
            }
        }

        private static (Logger Logger, CapturingSink Sink) CreateLogger()
        {
            var sink = new CapturingSink();
            Logger logger = new LoggerConfiguration().Enrich.With<TraceIdEnricher>().WriteTo.Sink(sink).CreateLogger();
            return (logger, sink);
        }

        private static string Scalar(LogEvent logEvent, string name)
        {
            return logEvent.Properties[name].ShouldBeOfType<ScalarValue>().Value.ShouldBeOfType<string>();
        }

        [Fact]
        public void Enrich_Given_AnEventInARequest_Then_ItHasTheTraceIdAndThePrefixThatSaysIt()
        {
            (Logger logger, CapturingSink sink) = CreateLogger();
            using var request = new Activity(@"request");
            request.SetIdFormat(ActivityIdFormat.W3C);
            request.Start();

            logger.Information(@"x");

            LogEvent logEvent = sink.Events.ShouldHaveSingleItem();
            Scalar(logEvent, TraceIdEnricher.TraceIdProperty).ShouldBe(request.TraceId.ToHexString());
            Scalar(logEvent, TraceIdEnricher.PrefixProperty).ShouldBe(request.TraceId.ToHexString() + @" ");
        }

        [Fact]
        public void Enrich_Given_AnEventOutsideARequest_Then_NoTraceIdAndNothingToPrefix()
        {
            Activity.Current.ShouldBeNull();
            (Logger logger, CapturingSink sink) = CreateLogger();

            logger.Information(@"x");

            LogEvent logEvent = sink.Events.ShouldHaveSingleItem();
            logEvent.Properties.ContainsKey(TraceIdEnricher.TraceIdProperty).ShouldBeFalse();
            Scalar(logEvent, TraceIdEnricher.PrefixProperty).ShouldBeEmpty();
        }

        [Fact]
        public void Enrich_Given_AnActivityThatIsNotW3C_Then_NoTraceId()
        {
            (Logger logger, CapturingSink sink) = CreateLogger();
            using var request = new Activity(@"request");
            request.SetIdFormat(ActivityIdFormat.Hierarchical);
            request.Start();

            logger.Information(@"x");

            LogEvent logEvent = sink.Events.ShouldHaveSingleItem();
            logEvent.Properties.ContainsKey(TraceIdEnricher.TraceIdProperty).ShouldBeFalse();
            Scalar(logEvent, TraceIdEnricher.PrefixProperty).ShouldBeEmpty();
        }

        [Fact]
        public void Enrich_Given_AnEventThatHasATraceIdAlready_Then_ItKeepsIt()
        {
            (Logger logger, CapturingSink sink) = CreateLogger();
            using var request = new Activity(@"request");
            request.SetIdFormat(ActivityIdFormat.W3C);
            request.Start();

            logger.Information(@"{TraceId} x", @"given");

            Scalar(sink.Events.ShouldHaveSingleItem(), TraceIdEnricher.TraceIdProperty).ShouldBe(@"given");
        }

        [Fact]
        public void Enrich_Given_NothingToEnrich_Then_Throws()
        {
            var enricher = new TraceIdEnricher();

            Should.Throw<ArgumentNullException>(() => enricher.Enrich(null!, null!));
        }
    }
}
