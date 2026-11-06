using Microsoft.AspNetCore.Http;
using System.Diagnostics;

namespace Zametek.ProjectPlan.CommandLine
{
    // The id of a request: the trace id it carries - the one its caller gave it, in a traceparent, or the one the server
    // gave it - as 32 hexadecimal characters. A response says it as its Request-Id, a problem as its traceId, and the
    // server's log names the request by it.
    internal static class RequestIdHelper
    {
        private const string c_ItemKey = @"Zametek.ProjectPlan.RequestId";
        private const string c_ActivityName = @"Zametek.ProjectPlan.Request";
        private const string c_TraceParentHeader = @"traceparent";
        private const string c_TraceStateHeader = @"tracestate";

        // Starts the trace the request is in, for as long as it takes - dispose what it returns when the request is done -
        // unless the host has started one already, when there is nothing to start and it returns null. The trace is the one
        // the caller gave the request in a valid traceparent, whose span is the parent of the request's; or, if the request
        // has none or it is not valid, a trace of its own.
        public static Activity? StartTrace(HttpRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (Activity.Current is { IdFormat: ActivityIdFormat.W3C })
            {
                return null;
            }

            var activity = new Activity(c_ActivityName);
            activity.SetIdFormat(ActivityIdFormat.W3C);

            if (ActivityContext.TryParse(request.Headers[c_TraceParentHeader], request.Headers[c_TraceStateHeader], isRemote: true, out ActivityContext parent))
            {
                activity.SetParentId(parent.TraceId, parent.SpanId, parent.TraceFlags);
                activity.TraceStateString = parent.TraceState;
            }

            return activity.Start();
        }

        public static string Get(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (Activity.Current is Activity activity
                && activity.IdFormat == ActivityIdFormat.W3C)
            {
                return activity.TraceId.ToHexString();
            }

            // A request that is not being traced is given an id of its own, which it keeps.
            if (context.Items.TryGetValue(c_ItemKey, out object? given)
                && given is string id)
            {
                return id;
            }

            string created = ActivityTraceId.CreateRandom().ToHexString();
            context.Items[c_ItemKey] = created;
            return created;
        }
    }
}
