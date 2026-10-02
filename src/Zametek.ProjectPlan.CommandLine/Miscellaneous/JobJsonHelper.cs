using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zametek.ProjectPlan.CommandLine
{
    // How zpp serve's API is written in JSON, by the server and by zpp sending it jobs: camelCase, enums by name - in any
    // case, never by number - and the text zpp prints as it prints it, quotes and all. A response is JSON for its caller,
    // never HTML, so nothing needs escaping that JSON does not escape.
    internal static class JobJsonHelper
    {
        // The server's: what it is sent is read strictly, so that an option misspelt, or a number in quotes, is refused
        // rather than ignored.
        public static JsonSerializerOptions ServerOptions { get; } = CreateOptions(JsonUnmappedMemberHandling.Disallow);

        // zpp's, as it sends a job: what it is answered with is read leniently, so that an answer from a newer server,
        // with more in it, still reads; and what it sends leaves out the outputs it does not ask for.
        public static JsonSerializerOptions ClientOptions { get; } = CreateOptions(JsonUnmappedMemberHandling.Skip, JsonIgnoreCondition.WhenWritingNull);

        private static JsonSerializerOptions CreateOptions(
            JsonUnmappedMemberHandling unmappedMemberHandling,
            JsonIgnoreCondition ignoreCondition = JsonIgnoreCondition.Never)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                UnmappedMemberHandling = unmappedMemberHandling,
                DefaultIgnoreCondition = ignoreCondition,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };

            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
            return options;
        }
    }
}
