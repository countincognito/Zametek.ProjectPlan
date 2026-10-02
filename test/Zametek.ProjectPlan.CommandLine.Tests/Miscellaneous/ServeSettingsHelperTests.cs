using Shouldly;
using System.Globalization;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for what zpp serve works out it runs with: each limit's default,
    /// overridden by zpp-serve.json, then by the ZPP_ environment variables,
    /// then by its options; where it listens, and that it listens beyond this
    /// machine only with an API key; its certificate and its culture - and
    /// that anything it cannot run with is a usage error.
    /// </summary>
    public class ServeSettingsHelperTests
    {
        private static ServeSettings Resolve(
            ServeOptions? options = null,
            string? settingsJson = null,
            Dictionary<string, string>? environment = null)
        {
            string directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $@"zpp-settings-{Guid.NewGuid():N}")).FullName;

            try
            {
                if (settingsJson is not null)
                {
                    File.WriteAllText(Path.Combine(directory, ServeSettingsHelper.SettingsFilename), settingsJson);
                }

                return ServeSettingsHelper.Resolve(
                    options ?? new ServeOptions(),
                    directory,
                    environment ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        private static string Option(string optionPropertyName)
        {
            return Program.OptionLongName<ServeOptions>(optionPropertyName);
        }

        [Fact]
        public void Resolve_Given_Nothing_Then_TheDefaults()
        {
            ServeSettings settings = Resolve();

            settings.Listen.ShouldBe([new ListenAddress(ServeSettingsHelper.DefaultListenUrl, false, @"localhost", 9770)]);
            settings.UnixSocket.ShouldBeNull();
            settings.ApiKey.ShouldBeNull();
            settings.Certificate.ShouldBeNull();
            settings.Culture.ShouldBeNull();
            settings.Limits.ShouldBe(new ServeLimits());
            settings.Limits.MaxJobs.ShouldBe(Environment.ProcessorCount);
            settings.Limits.MaxQueue.ShouldBe(2 * Environment.ProcessorCount);
            settings.Limits.MaxUploadMegabytes.ShouldBe(50);
            settings.Limits.MaxChartWidth.ShouldBe(5000);
            settings.Limits.MaxChartHeight.ShouldBe(5000);
            settings.Limits.JobTimeoutSeconds.ShouldBe(120);
            settings.Limits.MaxCompileTimeoutMilliseconds.ShouldBe(60_000);
        }

        [Fact]
        public void Resolve_Given_AUnixSocketAlone_Then_ListensThereAlone()
        {
            ServeSettings settings = Resolve(new ServeOptions { UnixSocket = @"/run/zpp.sock" });

            settings.UnixSocket.ShouldBe(@"/run/zpp.sock");
            settings.Listen.ShouldBeEmpty();
        }

        [Fact]
        public void Resolve_Given_LimitsInEachPlace_Then_OptionsBeatTheEnvironmentWhichBeatsTheFile()
        {
            ServeSettings settings = Resolve(
                new ServeOptions { MaxJobs = 6 },
                @"{ ""MaxJobs"": 2, ""MaxQueue"": 3, ""JobTimeoutSeconds"": 7 }",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [@"ZPP_MaxJobs"] = @"4",
                    [@"ZPP_MAXQUEUE"] = @"5",
                    [@"OTHER_MaxUploadMegabytes"] = @"1",
                });

            settings.Limits.MaxJobs.ShouldBe(6);
            settings.Limits.MaxQueue.ShouldBe(5);
            settings.Limits.JobTimeoutSeconds.ShouldBe(7);
            settings.Limits.MaxUploadMegabytes.ShouldBe(50);
        }

        [Fact]
        public void Resolve_Given_MaxJobsAlone_Then_TwiceAsManyMayWait()
        {
            ServeSettings settings = Resolve(new ServeOptions { MaxJobs = 3 });

            settings.Limits.MaxQueue.ShouldBe(6);
        }

        [Fact]
        public void Resolve_Given_AChartSize_Then_ItsWidthAndHeight()
        {
            ServeSettings settings = Resolve(new ServeOptions { MaxChartSize = [800, 600] });

            settings.Limits.MaxChartWidth.ShouldBe(800);
            settings.Limits.MaxChartHeight.ShouldBe(600);
        }

        [Theory]
        [InlineData(@"{ ""MaxJobs"": 0 }", nameof(ServeLimits.MaxJobs), nameof(ServeOptions.MaxJobs), 1)]
        [InlineData(@"{ ""MaxQueue"": -1 }", nameof(ServeLimits.MaxQueue), nameof(ServeOptions.MaxQueue), 0)]
        [InlineData(@"{ ""MaxUploadMegabytes"": 0 }", nameof(ServeLimits.MaxUploadMegabytes), nameof(ServeOptions.MaxUploadMegabytes), 1)]
        [InlineData(@"{ ""MaxChartWidth"": 0 }", nameof(ServeLimits.MaxChartWidth), nameof(ServeOptions.MaxChartSize), 1)]
        [InlineData(@"{ ""MaxChartHeight"": 0 }", nameof(ServeLimits.MaxChartHeight), nameof(ServeOptions.MaxChartSize), 1)]
        [InlineData(@"{ ""JobTimeoutSeconds"": 0 }", nameof(ServeLimits.JobTimeoutSeconds), nameof(ServeOptions.JobTimeoutSeconds), 1)]
        [InlineData(@"{ ""MaxCompileTimeoutMilliseconds"": 0 }", nameof(ServeLimits.MaxCompileTimeoutMilliseconds), nameof(ServeOptions.MaxCompileTimeoutMilliseconds), 1)]
        public void Resolve_Given_ALimitTooLow_Then_UsageException(
            string settingsJson,
            string limitName,
            string optionPropertyName,
            int minimum)
        {
            Should.Throw<UsageException>(() => Resolve(settingsJson: settingsJson))
                .Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeLimitTooLow, limitName, Option(optionPropertyName), minimum));
        }

        [Fact]
        public void Resolve_Given_ASettingsFileThatIsNotJson_Then_UsageException()
        {
            Should.Throw<UsageException>(() => Resolve(settingsJson: @"{ MaxJobs: "))
                .Message.ShouldContain(ServeSettingsHelper.SettingsFilename);
        }

        [Fact]
        public void Resolve_Given_ALimitThatIsNotANumber_Then_UsageException()
        {
            Should.Throw<UsageException>(() => Resolve(environment: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [@"ZPP_MaxJobs"] = @"many",
            }));
        }

        [Theory]
        [InlineData(@"http://localhost:9770", false, @"localhost", 9770)]
        [InlineData(@"https://0.0.0.0:443", true, @"0.0.0.0", 443)]
        [InlineData(@"http://127.0.0.1:0", false, @"127.0.0.1", 0)]
        [InlineData(@"http://[::1]:8080", false, @"::1", 8080)]
        [InlineData(@"http://*:80", false, @"*", 80)]
        [InlineData(@"HTTPS://LOCALHOST:9771", true, @"LOCALHOST", 9771)]
        public void ParseListenAddress_Given_AnAddress_Then_ReadsIt(
            string url,
            bool isHttps,
            string host,
            int port)
        {
            ServeSettingsHelper.ParseListenAddress(url).ShouldBe(new ListenAddress(url, isHttps, host, port));
        }

        [Theory]
        [InlineData(@"ftp://localhost:21")]
        [InlineData(@"http://example.com:80")]
        [InlineData(@"http://localhost:80/path")]
        [InlineData(@"http://unix:/tmp/zpp.sock")]
        [InlineData(@"localhost:80")]
        [InlineData(@"")]
        public void ParseListenAddress_Given_AnythingElse_Then_UsageException(string url)
        {
            Should.Throw<UsageException>(() => ServeSettingsHelper.ParseListenAddress(url))
                .Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeListenAddressNotValid, url));
        }

        [Theory]
        [InlineData(@"http://localhost:9770", true)]
        [InlineData(@"http://127.0.0.1:9770", true)]
        [InlineData(@"http://127.1.2.3:9770", true)]
        [InlineData(@"http://[::1]:9770", true)]
        [InlineData(@"http://0.0.0.0:9770", false)]
        [InlineData(@"http://[::]:9770", false)]
        [InlineData(@"http://*:9770", false)]
        [InlineData(@"http://192.168.1.10:9770", false)]
        public void IsLoopback_Given_AnAddress_Then_WhetherOnlyThisMachineCanReachIt(string url, bool isLoopback)
        {
            ServeSettingsHelper.IsLoopback(ServeSettingsHelper.ParseListenAddress(url)).ShouldBe(isLoopback);
        }

        [Fact]
        public void Resolve_Given_AnAddressOtherMachinesCanReachWithoutAKey_Then_UsageException()
        {
            const string url = @"http://0.0.0.0:9770";

            Should.Throw<UsageException>(() => Resolve(new ServeOptions { Listen = [@"http://localhost:9770", url] }))
                .Message.ShouldBe(string.Format(
                    Resource.ProjectPlan.Messages.Message_ServeNeedsApiKey,
                    url,
                    ServeSettingsHelper.ApiKeyVariable,
                    Option(nameof(ServeOptions.ApiKeyFile))));
        }

        [Fact]
        public void Resolve_Given_ThatAddressAndAKeyInTheEnvironment_Then_TheKey()
        {
            ServeSettings settings = Resolve(
                new ServeOptions { Listen = [@"http://0.0.0.0:9770"] },
                environment: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [ServeSettingsHelper.ApiKeyVariable] = @" s3cr3t ",
                });

            settings.ApiKey.ShouldBe(@"s3cr3t");
        }

        [Fact]
        public void Resolve_Given_AKeyFile_Then_ItsKeyRatherThanTheEnvironments()
        {
            string keyFile = Path.GetTempFileName();

            try
            {
                File.WriteAllText(keyFile, @"from-the-file" + Environment.NewLine);

                ServeSettings settings = Resolve(
                    new ServeOptions { ApiKeyFile = keyFile },
                    environment: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [ServeSettingsHelper.ApiKeyVariable] = @"from-the-environment",
                    });

                settings.ApiKey.ShouldBe(@"from-the-file");
            }
            finally
            {
                File.Delete(keyFile);
            }
        }

        [Fact]
        public void Resolve_Given_AnEmptyKeyFile_Then_UsageException()
        {
            string keyFile = Path.GetTempFileName();

            try
            {
                Should.Throw<UsageException>(() => Resolve(new ServeOptions { ApiKeyFile = keyFile }))
                    .Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_FileIsEmpty, keyFile));
            }
            finally
            {
                File.Delete(keyFile);
            }
        }

        [Fact]
        public void Resolve_Given_HttpsWithoutACertificate_Then_UsageException()
        {
            const string url = @"https://localhost:9771";

            Should.Throw<UsageException>(() => Resolve(new ServeOptions { Listen = [url] }))
                .Message.ShouldBe(string.Format(
                    Resource.ProjectPlan.Messages.Message_ServeHttpsNeedsCertificate,
                    url,
                    Option(nameof(ServeOptions.Certificate))));
        }

        [Fact]
        public void Resolve_Given_ACertificateWithoutHttps_Then_UsageException()
        {
            Should.Throw<UsageException>(() => Resolve(new ServeOptions { Certificate = @"server.pfx" }))
                .Message.ShouldBe(string.Format(
                    Resource.ProjectPlan.Messages.Message_ServeCertificateNeedsHttps,
                    Option(nameof(ServeOptions.Certificate))));
        }

        [Fact]
        public void Resolve_Given_ACertificateKeyWithoutACertificate_Then_UsageException()
        {
            Should.Throw<UsageException>(() => Resolve(new ServeOptions { CertificateKey = @"server.key" }))
                .Message.ShouldBe(string.Format(
                    Resource.ProjectPlan.Messages.Message_OptionRequiredWithOption,
                    Option(nameof(ServeOptions.Certificate)),
                    Option(nameof(ServeOptions.CertificateKey))));
        }

        [Fact]
        public void Resolve_Given_ACulture_Then_It()
        {
            ServeSettings settings = Resolve(new ServeOptions { Culture = @"fr-FR" });

            settings.Culture.ShouldBe(CultureInfo.GetCultureInfo(@"fr-FR"));
        }

        [Fact]
        public void Resolve_Given_ACultureThatIsNotOne_Then_UsageException()
        {
            Should.Throw<UsageException>(() => Resolve(new ServeOptions { Culture = @"xx-Nowhere" }))
                .Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_CultureNotKnown, @"xx-Nowhere"));
        }

        [Fact]
        public void GetSettingsDirectory_Then_TheDirectoryZppIsIn()
        {
            // The tests run in the test host, which is in the tests' own directory - or is the dotnet host, when the
            // directory is the tests' all the same.
            Path.TrimEndingDirectorySeparator(ServeSettingsHelper.GetSettingsDirectory())
                .ShouldBe(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory));
        }
    }
}
