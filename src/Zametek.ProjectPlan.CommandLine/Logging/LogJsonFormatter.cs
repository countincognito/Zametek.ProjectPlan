using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;
using Serilog.Formatting.Json;
using System.Globalization;
using System.Text.Json;

namespace Zametek.ProjectPlan.CommandLine
{
    // Writes each log event as a line of JSON, for a program to read: when it happened (UTC, as RFC 3339), its level, what it
    // says - rendered as a line of the text log is, in the invariant culture, with a text as it is and not in quotes - the id
    // of the request it belongs to, if it belongs to one, its exception, if it has one, and what else it was written with, by
    // name. Its names are in camelCase, as the API's are.
    internal sealed class LogJsonFormatter
        : ITextFormatter
    {
        private const string c_TimestampFormat = @"yyyy-MM-dd'T'HH:mm:ss.fff'Z'";


        private static readonly JsonValueFormatter s_ValueFormatter = new(typeTagName: null);

        // The message as the text log writes it: a text as it is, and not in quotes.
        private static readonly MessageTemplateTextFormatter s_MessageFormatter = new(@"{Message:lj}", CultureInfo.InvariantCulture);

        public void Format(
            LogEvent logEvent,
            TextWriter output)
        {
            ArgumentNullException.ThrowIfNull(logEvent);
            ArgumentNullException.ThrowIfNull(output);

            output.Write(@"{""timestamp"":");
            JsonValueFormatter.WriteQuotedJsonString(logEvent.Timestamp.UtcDateTime.ToString(c_TimestampFormat, CultureInfo.InvariantCulture), output);
            output.Write(@",""level"":");
            JsonValueFormatter.WriteQuotedJsonString(logEvent.Level.ToString().ToLowerInvariant(), output);
            output.Write(@",""message"":");
            using var message = new StringWriter(CultureInfo.InvariantCulture);
            s_MessageFormatter.Format(logEvent, message);
            JsonValueFormatter.WriteQuotedJsonString(message.ToString(), output);

            if (logEvent.Properties.TryGetValue(TraceIdEnricher.TraceIdProperty, out LogEventPropertyValue? traceId)
                && traceId is ScalarValue { Value: string id })
            {
                output.Write(@",""traceId"":");
                JsonValueFormatter.WriteQuotedJsonString(id, output);
            }

            if (logEvent.Exception is not null)
            {
                output.Write(@",""exception"":");
                JsonValueFormatter.WriteQuotedJsonString(logEvent.Exception.ToString(), output);
            }

            string separator = @",""properties"":{";

            foreach ((string name, LogEventPropertyValue value) in logEvent.Properties)
            {
                if (name is TraceIdEnricher.TraceIdProperty or TraceIdEnricher.PrefixProperty)
                {
                    continue;
                }

                output.Write(separator);
                JsonValueFormatter.WriteQuotedJsonString(JsonNamingPolicy.CamelCase.ConvertName(name), output);
                output.Write(':');
                s_ValueFormatter.Format(value, output);
                separator = @",";
            }

            if (separator == @",")
            {
                output.Write('}');
            }

            output.WriteLine('}');
        }
    }
}
