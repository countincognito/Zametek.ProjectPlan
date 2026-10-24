using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Shouldly;
using System.Diagnostics;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for the escapes that the text log writes in place of the control characters in a value - a line end above all - so
    /// that nothing a caller sends, in a path or in a project, can end a line of it.
    /// </summary>
    public class LogEscapeSinkTests
    {
        private sealed class CapturingSink
            : ILogEventSink, IDisposable
        {
            public List<LogEvent> Events { get; } = [];

            public bool IsDisposed { get; private set; }

            public void Emit(LogEvent logEvent)
            {
                Events.Add(logEvent);
            }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }

        private sealed class SinkThatCannotBeDisposed
            : ILogEventSink
        {
            public void Emit(LogEvent logEvent)
            {
            }
        }

        private static (Logger Logger, CapturingSink Sink) CreateLogger()
        {
            var inner = new CapturingSink();
            Logger logger = new LoggerConfiguration().WriteTo.Sink(new LogEscapeSink(inner)).CreateLogger();
            return (logger, inner);
        }

        // An event as a caller of the logger would not make it, with the values it is given.
        private static LogEvent Event(params (string Name, LogEventPropertyValue Value)[] properties)
        {
            return new LogEvent(
                DateTimeOffset.UtcNow,
                LogEventLevel.Information,
                null,
                new MessageTemplateParser().Parse(@"x"),
                properties.Select(x => new LogEventProperty(x.Name, x.Value)));
        }

        private static string Text(LogEventPropertyValue value)
        {
            return value.ShouldBeOfType<ScalarValue>().Value.ShouldBeOfType<string>();
        }

        [Theory]
        [InlineData("a\nb", @"a\nb")]
        [InlineData("a\r\nb", @"a\r\nb")]
        [InlineData("a\rb", @"a\rb")]
        [InlineData("\n", @"\n")]
        [InlineData("a\nb\nc", @"a\nb\nc")]
        public void EscapeText_Given_ALineEnd_Then_AnEscapeThatNamesIt(string text, string expected)
        {
            LogEscapeSink.EscapeText(text).ShouldBe(expected);
        }

        [Theory]
        [InlineData(0x00, "0000")]
        [InlineData(0x01, "0001")]
        [InlineData(0x08, "0008")]
        [InlineData(0x1b, "001b")]
        [InlineData(0x1f, "001f")]
        [InlineData(0x7f, "007f")]
        [InlineData(0x85, "0085")]
        [InlineData(0x9f, "009f")]
        [InlineData(0x2028, "2028")]
        [InlineData(0x2029, "2029")]
        public void EscapeText_Given_AnotherControlCharacter_Then_UAndFourDigits(int code, string digits)
        {
            LogEscapeSink.EscapeText($"a{(char)code}b").ShouldBe(@"a\u" + digits + "b");
        }

        [Theory]
        [InlineData("a\tb")]
        [InlineData(@"a\nb")]
        [InlineData(@"C:\logs\zpp")]
        [InlineData("/v1/jobs/a b")]
        [InlineData("")]
        [InlineData("a\"b'c")]
        public void EscapeText_Given_ACharacterThatIsNotOneToEscape_Then_TheTextAsItWas(string text)
        {
            LogEscapeSink.EscapeText(text).ShouldBe(text);
        }

        [Fact]
        public void EscapeText_Given_TextThatIsNotAscii_Then_TheTextAsItWas()
        {
            string text = $"Z{(char)0xfc}rich {(char)0x65e5}{(char)0x672c}";

            LogEscapeSink.EscapeText(text).ShouldBe(text);
        }

        [Fact]
        public void EscapeText_Given_ATextWithNothingToEscape_Then_TheSameText()
        {
            string text = new string('a', 12) + @" b";

            ReferenceEquals(LogEscapeSink.EscapeText(text), text).ShouldBeTrue();
        }

        [Fact]
        public void EscapeText_Given_NoText_Then_Throws()
        {
            Should.Throw<ArgumentNullException>(() => LogEscapeSink.EscapeText(null!));
        }

        [Fact]
        public void Emit_Given_AStringValue_Then_TheSinkGetsItEscaped()
        {
            (Logger logger, CapturingSink sink) = CreateLogger();

            logger.Information(@"{Method:l} {Path:l}: {StatusCode}", @"GET", "/v1/a\nWARN fake line", 400);

            LogEvent logEvent = sink.Events.ShouldHaveSingleItem();
            Text(logEvent.Properties[@"Path"]).ShouldBe(@"/v1/a\nWARN fake line");
            Text(logEvent.Properties[@"Method"]).ShouldBe(@"GET");
            logEvent.Properties[@"StatusCode"].ShouldBeOfType<ScalarValue>().Value.ShouldBe(400);
            logEvent.RenderMessage().ShouldBe(@"GET /v1/a\nWARN fake line: 400");
        }

        [Fact]
        public void Emit_Given_AValueOfTheContext_Then_ItIsEscapedToo()
        {
            (Logger logger, CapturingSink sink) = CreateLogger();

            logger.ForContext(@"RequestPath", "/v1/a\r\nb").Information(@"x");

            Text(sink.Events.ShouldHaveSingleItem().Properties[@"RequestPath"]).ShouldBe(@"/v1/a\r\nb");
        }

        [Fact]
        public void Emit_Given_ValuesInAStructureASequenceAndADictionary_Then_EachIsEscaped()
        {
            (Logger logger, CapturingSink sink) = CreateLogger();

            logger.Information(
                @"{@Request}",
                new
                {
                    Path = "/a\nb",
                    Tags = new[] { "x\ny", "z" },
                    Headers = new Dictionary<string, string> { ["k\n"] = "v\n" },
                    Size = 5,
                });

            StructureValue request = sink.Events.ShouldHaveSingleItem().Properties[@"Request"].ShouldBeOfType<StructureValue>();
            Text(request.Properties.Single(x => x.Name == @"Path").Value).ShouldBe(@"/a\nb");
            request.Properties.Single(x => x.Name == @"Tags").Value.ShouldBeOfType<SequenceValue>()
                .Elements.Select(Text).ShouldBe([@"x\ny", "z"]);
            (ScalarValue key, LogEventPropertyValue value) = request.Properties.Single(x => x.Name == @"Headers").Value.ShouldBeOfType<DictionaryValue>()
                .Elements.ShouldHaveSingleItem();
            Text(key).ShouldBe(@"k\n");
            Text(value).ShouldBe(@"v\n");
            request.Properties.Single(x => x.Name == @"Size").Value.ShouldBeOfType<ScalarValue>().Value.ShouldBe(5);
        }

        [Fact]
        public void Emit_Given_ACharacterThatNeedsAnEscape_Then_ItIsAnEscape()
        {
            (Logger logger, CapturingSink sink) = CreateLogger();

            logger.Information(@"{Character}", '\n');

            Text(sink.Events.ShouldHaveSingleItem().Properties[@"Character"]).ShouldBe(@"\n");
        }

        [Fact]
        public void Emit_Given_AnEventWithNothingToEscape_Then_TheSinkGetsTheSameEvent()
        {
            var inner = new CapturingSink();
            var sink = new LogEscapeSink(inner);
            LogEvent logEvent = Event(
                (@"Text", new ScalarValue(@"plain")),
                (@"Character", new ScalarValue('a')),
                (@"Number", new ScalarValue(5)),
                (@"Id", new ScalarValue(Guid.NewGuid())),
                (@"At", new ScalarValue(DateTimeOffset.UtcNow)),
                (@"Nothing", new ScalarValue(null)),
                (@"Sequence", new SequenceValue([new ScalarValue(@"a"), new ScalarValue(@"b")])),
                (@"Structure", new StructureValue([new LogEventProperty(@"Path", new ScalarValue(@"/a"))], @"Request")),
                (@"Dictionary", new DictionaryValue([KeyValuePair.Create(new ScalarValue(@"k"), (LogEventPropertyValue)new ScalarValue(@"v"))])));

            sink.Emit(logEvent);

            ReferenceEquals(inner.Events.ShouldHaveSingleItem(), logEvent).ShouldBeTrue();
        }

        [Fact]
        public void Emit_Given_AnEventWithAValueToEscape_Then_TheEventOfTheLoggerIsNotChanged()
        {
            var inner = new CapturingSink();
            var sink = new LogEscapeSink(inner);
            var exception = new InvalidOperationException(@"failed");
            LogEvent logEvent = new(
                DateTimeOffset.UtcNow,
                LogEventLevel.Warning,
                exception,
                new MessageTemplateParser().Parse(@"{Path}: {Other}"),
                [new LogEventProperty(@"Path", new ScalarValue("/a\nb")), new LogEventProperty(@"Other", new ScalarValue(@"plain"))]);

            sink.Emit(logEvent);

            LogEvent given = inner.Events.ShouldHaveSingleItem();
            ReferenceEquals(given, logEvent).ShouldBeFalse();
            Text(given.Properties[@"Path"]).ShouldBe(@"/a\nb");
            ReferenceEquals(given.Properties[@"Other"], logEvent.Properties[@"Other"]).ShouldBeTrue();
            Text(logEvent.Properties[@"Path"]).ShouldBe("/a\nb");
            given.Timestamp.ShouldBe(logEvent.Timestamp);
            given.Level.ShouldBe(LogEventLevel.Warning);
            given.Exception.ShouldBeSameAs(exception);
            given.MessageTemplate.ShouldBeSameAs(logEvent.MessageTemplate);
        }

        [Fact]
        public void Emit_Given_AnEventInATrace_Then_TheEventThatGoesOnIsInIt()
        {
            var inner = new CapturingSink();
            var sink = new LogEscapeSink(inner);
            ActivityTraceId traceId = ActivityTraceId.CreateRandom();
            ActivitySpanId spanId = ActivitySpanId.CreateRandom();
            var logEvent = new LogEvent(
                DateTimeOffset.UtcNow,
                LogEventLevel.Information,
                null,
                new MessageTemplateParser().Parse(@"{Path}"),
                [new LogEventProperty(@"Path", new ScalarValue("/a\nb"))],
                traceId,
                spanId);

            sink.Emit(logEvent);

            LogEvent given = inner.Events.ShouldHaveSingleItem();
            given.TraceId.ShouldBe(traceId);
            given.SpanId.ShouldBe(spanId);
        }

        [Fact]
        public void Emit_Given_AStructureWithATypeTag_Then_ItKeepsTheTag()
        {
            var inner = new CapturingSink();
            var sink = new LogEscapeSink(inner);

            sink.Emit(Event((@"Request", new StructureValue([new LogEventProperty(@"Path", new ScalarValue("/a\nb"))], @"Request"))));

            StructureValue request = inner.Events.ShouldHaveSingleItem().Properties[@"Request"].ShouldBeOfType<StructureValue>();
            request.TypeTag.ShouldBe(@"Request");
            Text(request.Properties.ShouldHaveSingleItem().Value).ShouldBe(@"/a\nb");
        }

        [Fact]
        public void Emit_Given_TwoKeysAlikeOnceEscaped_Then_TheFirstStaysAndNothingFails()
        {
            var inner = new CapturingSink();
            var sink = new LogEscapeSink(inner);

            sink.Emit(Event((@"Headers", new DictionaryValue(
            [
                KeyValuePair.Create(new ScalarValue("a\n"), (LogEventPropertyValue)new ScalarValue(@"first")),
                KeyValuePair.Create(new ScalarValue(@"a\n"), (LogEventPropertyValue)new ScalarValue(@"second")),
            ]))));

            (ScalarValue key, LogEventPropertyValue value) = inner.Events.ShouldHaveSingleItem().Properties[@"Headers"].ShouldBeOfType<DictionaryValue>().Elements.ShouldHaveSingleItem();
            Text(key).ShouldBe(@"a\n");
            Text(value).ShouldBe(@"first");
        }

        [Fact]
        public void Emit_Given_AnException_Then_ItIsTheSameExceptionWithItsOwnLines()
        {
            (Logger logger, CapturingSink sink) = CreateLogger();
            var exception = new InvalidOperationException("one\ntwo");

            logger.Error(exception, @"failed: {Reason}", "a\nb");

            LogEvent logEvent = sink.Events.ShouldHaveSingleItem();
            logEvent.Exception.ShouldBeSameAs(exception);
            logEvent.Exception.ShouldNotBeNull().Message.ShouldBe("one\ntwo");
            Text(logEvent.Properties[@"Reason"]).ShouldBe(@"a\nb");
        }

        [Fact]
        public void Emit_Given_AMessage_Then_ItsRenderingHasNoLineEnd()
        {
            (Logger logger, CapturingSink sink) = CreateLogger();

            logger.Information(@"Loading scenario {ScenarioTitle:l} ({ScenarioId:D})", "Plan\r\n[12:00:00 ERR] forged\nlines", Guid.Empty);

            string message = sink.Events.ShouldHaveSingleItem().RenderMessage();
            message.ShouldBe(@"Loading scenario Plan\r\n[12:00:00 ERR] forged\nlines (00000000-0000-0000-0000-000000000000)");
            message.ShouldNotContain('\n');
            message.ShouldNotContain('\r');
        }

        [Fact]
        public void Dispose_Given_ASinkThatCanBeDisposed_Then_DisposesIt()
        {
            var inner = new CapturingSink();

            new LogEscapeSink(inner).Dispose();

            inner.IsDisposed.ShouldBeTrue();
        }

        [Fact]
        public void Dispose_Given_ASinkThatCannotBeDisposed_Then_DoesNothing()
        {
            Should.NotThrow(() => new LogEscapeSink(new SinkThatCannotBeDisposed()).Dispose());
        }

        [Fact]
        public void New_Given_NoSink_Then_Throws()
        {
            Should.Throw<ArgumentNullException>(() => new LogEscapeSink(null!));
        }

        [Fact]
        public void Emit_Given_NoEvent_Then_Throws()
        {
            var sink = new LogEscapeSink(new CapturingSink());

            Should.Throw<ArgumentNullException>(() => sink.Emit(null!));
        }
    }
}
