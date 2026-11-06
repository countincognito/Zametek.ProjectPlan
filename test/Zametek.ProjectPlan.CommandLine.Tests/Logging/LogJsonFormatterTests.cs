using Serilog.Events;
using Serilog.Parsing;
using Shouldly;
using System.Text.Json;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for the log as a program reads it: a line of JSON for each event, with its time, level, message, the id of the
    /// request it belongs to, its exception, and what else it was written with.
    /// </summary>
    public class LogJsonFormatterTests
    {
        private static readonly DateTimeOffset s_Time = new(2026, 10, 4, 14, 37, 3, 123, TimeSpan.Zero);

        private static LogEvent Event(
            string template,
            LogEventLevel level = LogEventLevel.Information,
            Exception? exception = null,
            DateTimeOffset? time = null,
            params (string Name, LogEventPropertyValue Value)[] properties)
        {
            return new LogEvent(
                time ?? s_Time,
                level,
                exception,
                new MessageTemplateParser().Parse(template),
                properties.Select(x => new LogEventProperty(x.Name, x.Value)));
        }

        private static string Format(LogEvent logEvent)
        {
            using var writer = new StringWriter();
            new LogJsonFormatter().Format(logEvent, writer);
            return writer.ToString();
        }

        private static ScalarValue Scalar(object? value)
        {
            return new ScalarValue(value);
        }

        [Fact]
        public void Format_Given_AnEventOfARequest_Then_AnObjectWithEveryMemberInOrder()
        {
            string line = Format(Event(
                @"{Method} {Path}: {StatusCode} {Problem}, after {ElapsedMilliseconds} ms",
                properties:
                [
                    (@"Method", Scalar(@"POST")),
                    (@"Path", Scalar(@"/v1/projects/compile")),
                    (@"StatusCode", Scalar(200)),
                    (@"Problem", Scalar(@"ok")),
                    (@"ElapsedMilliseconds", Scalar(27L)),
                    (TraceIdEnricher.TraceIdProperty, Scalar(@"4bf92f3577b34da6a3ce929d0e0e4736")),
                    (TraceIdEnricher.PrefixProperty, Scalar(@"4bf92f3577b34da6a3ce929d0e0e4736 ")),
                ]));

            line.ShouldBe(
                "{\"timestamp\":\"2026-10-04T14:37:03.123Z\",\"level\":\"information\",\"message\":\"POST /v1/projects/compile: 200 ok, after 27 ms\",\"traceId\":\"4bf92f3577b34da6a3ce929d0e0e4736\",\"properties\":{\"method\":\"POST\",\"path\":\"/v1/projects/compile\",\"statusCode\":200,\"problem\":\"ok\",\"elapsedMilliseconds\":27}}\n"
                    .ReplaceLineEndings(Environment.NewLine));
        }

        [Fact]
        public void Format_Given_AnEventOutsideARequestWithNothingToSay_Then_OnlyItsTimeLevelAndMessage()
        {
            Format(Event(@"Listening")).ShouldBe(
                "{\"timestamp\":\"2026-10-04T14:37:03.123Z\",\"level\":\"information\",\"message\":\"Listening\"}\n"
                    .ReplaceLineEndings(Environment.NewLine));
        }

        [Fact]
        public void Format_Given_AnException_Then_ItIsInTheLine()
        {
            var exception = new InvalidOperationException(@"It failed");

            using JsonDocument document = JsonDocument.Parse(Format(Event(@"It went wrong", LogEventLevel.Error, exception)));

            document.RootElement.GetProperty(@"level").GetString().ShouldBe(@"error");
            document.RootElement.GetProperty(@"exception").GetString().ShouldBe(exception.ToString());
        }

        [Fact]
        public void Format_Given_AnEventAtAnOtherOffset_Then_TheTimeIsUtc()
        {
            using JsonDocument document = JsonDocument.Parse(Format(Event(
                @"x",
                time: new DateTimeOffset(2026, 10, 4, 15, 37, 3, 123, TimeSpan.FromHours(1)))));

            document.RootElement.GetProperty(@"timestamp").GetString().ShouldBe(@"2026-10-04T14:37:03.123Z");
        }

        [Theory]
        [InlineData(LogEventLevel.Verbose, @"verbose")]
        [InlineData(LogEventLevel.Debug, @"debug")]
        [InlineData(LogEventLevel.Information, @"information")]
        [InlineData(LogEventLevel.Warning, @"warning")]
        [InlineData(LogEventLevel.Error, @"error")]
        [InlineData(LogEventLevel.Fatal, @"fatal")]
        public void Format_Given_AnyLevel_Then_ItsNameInLowerCase(LogEventLevel level, string expected)
        {
            using JsonDocument document = JsonDocument.Parse(Format(Event(@"x", level)));

            document.RootElement.GetProperty(@"level").GetString().ShouldBe(expected);
        }

        [Fact]
        public void Format_Given_TextThatNeedsEscaping_Then_ALineOfValidJsonAndNothingElse()
        {
            string text = "a \"quoted\" <b>bold</b> line\nand another\ttabbed & more \\ é";

            string line = Format(Event(@"{Text}", properties: (@"Text", Scalar(text))));

            line.Count(x => x == '\n').ShouldBe(1);
            line.ShouldEndWith(Environment.NewLine);
            using JsonDocument document = JsonDocument.Parse(line);
            document.RootElement.GetProperty(@"message").GetString().ShouldBe(text);
            document.RootElement.GetProperty(@"properties").GetProperty(@"text").GetString().ShouldBe(text);
        }

        [Fact]
        public void Format_Given_PropertiesOfEachKind_Then_EachIsWrittenAsItsKindOfJson()
        {
            using JsonDocument document = JsonDocument.Parse(Format(Event(
                @"{Count} {Enabled} {Nothing} {Ratio} {Items} {Shape}",
                properties:
                [
                    (@"Count", Scalar(3)),
                    (@"Enabled", Scalar(true)),
                    (@"Nothing", Scalar(null)),
                    (@"Ratio", Scalar(0.5)),
                    (@"Items", new SequenceValue([Scalar(1), Scalar(@"two")])),
                    (@"Shape", new StructureValue([new LogEventProperty(@"Width", Scalar(800))], @"Size")),
                ])));

            JsonElement properties = document.RootElement.GetProperty(@"properties");
            properties.GetProperty(@"count").GetInt32().ShouldBe(3);
            properties.GetProperty(@"enabled").GetBoolean().ShouldBeTrue();
            properties.GetProperty(@"nothing").ValueKind.ShouldBe(JsonValueKind.Null);
            properties.GetProperty(@"ratio").GetDouble().ShouldBe(0.5);
            properties.GetProperty(@"items").EnumerateArray().Count().ShouldBe(2);
            properties.GetProperty(@"shape").GetProperty(@"Width").GetInt32().ShouldBe(800);

            // The type a structure was given is not in it.
            properties.GetProperty(@"shape").EnumerateObject().Select(x => x.Name).ShouldBe([@"Width"]);
        }

        [Fact]
        public void Format_Given_ANumberAndACulture_Then_TheMessageIsInTheInvariantCulture()
        {
            using var culture = new CultureScope(@"de-DE");

            using JsonDocument document = JsonDocument.Parse(Format(Event(@"{Ratio}", properties: (@"Ratio", Scalar(1234.5)))));

            document.RootElement.GetProperty(@"message").GetString().ShouldBe(@"1234.5");
        }

        [Fact]
        public void Format_Given_NothingToFormat_Then_Throws()
        {
            var formatter = new LogJsonFormatter();

            Should.Throw<ArgumentNullException>(() => formatter.Format(null!, new StringWriter()));
            Should.Throw<ArgumentNullException>(() => formatter.Format(Event(@"x"), null!));
        }

        // The current culture, for as long as it is in scope.
        private sealed class CultureScope
            : IDisposable
        {
            private readonly System.Globalization.CultureInfo m_Previous = System.Globalization.CultureInfo.CurrentCulture;

            public CultureScope(string name)
            {
                System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(name);
            }

            public void Dispose()
            {
                System.Globalization.CultureInfo.CurrentCulture = m_Previous;
            }
        }
    }
}
