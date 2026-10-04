using CommandLine;
using Shouldly;
using System.Reflection;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for zpp serve's options as seen through a parser configured the
    /// way JobServer configures its own - which, unlike zpp's, takes an option
    /// more than once. The limits are optional, so that configuration supplies
    /// any that are not given.
    /// </summary>
    public class ServeOptionsParsingTests
    {
        // ArgumentsHelperTests parses with it too.
        internal static ParserResult<ServeOptions> Parse(params string[] args)
        {
            using var parser = new Parser(with =>
            {
                with.CaseInsensitiveEnumValues = true;
                with.HelpWriter = null;
                with.AutoVersion = false;
                with.AllowMultiInstance = true;
            });

            return parser.ParseArguments<ServeOptions>(args);
        }

        private static ServeOptions ParsedValue(params string[] args)
        {
            return Parse(args).ShouldBeOfType<Parsed<ServeOptions>>().Value;
        }

        [Fact]
        public void Parse_Given_Nothing_Then_LeavesEveryLimitToConfiguration()
        {
            ServeOptions options = ParsedValue();

            options.Listen.ShouldBeEmpty();
            options.ApiKeyFile.ShouldBeNull();
            options.Certificate.ShouldBeNull();
            options.CertificateKey.ShouldBeNull();
            options.Culture.ShouldBeNull();
            options.Verbose.ShouldBeFalse();
            options.LogFormat.ShouldBe(LogFormat.Text);
            options.BehindTlsProxy.ShouldBeFalse();
            options.MaxJobs.ShouldBeNull();
            options.MaxQueue.ShouldBeNull();
            options.MaxUploadMegabytes.ShouldBeNull();
            options.MaxChartSize.ShouldBeEmpty();
            options.JobTimeoutSeconds.ShouldBeNull();
            options.MaxCompileTimeoutMilliseconds.ShouldBeNull();
        }

        [Theory]
        [InlineData(@"text", LogFormat.Text)]
        [InlineData(@"Text", LogFormat.Text)]
        [InlineData(@"json", LogFormat.Json)]
        [InlineData(@"JSON", LogFormat.Json)]
        public void Parse_Given_ALogFormat_Then_ItInAnyCase(string value, LogFormat expected)
        {
            ParsedValue(@"--log-format", value).LogFormat.ShouldBe(expected);
        }

        [Fact]
        public void Parse_Given_ALogFormatThatIsNotOne_Then_AParseError()
        {
            Parse(@"--log-format", @"xml").ShouldBeOfType<NotParsed<ServeOptions>>();
        }

        [Fact]
        public void Parse_Given_BehindTlsProxy_Then_ItIsSet()
        {
            ParsedValue(@"--behind-tls-proxy").BehindTlsProxy.ShouldBeTrue();
        }

        [Fact]
        public void Parse_Given_ListenTwice_Then_BothAddresses()
        {
            ServeOptions options = ParsedValue(@"--listen", @"http://localhost:9770", @"--listen", @"https://0.0.0.0:9771");

            options.Listen.ShouldBe([@"http://localhost:9770", @"https://0.0.0.0:9771"]);
        }

        [Fact]
        public void Parse_Given_ListenWithTwoAddresses_Then_BothAddresses()
        {
            ServeOptions options = ParsedValue(@"--listen", @"http://localhost:9770", @"https://0.0.0.0:9771");

            options.Listen.ShouldBe([@"http://localhost:9770", @"https://0.0.0.0:9771"]);
        }

        [Fact]
        public void Parse_Given_ASocketAndAnAddress_Then_BothAddresses()
        {
            // A socket is listened on by its address, as an address on the network is.
            ServeOptions options = ParsedValue(@"--listen", @"unix:/run/zpp.sock", @"--listen", @"http://localhost:9770");

            options.Listen.ShouldBe([@"unix:/run/zpp.sock", @"http://localhost:9770"]);
        }

        [Fact]
        public void Parse_Given_ASocketWithADrive_Then_ItAsItIsGiven()
        {
            ServeOptions options = ParsedValue(@"--listen", @"unix:C:\Users\me\zpp.sock");

            options.Listen.ShouldBe([@"unix:C:\Users\me\zpp.sock"]);
        }

        [Fact]
        public void Parse_Given_TheRemovedUnixSocketOption_Then_Fails()
        {
            Parse(@"--unix-socket", @"/run/zpp.sock").ShouldBeOfType<NotParsed<ServeOptions>>();
        }

        [Fact]
        public void Parse_Given_EveryLimit_Then_Each()
        {
            ServeOptions options = ParsedValue(
                @"--max-jobs", @"2",
                @"--max-queue", @"3",
                @"--max-upload", @"4",
                @"--max-chart-size", @"600:400",
                @"--job-timeout", @"5",
                @"--max-compile-timeout", @"6000");

            options.MaxJobs.ShouldBe(2);
            options.MaxQueue.ShouldBe(3);
            options.MaxUploadMegabytes.ShouldBe(4);
            options.MaxChartSize.ShouldBe([600, 400]);
            options.JobTimeoutSeconds.ShouldBe(5);
            options.MaxCompileTimeoutMilliseconds.ShouldBe(6000);
        }

        [Fact]
        public void Parse_Given_TheRest_Then_Each()
        {
            ServeOptions options = ParsedValue(
                @"--api-key-file", @"key.txt",
                @"--certificate", @"server.pem",
                @"--certificate-key", @"server.key",
                @"--culture", @"en-US",
                @"-v");

            options.ApiKeyFile.ShouldBe(@"key.txt");
            options.Certificate.ShouldBe(@"server.pem");
            options.CertificateKey.ShouldBe(@"server.key");
            options.Culture.ShouldBe(@"en-US");
            options.Verbose.ShouldBeTrue();
        }

        [Fact]
        public void Parse_Given_AChartSizeWithOneValue_Then_Fails()
        {
            Parse(@"--max-chart-size", @"600").ShouldBeOfType<NotParsed<ServeOptions>>();
        }

        [Fact]
        public void Parse_Given_AnUnknownOption_Then_Fails()
        {
            Parse(@"--nonsense").ShouldBeOfType<NotParsed<ServeOptions>>();
        }

        [Fact]
        public void Options_Given_EveryOptionProperty_Then_HasLongName()
        {
            // Messages name options by their long names, as zpp's do. The options are the instance properties, which
            // the parser sets - Usage, which is static, is the help's text.
            foreach (PropertyInfo property in typeof(ServeOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                OptionAttribute? attribute = property.GetCustomAttribute<OptionAttribute>();

                attribute.ShouldNotBeNull();
                attribute.LongName.ShouldNotBeNullOrWhiteSpace($@"{property.Name} has no long option name");
            }
        }
    }
}
