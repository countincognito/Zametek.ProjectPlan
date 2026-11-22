using Zametek.Common.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    // Works out whether zpp runs its job on a server, and if it does, where and with what. Anything it cannot run with is
    // refused with a UsageException, before anything is sent.
    internal static class ClientSettingsHelper
    {
        // The server zpp runs on when neither --server nor --local says otherwise.
        public const string ServerVariable = @"ZPP_SERVER";

        // The server's API key, when it is not in a file - as zpp serve reads its own.
        public const string ApiKeyVariable = ServeSettingsHelper.ApiKeyVariable;

        // The address that stands for a server on a Unix domain socket, which has none of its own.
        private static readonly Uri s_SocketAddress = new(@"http://localhost/");

        // Where zpp runs: on the server --server names, or else ZPP_SERVER does - unless --local says to run here - and
        // null when it runs here.
        public static ClientSettings? Resolve(
            Options options,
            IReadOnlyDictionary<string, string> environment)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(environment);

            string serverOption = Program.OptionLongName(nameof(Options.Server));
            string localOption = Program.OptionLongName(nameof(Options.Local));

            if (options.Local
                && options.Server is not null)
            {
                throw new UsageException(string.Format(Resource.ProjectPlan.Messages.Message_SpecifyEitherOptionNotBoth, serverOption, localOption));
            }

            if (options.ServerTimeoutMilliseconds < 0)
            {
                throw new UsageException(string.Format(
                    Resource.ProjectPlan.Messages.Message_OptionCannotBeNegative,
                    Program.OptionLongName(nameof(Options.ServerTimeoutMilliseconds))));
            }

            string? server = null;
            string source = serverOption;

            if (options.Server is string given)
            {
                server = given;
            }
            else if (!options.Local
                && environment.TryGetValue(ServerVariable, out string? variable)
                && !string.IsNullOrWhiteSpace(variable))
            {
                server = variable.Trim();
                source = ServerVariable;
            }

            if (server is null)
            {
                // An option that says how to talk to a server, with no server to talk to.
                string? serverOnly = options.ApiKeyFile is not null
                    ? nameof(Options.ApiKeyFile)
                    : options.AllowKeyOverHttp ? nameof(Options.AllowKeyOverHttp)
                    : options.ServerTimeoutMilliseconds is not null ? nameof(Options.ServerTimeoutMilliseconds) : null;

                return serverOnly is null
                    ? null
                    : throw new UsageException(string.Format(
                        Resource.ProjectPlan.Messages.Message_OptionOnlyValidWithServer,
                        Program.OptionLongName(serverOnly),
                        serverOption,
                        ServerVariable));
            }

            (Uri baseAddress, string? socket) = ParseServer(server, source);

            // zpp serve imports workbooks only, as it says; zpp says so before it sends anything, and how to import the
            // plan here instead.
            if (options.ImportFilename is string import
                && FileFormatHelper.GetProjectScenarioImportFormat(import) == ProjectScenarioImportFormat.MicrosoftProject)
            {
                throw new UsageException(source == ServerVariable
                    ? string.Format(Resource.ProjectPlan.Messages.Message_ServerFromEnvironmentCannotImport, ServerVariable, import, localOption)
                    : string.Format(Resource.ProjectPlan.Messages.Message_ServerCannotImport, import, serverOption));
            }

            string? apiKey = ResolveApiKey(options, environment);

            // The key goes in a header of each request. Over plain http, each computer on the way to the server can read
            // it. A server on this computer, or on a socket, has no way to it but this computer.
            if (apiKey is not null
                && socket is null
                && baseAddress.Scheme == Uri.UriSchemeHttp
                && !baseAddress.IsLoopback
                && !options.AllowKeyOverHttp)
            {
                throw new UsageException(string.Format(
                    Resource.ProjectPlan.Messages.Message_ServerKeyOverPlainHttp,
                    server,
                    Program.OptionLongName(nameof(Options.AllowKeyOverHttp))));
            }

            return new ClientSettings
            {
                Server = server,
                BaseAddress = baseAddress,
                UnixSocket = socket,
                ApiKey = apiKey,

                // The option gives the time limit, and 0 says that there is none. Without the option, zpp works the time
                // limit out from the limits of the server, which it asks for (see JobClient).
                RequestTimeout = options.ServerTimeoutMilliseconds is int timeout && timeout > 0
                    ? TimeSpan.FromMilliseconds(timeout)
                    : null,
                RequestTimeoutFromServer = options.ServerTimeoutMilliseconds is null,
            };
        }

        // A server as --server or ZPP_SERVER gives it: an http or https address, which a reverse proxy may have put under
        // a path of its own; or unix: and the path of the server's socket (see UnixSocketHelper), as zpp serve is given
        // the socket to listen on.
        public static (Uri BaseAddress, string? UnixSocket) ParseServer(
            string server,
            string source)
        {
            ArgumentNullException.ThrowIfNull(server);
            ArgumentNullException.ThrowIfNull(source);

            if (UnixSocketHelper.IsSocketAddress(server))
            {
                return UnixSocketHelper.GetPath(server) is string path
                    ? (s_SocketAddress, path)
                    : throw NotAServer(server, source);
            }

            if (!Uri.TryCreate(server, UriKind.Absolute, out Uri? address)
                || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps)
                || address.UserInfo.Length > 0
                || address.Query.Length > 0
                || address.Fragment.Length > 0)
            {
                throw NotAServer(server, source);
            }

            // Ending with a slash, so that the API's paths are taken from all of it.
            var builder = new UriBuilder(address);
            if (!builder.Path.EndsWith('/'))
            {
                builder.Path += @"/";
            }

            return (builder.Uri, null);
        }

        // The key from --api-key-file, or else ZPP_API_KEY, as zpp serve reads its own.
        private static string? ResolveApiKey(
            Options options,
            IReadOnlyDictionary<string, string> environment)
        {
            if (options.ApiKeyFile is string apiKeyFile)
            {
                string apiKey = File.ReadAllText(apiKeyFile).Trim();

                return apiKey.Length == 0
                    ? throw new UsageException(string.Format(Resource.ProjectPlan.Messages.Message_FileIsEmpty, apiKeyFile))
                    : apiKey;
            }

            return environment.TryGetValue(ApiKeyVariable, out string? variable)
                && !string.IsNullOrWhiteSpace(variable)
                ? variable.Trim()
                : null;
        }

        private static UsageException NotAServer(
            string server,
            string source)
        {
            return new UsageException(string.Format(Resource.ProjectPlan.Messages.Message_ServerNotValid, server, source));
        }
    }
}
