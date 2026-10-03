using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace Zametek.ProjectPlan.CommandLine
{
    // Works out what zpp serve runs with, from its options, zpp-serve.json and the environment. Anything it cannot run
    // with is refused with a UsageException, before it starts.
    internal static class ServeSettingsHelper
    {
        // The file beside zpp that holds the limits it serves within, if they are not the defaults.
        public const string SettingsFilename = @"zpp-serve.json";

        // Environment variables named for a limit after this - ZPP_MaxJobs, say - set that limit.
        public const string EnvironmentPrefix = @"ZPP_";

        // The API key, when it is not in a file.
        public const string ApiKeyVariable = @"ZPP_API_KEY";

        // The password of a .pfx or .p12 certificate.
        public const string CertificatePasswordVariable = @"ZPP_CERTIFICATE_PASSWORD";

        public const string DefaultListenUrl = @"http://localhost:9770";

        private const string c_Localhost = @"localhost";

        // Hosts that stand for every address the machine has, as Kestrel reads them.
        private static readonly string[] s_AnyHosts = [@"*", @"+"];

        // The extensions of certificates read as PEM rather than as PKCS #12.
        private static readonly string[] s_PemExtensions = [@".pem", @".crt"];

        public static ServeSettings Resolve(
            ServeOptions options,
            string settingsDirectory,
            IReadOnlyDictionary<string, string> environment)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(settingsDirectory);
            ArgumentNullException.ThrowIfNull(environment);

            (IReadOnlyList<ListenAddress> listen, IReadOnlyList<string> sockets) = ResolveListen(options);
            string? apiKey = ResolveApiKey(options, environment);

            // Anything that can reach a port beyond this machine has to say who it is.
            ListenAddress? exposed = listen.FirstOrDefault(x => !IsLoopback(x));
            if (exposed is not null
                && apiKey is null)
            {
                throw new UsageException(string.Format(
                    Resource.ProjectPlan.Messages.Message_ServeNeedsApiKey,
                    exposed.Url,
                    ApiKeyVariable,
                    OptionLongName(nameof(ServeOptions.ApiKeyFile))));
            }

            return new ServeSettings
            {
                Listen = listen,
                UnixSockets = sockets,
                ApiKey = apiKey,
                Certificate = ResolveCertificate(options, listen, environment),
                Culture = ResolveCulture(options),
                Verbose = options.Verbose,
                Limits = ResolveLimits(options, settingsDirectory, environment),
            };
        }

        // The directory zpp is in, where it looks for zpp-serve.json: the executable's, unless zpp was started through
        // the dotnet host - as dotnet zpp.dll - when it is zpp.dll's. A single-file zpp unpacks itself elsewhere, so
        // AppContext.BaseDirectory is not where it is installed.
        public static string GetSettingsDirectory()
        {
            string? processPath = Environment.ProcessPath;

            return processPath is not null
                && !string.Equals(Path.GetFileNameWithoutExtension(processPath), @"dotnet", StringComparison.OrdinalIgnoreCase)
                ? Path.GetDirectoryName(processPath) ?? AppContext.BaseDirectory
                : AppContext.BaseDirectory;
        }

        // Whether only this machine can reach the address.
        public static bool IsLoopback(ListenAddress address)
        {
            ArgumentNullException.ThrowIfNull(address);

            return string.Equals(address.Host, c_Localhost, StringComparison.OrdinalIgnoreCase)
                || (IPAddress.TryParse(address.Host, out IPAddress? ipAddress) && IPAddress.IsLoopback(ipAddress));
        }

        // Whether the address stands for every address the machine has.
        public static bool IsAnyAddress(ListenAddress address)
        {
            ArgumentNullException.ThrowIfNull(address);
            return s_AnyHosts.Contains(address.Host);
        }

        // An address as --listen gives it: http or https, then localhost, an IP address, or * for every address the
        // machine has, then a port - and nothing else.
        public static ListenAddress ParseListenAddress(string url)
        {
            ArgumentNullException.ThrowIfNull(url);

            BindingAddress address;
            try
            {
                address = BindingAddress.Parse(url);
            }
            catch (FormatException)
            {
                throw new UsageException(string.Format(Resource.ProjectPlan.Messages.Message_ServeListenAddressNotValid, url));
            }

            bool isHttp = string.Equals(address.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
            bool isHttps = string.Equals(address.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

            // An IPv6 address comes in brackets.
            string host = address.Host.StartsWith('[') && address.Host.EndsWith(']')
                ? address.Host[1..^1]
                : address.Host;

            bool isKnownHost = string.Equals(host, c_Localhost, StringComparison.OrdinalIgnoreCase)
                || s_AnyHosts.Contains(host)
                || IPAddress.TryParse(host, out _);

            if (!(isHttp || isHttps)
                || !isKnownHost
                || address.IsUnixPipe
                || address.IsNamedPipe
                || !string.IsNullOrEmpty(address.PathBase)
                || address.Port is < 0 or > IPEndPoint.MaxPort)
            {
                throw new UsageException(string.Format(Resource.ProjectPlan.Messages.Message_ServeListenAddressNotValid, url));
            }

            return new ListenAddress(url, isHttps, host, address.Port);
        }

        // A .pem certificate - with its key in the same file, or in keyFile - or else a .pfx or .p12 one, with its
        // password if it has one.
        public static X509Certificate2 LoadCertificate(
            string certificateFile,
            string? keyFile,
            string? password)
        {
            ArgumentNullException.ThrowIfNull(certificateFile);

            if (keyFile is not null
                || s_PemExtensions.Contains(Path.GetExtension(certificateFile), StringComparer.OrdinalIgnoreCase))
            {
                // A key read from PEM is ephemeral, which TLS on Windows cannot use, so the certificate goes through
                // PKCS #12 - on every platform, so that it is the same certificate on each.
                using X509Certificate2 pem = X509Certificate2.CreateFromPemFile(certificateFile, keyFile);
                return X509CertificateLoader.LoadPkcs12(pem.Export(X509ContentType.Pkcs12), null);
            }

            return X509CertificateLoader.LoadPkcs12FromFile(certificateFile, password);
        }

        // What it listens on, as --listen gives it - or else only this machine, on the port zpp serve has - told apart:
        // the addresses on the network, and the Unix domain sockets, each by its path in full.
        private static (IReadOnlyList<ListenAddress> Addresses, IReadOnlyList<string> Sockets) ResolveListen(ServeOptions options)
        {
            IEnumerable<string> urls = options.Listen.Any() ? options.Listen : [DefaultListenUrl];

            var addresses = new List<ListenAddress>();
            var sockets = new List<string>();

            foreach (string url in urls)
            {
                if (!UnixSocketHelper.IsSocketAddress(url))
                {
                    addresses.Add(ParseListenAddress(url));
                    continue;
                }

                string socket = UnixSocketHelper.GetPath(url)
                    ?? throw new UsageException(string.Format(Resource.ProjectPlan.Messages.Message_ServeUnixSocketNeedsPath, url));

                // A socket can be listened on only once, however its path is written.
                if (sockets.Contains(socket))
                {
                    throw new UsageException(string.Format(Resource.ProjectPlan.Messages.Message_ServeUnixSocketListedTwice, socket));
                }

                sockets.Add(socket);
            }

            return (addresses, sockets);
        }

        private static string? ResolveApiKey(
            ServeOptions options,
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

        private static X509Certificate2? ResolveCertificate(
            ServeOptions options,
            IReadOnlyList<ListenAddress> listen,
            IReadOnlyDictionary<string, string> environment)
        {
            string certificate = OptionLongName(nameof(ServeOptions.Certificate));

            if (options.CertificateKey is not null
                && options.Certificate is null)
            {
                throw new UsageException(string.Format(
                    Resource.ProjectPlan.Messages.Message_OptionRequiredWithOption,
                    certificate,
                    OptionLongName(nameof(ServeOptions.CertificateKey))));
            }

            ListenAddress? https = listen.FirstOrDefault(x => x.IsHttps);

            if (https is null)
            {
                return options.Certificate is null
                    ? null
                    : throw new UsageException(string.Format(Resource.ProjectPlan.Messages.Message_ServeCertificateNeedsHttps, certificate));
            }

            if (options.Certificate is not string certificateFile)
            {
                throw new UsageException(string.Format(Resource.ProjectPlan.Messages.Message_ServeHttpsNeedsCertificate, https.Url, certificate));
            }

            environment.TryGetValue(CertificatePasswordVariable, out string? password);
            return LoadCertificate(certificateFile, options.CertificateKey, password);
        }

        private static CultureInfo? ResolveCulture(ServeOptions options)
        {
            if (options.Culture is not string name)
            {
                return null;
            }

            try
            {
                return CultureInfo.GetCultureInfo(name, predefinedOnly: true);
            }
            catch (CultureNotFoundException)
            {
                throw new UsageException(string.Format(Resource.ProjectPlan.Messages.Message_CultureNotKnown, name));
            }
        }

        // Each limit's default, overridden by zpp-serve.json, then by the environment, then by the options.
        private static ServeLimits ResolveLimits(
            ServeOptions options,
            string settingsDirectory,
            IReadOnlyDictionary<string, string> environment)
        {
            string settingsFile = Path.Combine(settingsDirectory, SettingsFilename);
            IConfigurationRoot configuration;
            var limits = new ServeLimits();

            try
            {
                // Read once, when the server starts: watching it for changes is what makes the generic host slow to
                // start on Linux.
                configuration = new ConfigurationBuilder()
                    .AddJsonFile(settingsFile, optional: true, reloadOnChange: false)
                    .AddInMemoryCollection(FromEnvironment(environment))
                    .AddInMemoryCollection(FromOptions(options))
                    .Build();

                configuration.Bind(limits);
            }
            catch (Exception ex) when (ex is InvalidDataException or FormatException or InvalidOperationException)
            {
                // A file that cannot be read says why in the exception it wraps.
                string reason = ex is InvalidDataException { InnerException: Exception inner } ? inner.Message : ex.Message;

                throw new UsageException(string.Format(
                    Resource.ProjectPlan.Messages.Message_ServeSettingsNotValid,
                    settingsFile,
                    EnvironmentPrefix,
                    reason));
            }

            if (configuration[nameof(ServeLimits.MaxQueue)] is null)
            {
                limits = limits with { MaxQueue = 2 * limits.MaxJobs };
            }

            RequireAtLeast(limits.MaxJobs, 1, nameof(ServeLimits.MaxJobs), nameof(ServeOptions.MaxJobs));
            RequireAtLeast(limits.MaxQueue, 0, nameof(ServeLimits.MaxQueue), nameof(ServeOptions.MaxQueue));
            RequireAtLeast(limits.MaxUploadMegabytes, 1, nameof(ServeLimits.MaxUploadMegabytes), nameof(ServeOptions.MaxUploadMegabytes));
            RequireAtLeast(limits.MaxChartWidth, 1, nameof(ServeLimits.MaxChartWidth), nameof(ServeOptions.MaxChartSize));
            RequireAtLeast(limits.MaxChartHeight, 1, nameof(ServeLimits.MaxChartHeight), nameof(ServeOptions.MaxChartSize));
            RequireAtLeast(limits.JobTimeoutSeconds, 1, nameof(ServeLimits.JobTimeoutSeconds), nameof(ServeOptions.JobTimeoutSeconds));
            RequireAtLeast(limits.MaxCompileTimeoutMilliseconds, 1, nameof(ServeLimits.MaxCompileTimeoutMilliseconds), nameof(ServeOptions.MaxCompileTimeoutMilliseconds));

            return limits;
        }

        // The environment variables named for a setting after ZPP_, as configuration: ZPP_MaxJobs is MaxJobs. As in
        // .NET's own, a double underscore separates the levels of a setting's name.
        private static Dictionary<string, string?> FromEnvironment(IReadOnlyDictionary<string, string> environment)
        {
            return environment
                .Where(x => x.Key.StartsWith(EnvironmentPrefix, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(
                    x => x.Key[EnvironmentPrefix.Length..].Replace(@"__", ConfigurationPath.KeyDelimiter, StringComparison.Ordinal),
                    x => (string?)x.Value,
                    StringComparer.OrdinalIgnoreCase);
        }

        // The limits the options give, as configuration.
        private static Dictionary<string, string?> FromOptions(ServeOptions options)
        {
            var values = new Dictionary<string, string?>();

            Add(values, nameof(ServeLimits.MaxJobs), options.MaxJobs);
            Add(values, nameof(ServeLimits.MaxQueue), options.MaxQueue);
            Add(values, nameof(ServeLimits.MaxUploadMegabytes), options.MaxUploadMegabytes);
            Add(values, nameof(ServeLimits.JobTimeoutSeconds), options.JobTimeoutSeconds);
            Add(values, nameof(ServeLimits.MaxCompileTimeoutMilliseconds), options.MaxCompileTimeoutMilliseconds);

            // The parser has checked that a size it was given has exactly two values.
            if (options.MaxChartSize.Any())
            {
                IList<int> size = [.. options.MaxChartSize];
                Add(values, nameof(ServeLimits.MaxChartWidth), size[0]);
                Add(values, nameof(ServeLimits.MaxChartHeight), size[1]);
            }

            return values;
        }

        private static void Add(
            Dictionary<string, string?> values,
            string key,
            int? value)
        {
            if (value is int given)
            {
                values.Add(key, given.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static void RequireAtLeast(
            int value,
            int minimum,
            string limitName,
            string optionPropertyName)
        {
            if (value < minimum)
            {
                throw new UsageException(string.Format(
                    Resource.ProjectPlan.Messages.Message_ServeLimitTooLow,
                    limitName,
                    OptionLongName(optionPropertyName),
                    minimum));
            }
        }

        private static string OptionLongName(string optionPropertyName)
        {
            return Program.OptionLongName<ServeOptions>(optionPropertyName);
        }
    }
}
