using CommandLine;

namespace Zametek.ProjectPlan.CommandLine
{
    // The options of zpp serve, which runs zpp as a server. As in Options, each long name is declared once and referenced
    // from the attributes. The limits are optional: each one given here overrides the same limit from zpp-serve.json
    // and from the environment (see ServeLimits).
    public class ServeOptions
    {
        private const string c_ListenLongName = "listen";
        private const string c_UnixSocketLongName = "unix-socket";
        private const string c_ApiKeyFileLongName = "api-key-file";
        private const string c_CertificateLongName = "certificate";
        private const string c_CertificateKeyLongName = "certificate-key";
        private const string c_CultureLongName = "culture";
        private const string c_VerboseLongName = "verbose";
        private const string c_MaxJobsLongName = "max-jobs";
        private const string c_MaxQueueLongName = "max-queue";
        private const string c_MaxUploadLongName = "max-upload";
        private const string c_MaxChartSizeLongName = "max-chart-size";
        private const string c_JobTimeoutLongName = "job-timeout";
        private const string c_MaxCompileTimeoutLongName = "max-compile-timeout";

        [Option(c_ListenLongName, HelpText = "Address to listen on: http or https, then localhost, an IP address or *, then a port - e.g. http://localhost:9770 or https://0.0.0.0:9771. Repeat it to listen on more than one (defaults to http://localhost:9770, unless --" + c_UnixSocketLongName + " is given)")]
        public IEnumerable<string> Listen { get; set; } = [];

        [Option(c_UnixSocketLongName, HelpText = "Unix domain socket to listen on, which only its owner can connect to")]
        public string? UnixSocket { get; set; } = default;

        [Option(c_ApiKeyFileLongName, HelpText = "File holding the API key that requests must carry, as Authorization: Bearer <key> - or set ZPP_API_KEY. Required to listen on anything but this machine")]
        public string? ApiKeyFile { get; set; } = default;

        [Option(c_CertificateLongName, HelpText = "Certificate for https addresses: a .pfx or .p12 file, its password in ZPP_CERTIFICATE_PASSWORD if it has one, or a .pem file")]
        public string? Certificate { get; set; } = default;

        [Option(c_CertificateKeyLongName, HelpText = "Private key of a .pem certificate, when the certificate's file does not hold it")]
        public string? CertificateKey { get; set; } = default;

        [Option(c_CultureLongName, HelpText = "Culture to write numbers and dates in, such as en-GB (defaults to the machine's)")]
        public string? Culture { get; set; } = default;

        [Option('v', c_VerboseLongName, HelpText = "Show the jobs' and the web server's informational log output on stderr")]
        public bool Verbose { get; set; } = default;



        [Option(c_MaxJobsLongName, HelpText = "Jobs to run at once (defaults to the number of processors)")]
        public int? MaxJobs { get; set; } = default;

        [Option(c_MaxQueueLongName, HelpText = "Jobs that may wait for one to finish before more are turned away (defaults to twice --" + c_MaxJobsLongName + ")")]
        public int? MaxQueue { get; set; } = default;

        [Option(c_MaxUploadLongName, HelpText = "Largest request, in megabytes (defaults to 50)")]
        public int? MaxUploadMegabytes { get; set; } = default;

        [Option(c_MaxChartSizeLongName, Min = 2, Max = 2, Separator = ':', HelpText = "Largest chart, in pixels (<width>:<height> - defaults to 5000:5000)")]
        public IEnumerable<int> MaxChartSize { get; set; } = [];

        [Option(c_JobTimeoutLongName, HelpText = "Seconds a job may run before it is stopped (defaults to 120)")]
        public int? JobTimeoutSeconds { get; set; } = default;

        [Option(c_MaxCompileTimeoutLongName, HelpText = "Most milliseconds a job may give its compilation (defaults to 60000) - a job cannot switch the limit off")]
        public int? MaxCompileTimeoutMilliseconds { get; set; } = default;
    }
}
