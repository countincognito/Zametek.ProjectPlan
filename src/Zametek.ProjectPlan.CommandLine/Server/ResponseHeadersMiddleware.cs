using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using System.Diagnostics;

namespace Zametek.ProjectPlan.CommandLine
{
    // Puts the request in its trace - the one its caller gave it in a traceparent, or one of its own - and on every response
    // puts what none should be without, whatever made it - an endpoint, the key check, the router: the request's id, which
    // is its trace id, as Request-Id; X-Content-Type-Options: nosniff; and Cache-Control: no-store - what the API answers
    // with is made of somebody's project, and a probe's answer is of its moment - unless the answer says otherwise.
    internal class ResponseHeadersMiddleware
    {
        #region Fields

        public const string RequestIdHeader = @"Request-Id";

        private const string c_NoSniff = @"nosniff";
        private const string c_NoStore = @"no-store";

        private readonly RequestDelegate m_Next;

        #endregion

        #region Ctors

        public ResponseHeadersMiddleware(RequestDelegate next)
        {
            ArgumentNullException.ThrowIfNull(next);
            m_Next = next;
        }

        #endregion

        #region Public Members

        public async Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            // The trace lasts as long as the request does.
            using Activity? trace = RequestIdHelper.StartTrace(context.Request);

            // When the response starts, so that an answer that sets its own Cache-Control has done so by then.
            context.Response.OnStarting(
                static state =>
                {
                    var httpContext = (HttpContext)state;
                    IHeaderDictionary headers = httpContext.Response.Headers;

                    headers[RequestIdHeader] = RequestIdHelper.Get(httpContext);
                    headers.XContentTypeOptions = c_NoSniff;

                    if (!headers.ContainsKey(HeaderNames.CacheControl))
                    {
                        headers.CacheControl = c_NoStore;
                    }

                    return Task.CompletedTask;
                },
                context);

            await m_Next(context);
        }

        #endregion
    }
}
