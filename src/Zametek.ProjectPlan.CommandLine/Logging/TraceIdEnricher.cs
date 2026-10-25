using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;

namespace Zametek.ProjectPlan.CommandLine
{
    // Puts the id of the request being served on each log event written while it is - the trace id that its response says
    // as Request-Id and its problem as traceId - so that a line of the log can be found from the response, and every line
    // of one request from each other. The event has it as TraceId, for a program, and as TraceIdPrefix, for a person: the id
    // and a space, or nothing for an event that belongs to no request.
    internal sealed class TraceIdEnricher
        : ILogEventEnricher
    {
        public const string TraceIdProperty = @"TraceId";
        public const string PrefixProperty = @"TraceIdPrefix";

        public void Enrich(
            LogEvent logEvent,
            ILogEventPropertyFactory propertyFactory)
        {
            ArgumentNullException.ThrowIfNull(logEvent);
            ArgumentNullException.ThrowIfNull(propertyFactory);

            if (Activity.Current is { IdFormat: ActivityIdFormat.W3C } activity)
            {
                string traceId = activity.TraceId.ToHexString();

                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(TraceIdProperty, traceId));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(PrefixProperty, traceId + @" "));
            }
            else
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(PrefixProperty, string.Empty));
            }
        }
    }
}
