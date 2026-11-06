using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zametek.ProjectPlan.CommandLine
{
    // How zpp serve's API is written in JSON, by the server and by zpp sending it requests: camelCase, enums by name - never
    // by number - and durations as ISO 8601, as ApiNamingPolicy and IsoDurationConverter have them, and the text zpp prints as
    // it prints it, quotes and all. Names are matched as they are written, case and all. A response is JSON for its caller,
    // never HTML, so nothing needs escaping that JSON does not escape.
    internal static class JobJsonHelper
    {
        // The server's: what it answers with is written in full - a member that has no value is null, which says the value is
        // known and is none - and what a test reads back of it is read strictly, so that a member the contract does not name
        // shows. (The server reads the options of a request itself, member by member, to report every problem in them: see
        // CompileOptionsHelper.)
        public static JsonSerializerOptions ServerOptions { get; } = CreateOptions(JsonUnmappedMemberHandling.Disallow);

        // zpp's, as it sends a request: what it is answered with is read leniently, so that an answer from a newer server,
        // with more in it, still reads; and what it sends leaves out what it was not asked to do, and what is as the server
        // would have it without being told.
        public static JsonSerializerOptions ClientOptions { get; } = CreateOptions(JsonUnmappedMemberHandling.Skip, JsonIgnoreCondition.WhenWritingDefault);

        private static JsonSerializerOptions CreateOptions(
            JsonUnmappedMemberHandling unmappedMemberHandling,
            JsonIgnoreCondition ignoreCondition = JsonIgnoreCondition.Never)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = ApiNamingPolicy.Instance,
                UnmappedMemberHandling = unmappedMemberHandling,
                DefaultIgnoreCondition = ignoreCondition,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };

            options.Converters.Add(new JsonStringEnumConverter(ApiNamingPolicy.Instance, allowIntegerValues: false));
            options.Converters.Add(new IsoDurationConverter());
            return options;
        }
    }
}
