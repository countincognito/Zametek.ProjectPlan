using Shouldly;
using System.Net;
using System.Net.Sockets;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for what zpp serve does as it starts, on its options as they are given to it: it refuses an address that would send
    /// the API key in the clear, unless a proxy is said to end TLS - and then says in its log that one must - and a key that is
    /// too short, and says so before it listens on anything. Each run has a time of its own, after which the server is stopped, so
    /// that a refusal that did not happen fails its test and does not hold up the run. In the same collection as
    /// ProgramExitCodeTests, because the log is written to the console's stderr, which they swap.
    /// </summary>
    [Collection(ProgramExitCodeTests.CollectionName)]
    public class JobServerStartTests
    {
        private const string c_Key = @"0123456789abcdef0123456789abcdef0123456789";

        private static readonly TimeSpan s_RunLimit = TimeSpan.FromSeconds(60);

        // A port nothing listens on.
        private static int FreePort()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static async Task<string> WriteKeyAsync(
            string directory,
            string key)
        {
            string keyFile = Path.Combine(directory, @"api-key");
            await File.WriteAllTextAsync(keyFile, key + Environment.NewLine);
            return keyFile;
        }

        // Runs zpp serve with the arguments until its health check answers on the port - or it ends - and then stops it: how it
        // ended, what it said to the console, and what its log said on stderr.
        private static async Task<(ExitCode ExitCode, RecordingJobConsole Console, string Log)> RunAsync(
            int port,
            params string[] args)
        {
            var console = new RecordingJobConsole();
            using var stopping = new CancellationTokenSource(s_RunLimit);
            using var client = new HttpClient { BaseAddress = new Uri($@"http://127.0.0.1:{port}") };
            TextWriter original = Console.Error;
            using var error = new StringWriter();
            Console.SetError(error);

            try
            {
                Task<ExitCode> running = JobServer.RunAsync(args, console, stopping.Token);

                for (int attempt = 0; attempt < 600 && !running.IsCompleted; attempt++)
                {
                    try
                    {
                        using HttpResponseMessage live = await client.GetAsync(@"/health/live", stopping.Token);

                        if (live.StatusCode == HttpStatusCode.OK)
                        {
                            break;
                        }
                    }
                    catch (HttpRequestException)
                    {
                        await Task.Delay(100, stopping.Token);
                    }
                }

                await stopping.CancelAsync();
                ExitCode exitCode = await running;
                return (exitCode, console, error.ToString());
            }
            finally
            {
                Console.SetError(original);
            }
        }

        private static string NewDirectory()
        {
            return Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $@"zpp-start-{Guid.NewGuid():N}")).FullName;
        }

        [Fact]
        public async Task Run_Given_PlainHttpThatOtherMachinesCanReachAndNoProxy_Then_UsageErrorAndNothingListens()
        {
            string directory = NewDirectory();

            try
            {
                int port = FreePort();
                string url = $@"http://0.0.0.0:{port}";
                string keyFile = await WriteKeyAsync(directory, c_Key);

                (ExitCode exitCode, RecordingJobConsole console, string log) = await RunAsync(port, @"--listen", url, @"--api-key-file", keyFile);

                exitCode.ShouldBe(ExitCode.UsageError);
                console.Calls.ShouldHaveSingleItem().Text.ShouldBe(string.Format(
                    Resource.ProjectPlan.Messages.Message_ServePlainHttpBeyondThisMachine,
                    url,
                    @"--behind-tls-proxy"));
                log.ShouldNotContain(@"Now listening");
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public async Task Run_Given_AKeyThatIsTooShort_Then_UsageErrorAndNothingListens()
        {
            string directory = NewDirectory();

            try
            {
                int port = FreePort();
                string keyFile = await WriteKeyAsync(directory, new string('k', ServeSettingsHelper.MinimumApiKeyLength - 1));

                (ExitCode exitCode, RecordingJobConsole console, string log) = await RunAsync(port, @"--listen", $@"http://127.0.0.1:{port}", @"--api-key-file", keyFile);

                exitCode.ShouldBe(ExitCode.UsageError);
                console.Calls.ShouldHaveSingleItem().Text.ShouldBe(string.Format(
                    Resource.ProjectPlan.Messages.Message_ServeApiKeyTooShort,
                    ServeSettingsHelper.MinimumApiKeyLength));
                log.ShouldNotContain(@"Now listening");
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public async Task Run_Given_PlainHttpBehindATlsProxy_Then_ListensAndWarnsOnceThatTheProxyMustEndTls()
        {
            string directory = NewDirectory();

            try
            {
                int port = FreePort();
                string url = $@"http://0.0.0.0:{port}";
                string keyFile = await WriteKeyAsync(directory, c_Key);

                (ExitCode exitCode, _, string log) = await RunAsync(port, @"--listen", url, @"--api-key-file", keyFile, @"--behind-tls-proxy");

                exitCode.ShouldBe(ExitCode.Success);
                log.ShouldContain(@"Now listening");
                string[] warnings = [.. log.Split('\n').Where(x => x.Contains(@"WRN]") && x.Contains(@"plain http"))];
                string warning = warnings.ShouldHaveSingleItem();
                warning.ShouldContain(url);
                warning.ShouldContain(@"--behind-tls-proxy");
                warning.ShouldContain(@"the proxy in front of it must end TLS");
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public async Task Run_Given_PlainHttpOnThisMachine_Then_ListensWithoutAWarning()
        {
            int port = FreePort();

            (ExitCode exitCode, _, string log) = await RunAsync(port, @"--listen", $@"http://127.0.0.1:{port}");

            exitCode.ShouldBe(ExitCode.Success);
            log.ShouldContain(@"Now listening");
            log.ShouldNotContain(@"plain http");
        }

        [Theory]
        [InlineData(LogFormat.Text)]
        [InlineData(LogFormat.Json)]
        public async Task Run_Given_ALogFormat_Then_EveryLineOfItsLogIsWrittenAsItsFormatSays(LogFormat format)
        {
            int port = FreePort();

            (ExitCode exitCode, _, string log) = await RunAsync(port, @"--listen", $@"http://127.0.0.1:{port}", @"--log-format", format.ToString());

            exitCode.ShouldBe(ExitCode.Success);
            string[] lines = [.. log.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(x => x.TrimEnd('\r'))];
            lines.ShouldNotBeEmpty();

            if (format == LogFormat.Json)
            {
                lines.ShouldAllBe(x => x.StartsWith(@"{""timestamp"":""") && x.EndsWith('}'));
            }
            else
            {
                lines.ShouldAllBe(x => System.Text.RegularExpressions.Regex.IsMatch(x, @"^\[\d\d:\d\d:\d\d [A-Z]{3}\] "));
            }
        }
    }
}
