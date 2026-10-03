using System.Globalization;
using System.Security.Cryptography.X509Certificates;

namespace Zametek.ProjectPlan.CommandLine
{
    // Everything zpp serve runs with, worked out from its options, zpp-serve.json and the environment before it starts.
    internal record ServeSettings
    {
        // The addresses it listens on, each on a port of its own.
        public IReadOnlyList<ListenAddress> Listen { get; init; } = [];

        // The Unix domain sockets it listens on, each by its path in full.
        public IReadOnlyList<string> UnixSockets { get; init; } = [];

        // The key a request must carry, when it needs one.
        public string? ApiKey { get; init; }

        // The certificate for its https addresses, if it has any.
        public X509Certificate2? Certificate { get; init; }

        // The culture its jobs write numbers and dates in, when it is not the machine's.
        public CultureInfo? Culture { get; init; }

        public bool Verbose { get; init; }

        public ServeLimits Limits { get; init; } = new();
    }
}
