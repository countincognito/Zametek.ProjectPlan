using Shouldly;
using System.Globalization;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for what zpp serve works out it runs with: each limit's default,
    /// overridden by zpp-serve.json, then by the ZPP_ environment variables,
    /// then by its options; where it listens - its sockets' paths in full - and
    /// that it listens beyond this machine only with an API key; its
    /// certificate and its culture - and that anything it cannot run with is
    /// a usage error.
    /// </summary>
    public class ServeSettingsHelperTests
    {
        // A key of the shortest length a server takes, and another of its length.
        private const string c_Key = @"0123456789abcdef0123456789abcdef";
        private const string c_OtherKey = @"fedcba9876543210fedcba9876543210";

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
            settings.UnixSockets.ShouldBeEmpty();
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

        private static string SocketPath(string name = @"zpp.sock")
        {
            return Path.Combine(Path.GetTempPath(), name);
        }

        [Fact]
        public void Resolve_Given_AUnixSocketAlone_Then_ListensThereAlone()
        {
            string socket = SocketPath();

            ServeSettings settings = Resolve(new ServeOptions { Listen = [$@"unix:{socket}"] });

            // Only a port beyond this machine needs a key.
            settings.UnixSockets.ShouldBe([socket]);
            settings.Listen.ShouldBeEmpty();
            settings.ApiKey.ShouldBeNull();
        }

        [Fact]
        public void Resolve_Given_AUnixSocketAndAnAddress_Then_ListensOnBoth()
        {
            string socket = SocketPath();

            ServeSettings settings = Resolve(new ServeOptions { Listen = [@"http://localhost:9771", $@"unix:{socket}"] });

            settings.Listen.ShouldBe([new ListenAddress(@"http://localhost:9771", false, @"localhost", 9771)]);
            settings.UnixSockets.ShouldBe([socket]);
        }

        [Fact]
        public void Resolve_Given_TwoUnixSockets_Then_ListensOnBothInTheOrderGiven()
        {
            string first = SocketPath(@"first.sock");
            string second = SocketPath(@"second.sock");

            ServeSettings settings = Resolve(new ServeOptions { Listen = [$@"unix:{second}", $@"unix:{first}"] });

            settings.UnixSockets.ShouldBe([second, first]);
        }

        [Fact]
        public void Resolve_Given_ARelativeUnixSocket_Then_ItsPathInFull()
        {
            // Kestrel listens only on a socket whose path is absolute. The current directory is where the tests are
            // run, which may be too deep for a socket in it, so the path goes up to its root.
            ServeSettings settings = Resolve(new ServeOptions { Listen = [$@"unix:{UnixSocketHelperTests.UpToTheRoot}zpp.sock"] });

            settings.UnixSockets.ShouldBe([Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory)!, @"zpp.sock")]);
        }

        [Theory]
        [InlineData(@"unix:")]
        [InlineData(@"unix://")]
        [InlineData(@"unix:   ")]
        public void Resolve_Given_AUnixSocketWithoutAPath_Then_UsageException(string address)
        {
            Should.Throw<UsageException>(() => Resolve(new ServeOptions { Listen = [@"http://localhost:9771", address] }))
                .Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeUnixSocketNeedsPath, address));
        }

        [Fact]
        public void Resolve_Given_TheSameUnixSocketTwice_Then_UsageException()
        {
            // However its path is written: it can be listened on only once.
            string socket = SocketPath();

            Should.Throw<UsageException>(() => Resolve(new ServeOptions { Listen = [$@"unix:{socket}", $@"unix://{socket}"] }))
                .Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeUnixSocketListedTwice, socket));
        }

        [Fact]
        public void Resolve_Given_AUnixSocketWhosePathIsTooLong_Then_UsageException()
        {
            string socket = SocketPath(new string('x', 300) + @".sock");

            Should.Throw<UsageException>(() => Resolve(new ServeOptions { Listen = [$@"unix:{socket}"] }))
                .Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_UnixSocketPathTooLong, socket));
        }

        [Fact]
        public void Resolve_Given_AUnixSocketAndAnAddressOtherMachinesCanReachWithoutAKey_Then_UsageException()
        {
            // The socket does not make the address any safer.
            const string url = @"http://0.0.0.0:9770";

            Should.Throw<UsageException>(() => Resolve(new ServeOptions { Listen = [$@"unix:{SocketPath()}", url] }))
                .Message.ShouldBe(string.Format(
                    Resource.ProjectPlan.Messages.Message_ServeNeedsApiKey,
                    url,
                    ServeSettingsHelper.ApiKeyVariable,
                    Option(nameof(ServeOptions.ApiKeyFile))));
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
                new ServeOptions { Listen = [@"http://0.0.0.0:9770"], BehindTlsProxy = true },
                environment: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [ServeSettingsHelper.ApiKeyVariable] = $@" {c_Key} ",
                });

            settings.ApiKey.ShouldBe(c_Key);
        }

        [Fact]
        public void Resolve_Given_AKeyFile_Then_ItsKeyRatherThanTheEnvironments()
        {
            string keyFile = Path.GetTempFileName();

            try
            {
                File.WriteAllText(keyFile, c_Key + Environment.NewLine);

                ServeSettings settings = Resolve(
                    new ServeOptions { ApiKeyFile = keyFile },
                    environment: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [ServeSettingsHelper.ApiKeyVariable] = c_OtherKey,
                    });

                settings.ApiKey.ShouldBe(c_Key);
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

        [Theory]
        [InlineData(31, false)]
        [InlineData(32, true)]
        [InlineData(33, true)]
        [InlineData(1, false)]
        public void Resolve_Given_AKeyInTheEnvironment_Then_RefusedWhenShorterThanTheFloor(int length, bool accepted)
        {
            string key = new('k', length);
            var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [ServeSettingsHelper.ApiKeyVariable] = key };

            if (accepted)
            {
                Resolve(environment: environment).ApiKey.ShouldBe(key);
            }
            else
            {
                Should.Throw<UsageException>(() => Resolve(environment: environment))
                    .Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeApiKeyTooShort, ServeSettingsHelper.MinimumApiKeyLength));
            }
        }

        [Theory]
        [InlineData(31, false)]
        [InlineData(32, true)]
        public void Resolve_Given_AKeyInAFile_Then_RefusedWhenShorterThanTheFloor(int length, bool accepted)
        {
            string keyFile = Path.GetTempFileName();

            try
            {
                string key = new('k', length);
                File.WriteAllText(keyFile, key + Environment.NewLine);

                if (accepted)
                {
                    Resolve(new ServeOptions { ApiKeyFile = keyFile }).ApiKey.ShouldBe(key);
                }
                else
                {
                    Should.Throw<UsageException>(() => Resolve(new ServeOptions { ApiKeyFile = keyFile }))
                        .Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeApiKeyTooShort, ServeSettingsHelper.MinimumApiKeyLength));
                }
            }
            finally
            {
                File.Delete(keyFile);
            }
        }

        [Fact]
        public void MinimumApiKeyLength_Then_ThirtyTwoCharacters()
        {
            ServeSettingsHelper.MinimumApiKeyLength.ShouldBe(32);
        }

        [Theory]
        [InlineData(@"http://0.0.0.0:9770")]
        [InlineData(@"http://*:9770")]
        [InlineData(@"http://[::]:9770")]
        [InlineData(@"http://192.168.1.10:9770")]
        public void Resolve_Given_PlainHttpThatOtherMachinesCanReach_Then_UsageExceptionUnlessAProxyEndsTls(string url)
        {
            var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [ServeSettingsHelper.ApiKeyVariable] = c_Key };

            Should.Throw<UsageException>(() => Resolve(new ServeOptions { Listen = [url] }, environment: environment))
                .Message.ShouldBe(string.Format(
                    Resource.ProjectPlan.Messages.Message_ServePlainHttpBeyondThisMachine,
                    url,
                    Option(nameof(ServeOptions.BehindTlsProxy))));

            ServeSettings settings = Resolve(new ServeOptions { Listen = [url], BehindTlsProxy = true }, environment: environment);

            settings.BehindTlsProxy.ShouldBeTrue();
            settings.Listen.ShouldHaveSingleItem().Url.ShouldBe(url);
        }

        [Fact]
        public void Resolve_Given_PlainHttpThatOtherMachinesCanReachWithNoKeyAndBehindAProxy_Then_StillNeedsAKey()
        {
            const string url = @"http://0.0.0.0:9770";

            Should.Throw<UsageException>(() => Resolve(new ServeOptions { Listen = [url], BehindTlsProxy = true }))
                .Message.ShouldBe(string.Format(
                    Resource.ProjectPlan.Messages.Message_ServeNeedsApiKey,
                    url,
                    ServeSettingsHelper.ApiKeyVariable,
                    Option(nameof(ServeOptions.ApiKeyFile))));
        }

        [Theory]
        [InlineData(@"http://localhost:9770")]
        [InlineData(@"http://127.0.0.1:9770")]
        [InlineData(@"http://[::1]:9770")]
        public void Resolve_Given_PlainHttpOnThisMachine_Then_NeedsNothingToSayThatTlsEndsInFront(string url)
        {
            ServeSettings settings = Resolve(new ServeOptions { Listen = [url] });

            settings.BehindTlsProxy.ShouldBeFalse();
            settings.Listen.ShouldHaveSingleItem().Url.ShouldBe(url);
        }

        [Fact]
        public void Resolve_Given_BehindATlsProxyAndNothingBeyondThisMachine_Then_UsageException()
        {
            // It has nothing to say that is true of the address it is given, or of none.
            Should.Throw<UsageException>(() => Resolve(new ServeOptions { BehindTlsProxy = true }))
                .Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeBehindTlsProxyNeedsHttp, Option(nameof(ServeOptions.BehindTlsProxy))));
            Should.Throw<UsageException>(() => Resolve(new ServeOptions { BehindTlsProxy = true, Listen = [$@"unix:{SocketPath()}"] }))
                .Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeBehindTlsProxyNeedsHttp, Option(nameof(ServeOptions.BehindTlsProxy))));
        }

        [Fact]
        public void Resolve_Given_PlainHttpBesideHttpsBeyondThisMachine_Then_UsageException()
        {
            // A client could use the one that is not protected.
            var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [ServeSettingsHelper.ApiKeyVariable] = c_Key };

            Should.Throw<UsageException>(() => Resolve(
                new ServeOptions { Listen = [@"http://0.0.0.0:9770", @"https://0.0.0.0:9771"], BehindTlsProxy = true },
                environment: environment))
                .Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServePlainHttpBesideTls, @"http://0.0.0.0:9770", @"https://0.0.0.0:9771"));
        }

        [Fact]
        public void Resolve_Given_PlainHttpBeyondThisMachineBesideHttpsOnThisMachine_Then_ListensOnBoth()
        {
            // Only the proxy reaches the one, and only this machine the other: nothing is served in the clear beside what is served
            // over TLS to the same clients.
            string directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $@"zpp-settings-{Guid.NewGuid():N}")).FullName;

            try
            {
                // Written to its file, which is what the options name.
                TestCertificates.CreateSelfSigned(directory).Dispose();

                ServeSettings settings = Resolve(
                    new ServeOptions
                    {
                        Listen = [@"http://0.0.0.0:9770", @"https://localhost:9771"],
                        BehindTlsProxy = true,
                        Certificate = Path.Combine(directory, @"server.pfx"),
                    },
                    environment: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [ServeSettingsHelper.ApiKeyVariable] = c_Key,
                        [ServeSettingsHelper.CertificatePasswordVariable] = @"p4ssw0rd",
                    });

                settings.Listen.Select(x => x.Url).ShouldBe([@"http://0.0.0.0:9770", @"https://localhost:9771"]);
                settings.Certificate.ShouldNotBeNull();
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Theory]
        [InlineData(@"http://localhost:9770", false)]
        [InlineData(@"http://127.0.0.1:9770", false)]
        [InlineData(@"http://[::1]:9770", false)]
        [InlineData(@"https://localhost:9770", false)]
        [InlineData(@"https://0.0.0.0:9770", false)]
        [InlineData(@"http://0.0.0.0:9770", true)]
        [InlineData(@"http://[::]:9770", true)]
        [InlineData(@"http://*:9770", true)]
        [InlineData(@"http://192.168.1.10:9770", true)]
        public void IsPlainHttpBeyondThisMachine_Given_AnAddress_Then_WhetherItIsPlainAndOtherMachinesCanReachIt(string url, bool expected)
        {
            ServeSettingsHelper.IsPlainHttpBeyondThisMachine(ServeSettingsHelper.ParseListenAddress(url)).ShouldBe(expected);
        }

        [Theory]
        [InlineData(LogFormat.Text)]
        [InlineData(LogFormat.Json)]
        public void Resolve_Given_ALogFormat_Then_ItInTheSettings(LogFormat format)
        {
            Resolve(new ServeOptions { LogFormat = format }).LogFormat.ShouldBe(format);
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
