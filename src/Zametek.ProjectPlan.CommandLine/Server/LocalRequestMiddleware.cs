using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Zametek.ProjectPlan.CommandLine
{
    // Turns away the requests that a web page can make a browser send to a zpp serve that has no API key. Such a server
    // listens only on this computer, but a page that the user opens in a browser is on this computer too. The browser
    // sends a multipart POST to http://localhost:9770 without a question to the server first. The page cannot read the
    // answer, but the job runs, and a loop of requests keeps the server busy. With a name of its own that it points at
    // 127.0.0.1 (DNS rebinding), the page can read the answers too.
    //
    // A browser gives the origin of the page in the Origin header of each POST, and a page cannot take the header away.
    // zpp and a script send none. Thus, a request with an Origin is refused. A request through a name of the page has that
    // name as its Host, and so a Host that is not this computer is refused too. The names that --allow-host gives are the
    // exception: a proxy in front of the server sends its own name. A server with a key needs none of this, because a page
    // does not have the key. A Unix domain socket needs none either, because a browser cannot connect to one.
    internal class LocalRequestMiddleware
    {
        #region Fields

        private const string c_Localhost = @"localhost";
        private const string c_LocalhostSuffix = @".localhost";

        private readonly RequestDelegate m_Next;
        private readonly IReadOnlyList<string> m_AllowedHosts;
        private readonly ILogger<LocalRequestMiddleware> m_Logger;

        #endregion

        #region Ctors

        public LocalRequestMiddleware(
            RequestDelegate next,
            IReadOnlyList<string> allowedHosts,
            ILogger<LocalRequestMiddleware> logger)
        {
            ArgumentNullException.ThrowIfNull(next);
            ArgumentNullException.ThrowIfNull(allowedHosts);
            ArgumentNullException.ThrowIfNull(logger);
            m_Next = next;
            m_AllowedHosts = allowedHosts;
            m_Logger = logger;
        }

        #endregion

        #region Public Members

        public async Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            // A connection on a Unix domain socket has no address.
            if (context.Connection.LocalIpAddress is not null)
            {
                string? reason = null;
                string? detail = null;

                if (context.Request.Headers.Origin.Count > 0)
                {
                    reason = @"it has an Origin header";
                    detail = Resource.ProjectPlan.Messages.Message_ServeOriginNotAllowed;
                }
                else if (!IsAllowedHost(context.Request.Host))
                {
                    reason = @"its Host is not this computer";
                    detail = string.Format(
                        Resource.ProjectPlan.Messages.Message_ServeHostNotAllowed,
                        Program.OptionLongName<ServeOptions>(nameof(ServeOptions.AllowHost)));
                }

                if (detail is not null)
                {
                    // What was refused, and not the header: the path is written as the request escaped it, so that nothing
                    // in it can end a line of the log.
                    m_Logger.LogWarning(
                        "{Method} {Path}: refused, {Reason}",
                        context.Request.Method,
                        context.Request.Path.ToUriComponent(),
                        reason);

                    await ProblemHelper.ToResult(ProblemHelper.CreateForStatus(
                        context,
                        StatusCodes.Status403Forbidden,
                        detail)).ExecuteAsync(context);
                    return;
                }
            }

            await m_Next(context);
        }

        // Whether a request names this computer, or a name that the server was told to take. A request with no Host is not
        // one that a browser sends.
        public bool IsAllowedHost(HostString host)
        {
            if (!host.HasValue)
            {
                return true;
            }

            // Without the port. An IPv6 address comes in brackets.
            string name = host.Host;

            if (name.StartsWith('[')
                && name.EndsWith(']'))
            {
                name = name[1..^1];
            }

            return string.Equals(name, c_Localhost, StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(c_LocalhostSuffix, StringComparison.OrdinalIgnoreCase)
                || (IPAddress.TryParse(name, out IPAddress? address) && IPAddress.IsLoopback(address))
                || m_AllowedHosts.Contains(name, StringComparer.OrdinalIgnoreCase);
        }

        #endregion
    }
}
