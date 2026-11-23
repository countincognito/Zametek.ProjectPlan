namespace Zametek.ProjectPlan.CommandLine
{
    // Where zpp sends a job that runs on a server, and what it sends with it, worked out from its options and the
    // environment before anything is sent.
    internal record ClientSettings
    {
        // The server, as it was given: what zpp's messages name it by.
        public required string Server { get; init; }

        // The address the API's paths are taken from: the server's own, or one that stands for the server's socket.
        public required Uri BaseAddress { get; init; }

        // The Unix domain socket the server listens on, when it is on one.
        public string? UnixSocket { get; init; }

        // The key requests carry, when the server needs one.
        public string? ApiKey { get; init; }

        // How long zpp waits for the answer to one request, or null for no limit.
        public TimeSpan? RequestTimeout { get; init; }

        // No option gave the time limit of a request, and so it comes from the limits of the server. RequestTimeout is
        // null until zpp has them.
        public bool RequestTimeoutFromServer { get; init; }

        // The largest answer that zpp reads into memory. Without a limit, an address that sends a body with no end made zpp
        // hold as much as 2 GB (from the review of src, test and pkg). An answer of a real server can be large: a chart in
        // Bmp of 5000 by 5000 pixels is about 100 MB.
        public long MaxAnswerBytes { get; init; } = DefaultMaxAnswerBytes;

        public const long DefaultMaxAnswerBytes = 1L << 30;
    }
}
