using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using System.Text;

namespace Zametek.ProjectPlan.CommandLine
{
    // Turns away any request to zpp serve's API that does not carry its key, as Authorization: Bearer <key> - before the
    // request can take a job's place, or wait for one. The health checks need no key, so that a load balancer or an
    // orchestrator can ask for them.
    internal class ApiKeyMiddleware
    {
        #region Fields

        private const string c_Scheme = @"Bearer";

        private readonly RequestDelegate m_Next;
        private readonly byte[] m_KeyHash;

        #endregion

        #region Ctors

        public ApiKeyMiddleware(
            RequestDelegate next,
            string apiKey)
        {
            ArgumentNullException.ThrowIfNull(next);
            ArgumentException.ThrowIfNullOrEmpty(apiKey);
            m_Next = next;
            m_KeyHash = Hash(apiKey);
        }

        #endregion

        #region Public Members

        public async Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.Request.Path.StartsWithSegments(JobServer.ApiPath)
                && !IsAuthorized(context.Request.Headers.Authorization.ToString()))
            {
                context.Response.Headers.WWWAuthenticate = c_Scheme;
                await ProblemHelper.ToResult(ProblemHelper.CreateForStatus(
                    context,
                    StatusCodes.Status401Unauthorized,
                    Resource.ProjectPlan.Messages.Message_ServeApiKeyRequired)).ExecuteAsync(context);
                return;
            }

            await m_Next(context);
        }

        // Whether an Authorization header carries the key. The key is compared by its hash, in constant time, so that
        // how long the comparison takes says nothing about how much of a key was right.
        public bool IsAuthorized(string authorization)
        {
            ArgumentNullException.ThrowIfNull(authorization);

            // The scheme, then the key.
            int space = authorization.IndexOf(' ', StringComparison.Ordinal);
            string key = space < 0 ? string.Empty : authorization[(space + 1)..].Trim();

            return space > 0
                && string.Equals(authorization[..space], c_Scheme, StringComparison.OrdinalIgnoreCase)
                && key.Length > 0
                && CryptographicOperations.FixedTimeEquals(Hash(key), m_KeyHash);
        }

        #endregion

        #region Private Members

        private static byte[] Hash(string key)
        {
            return SHA256.HashData(Encoding.UTF8.GetBytes(key));
        }

        #endregion
    }
}
