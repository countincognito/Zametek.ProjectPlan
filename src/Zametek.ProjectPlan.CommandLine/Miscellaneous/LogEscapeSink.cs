using Serilog.Core;
using Serilog.Events;
using System.Globalization;
using System.Text;

namespace Zametek.ProjectPlan.CommandLine
{
    // The last step before the text log is written: it gives the sink of the log each event with the control characters in its
    // values written as escapes, so that nothing in a value can end a line of the log. Not the path of a request, which a caller
    // can give a line end as %0A and which the web server logs as it is, and not the title of a scenario in a project that a
    // caller sent. The escapes are \n and \r, and \u with four digits for any other control character - an escape of a terminal
    // among them - and for the separators of a line and of a paragraph that Unicode has. A tab is not one, and a backslash stays
    // as it is. An event with none of them goes on as it is, and so does an exception, whose lines are its own. Only the text
    // log has this sink: the JSON log writes a value as JSON does, and keeps it.
    internal sealed class LogEscapeSink
        : ILogEventSink, IDisposable
    {
        // The separators of a line and of a paragraph that Unicode has, which some programs that show a log take for line ends.
        private const char c_LineSeparator = (char)0x2028;
        private const char c_ParagraphSeparator = (char)0x2029;

        private readonly ILogEventSink m_Sink;

        public LogEscapeSink(ILogEventSink sink)
        {
            ArgumentNullException.ThrowIfNull(sink);
            m_Sink = sink;
        }

        public void Emit(LogEvent logEvent)
        {
            ArgumentNullException.ThrowIfNull(logEvent);

            Dictionary<string, LogEventPropertyValue> escaped = [];

            foreach ((string name, LogEventPropertyValue value) in logEvent.Properties)
            {
                LogEventPropertyValue safe = EscapeValue(value);

                if (!ReferenceEquals(safe, value))
                {
                    escaped[name] = safe;
                }
            }

            if (escaped.Count == 0)
            {
                m_Sink.Emit(logEvent);
                return;
            }

            // The event is the logger's, and not changed here: an event like it, with the values escaped, goes on.
            LogEventProperty[] properties = [.. logEvent.Properties.Select(x => new LogEventProperty(x.Key, escaped.GetValueOrDefault(x.Key, x.Value)))];

            m_Sink.Emit(logEvent.TraceId is { } traceId && logEvent.SpanId is { } spanId
                ? new LogEvent(logEvent.Timestamp, logEvent.Level, logEvent.Exception, logEvent.MessageTemplate, properties, traceId, spanId)
                : new LogEvent(logEvent.Timestamp, logEvent.Level, logEvent.Exception, logEvent.MessageTemplate, properties));
        }

        public void Dispose()
        {
            (m_Sink as IDisposable)?.Dispose();
        }

        // The text with each character that needs it as an escape, and the same text if none does.
        public static string EscapeText(string text)
        {
            ArgumentNullException.ThrowIfNull(text);

            int first = 0;

            while (first < text.Length
                && !NeedsEscape(text[first]))
            {
                first++;
            }

            if (first == text.Length)
            {
                return text;
            }

            var builder = new StringBuilder(text.Length + 8);
            builder.Append(text, 0, first);

            for (int i = first; i < text.Length; i++)
            {
                char character = text[i];

                if (!NeedsEscape(character))
                {
                    builder.Append(character);
                }
                else if (character == '\n')
                {
                    builder.Append(@"\n");
                }
                else if (character == '\r')
                {
                    builder.Append(@"\r");
                }
                else
                {
                    builder.Append(@"\u").Append(((int)character).ToString(@"x4", CultureInfo.InvariantCulture));
                }
            }

            return builder.ToString();
        }

        private static bool NeedsEscape(char character)
        {
            return character != '\t'
                && (char.IsControl(character) || character is c_LineSeparator or c_ParagraphSeparator);
        }

        // The value with the text in it, at any depth, escaped: the same value if there is nothing to escape in it.
        private static LogEventPropertyValue EscapeValue(LogEventPropertyValue value)
        {
            if (value is ScalarValue { Value: string text })
            {
                string escapedText = EscapeText(text);
                return ReferenceEquals(escapedText, text) ? value : new ScalarValue(escapedText);
            }

            if (value is ScalarValue { Value: char character }
                && NeedsEscape(character))
            {
                return new ScalarValue(EscapeText(character.ToString()));
            }

            if (value is SequenceValue sequence)
            {
                LogEventPropertyValue[] elements = [.. sequence.Elements.Select(EscapeValue)];

                return elements.Zip(sequence.Elements).All(x => ReferenceEquals(x.First, x.Second))
                    ? value
                    : new SequenceValue(elements);
            }

            if (value is StructureValue structure)
            {
                LogEventProperty[] properties = [.. structure.Properties.Select(x => new LogEventProperty(x.Name, EscapeValue(x.Value)))];

                return properties.Zip(structure.Properties).All(x => ReferenceEquals(x.First.Value, x.Second.Value))
                    ? value
                    : new StructureValue(properties, structure.TypeTag);
            }

            if (value is DictionaryValue dictionary)
            {
                var elements = new Dictionary<ScalarValue, LogEventPropertyValue>();
                bool isEscaped = false;

                foreach ((ScalarValue key, LogEventPropertyValue element) in dictionary.Elements)
                {
                    var safeKey = (ScalarValue)EscapeValue(key);
                    LogEventPropertyValue safeElement = EscapeValue(element);
                    isEscaped |= !ReferenceEquals(safeKey, key) || !ReferenceEquals(safeElement, element);

                    // Two keys that are alike once escaped cannot both stay: the first does.
                    elements.TryAdd(safeKey, safeElement);
                }

                return isEscaped ? new DictionaryValue(elements) : value;
            }

            return value;
        }
    }
}
