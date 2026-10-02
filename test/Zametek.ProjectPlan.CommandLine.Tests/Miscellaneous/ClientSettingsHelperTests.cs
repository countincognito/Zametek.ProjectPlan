using Shouldly;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for where zpp runs: on the server --server names, or else on
    /// ZPP_SERVER's - unless --local says to run here - or here; the addresses
    /// it takes a server at; the API key it sends one, from --api-key-file or
    /// ZPP_API_KEY; and that anything it cannot run with - a server and --local
    /// both, an address that is not a server's, a key with no server to send it
    /// to, or an MS Project import a server cannot do - is a usage error.
    /// </summary>
    public class ClientSettingsHelperTests
    {
        private const string c_Server = @"http://localhost:9770";

        private static ClientSettings? Resolve(
            Options options,
            params (string Name, string Value)[] variables)
        {
            return ClientSettingsHelper.Resolve(options, variables.ToDictionary(x => x.Name, x => x.Value, StringComparer.OrdinalIgnoreCase));
        }

        [Fact]
        public void Resolve_Given_NoServer_Then_RunsHere()
        {
            Resolve(new Options { InputFilename = @"plan.zpp" }).ShouldBeNull();
        }

        [Fact]
        public void Resolve_Given_AServer_Then_RunsThere()
        {
            ClientSettings settings = Resolve(new Options { InputFilename = @"plan.zpp", Server = c_Server }).ShouldNotBeNull();

            settings.ShouldBe(new ClientSettings { Server = c_Server, BaseAddress = new Uri(c_Server + @"/") });
        }

        [Fact]
        public void Resolve_Given_TheServerInTheEnvironment_Then_RunsThere()
        {
            Resolve(new Options { InputFilename = @"plan.zpp" }, (ClientSettingsHelper.ServerVariable, $@" {c_Server} "))
                .ShouldNotBeNull().Server.ShouldBe(c_Server);
        }

        [Fact]
        public void Resolve_Given_AServerAndOneInTheEnvironment_Then_TheOptionsServer()
        {
            Resolve(new Options { InputFilename = @"plan.zpp", Server = c_Server }, (ClientSettingsHelper.ServerVariable, @"http://elsewhere:9770"))
                .ShouldNotBeNull().Server.ShouldBe(c_Server);
        }

        [Fact]
        public void Resolve_Given_LocalAndAServerInTheEnvironment_Then_RunsHere()
        {
            Resolve(new Options { InputFilename = @"plan.zpp", Local = true }, (ClientSettingsHelper.ServerVariable, c_Server)).ShouldBeNull();
        }

        [Fact]
        public void Resolve_Given_ABlankServerInTheEnvironment_Then_RunsHere()
        {
            Resolve(new Options { InputFilename = @"plan.zpp" }, (ClientSettingsHelper.ServerVariable, @" ")).ShouldBeNull();
        }

        [Fact]
        public void Resolve_Given_AServerAndLocal_Then_UsageException()
        {
            Should.Throw<UsageException>(() => Resolve(new Options { InputFilename = @"plan.zpp", Server = c_Server, Local = true }))
                .Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_SpecifyEitherOptionNotBoth, @"--server", @"--local"));
        }

        [Theory]
        [InlineData(@"http://localhost:9770", @"http://localhost:9770/")]
        [InlineData(@"http://localhost:9770/", @"http://localhost:9770/")]
        [InlineData(@"https://zpp.example.com", @"https://zpp.example.com/")]
        [InlineData(@"https://example.com/zpp", @"https://example.com/zpp/")]
        [InlineData(@"http://[::1]:9770", @"http://[::1]:9770/")]
        public void ParseServer_Given_AnAddress_Then_TheAddressTheApisPathsAreTakenFrom(string server, string baseAddress)
        {
            (Uri address, string? socket) = ClientSettingsHelper.ParseServer(server, @"--server");

            address.ShouldBe(new Uri(baseAddress));
            socket.ShouldBeNull();
        }

        [Theory]
        [InlineData(@"unix:/tmp/zpp.sock", @"/tmp/zpp.sock")]
        [InlineData(@"unix:///tmp/zpp.sock", @"/tmp/zpp.sock")]
        [InlineData(@"UNIX:zpp.sock", @"zpp.sock")]
        public void ParseServer_Given_ASocket_Then_ItsPathInFull(string server, string path)
        {
            (Uri address, string? socket) = ClientSettingsHelper.ParseServer(server, @"--server");

            address.ShouldBe(new Uri(@"http://localhost/"));
            socket.ShouldBe(Path.GetFullPath(path));
        }

        [Theory]
        [InlineData(@"")]
        [InlineData(@"localhost:9770")]
        [InlineData(@"ftp://localhost:9770")]
        [InlineData(@"http://user:password@localhost:9770")]
        [InlineData(@"http://localhost:9770/?job=1")]
        [InlineData(@"http://localhost:9770/#job")]
        [InlineData(@"unix:")]
        [InlineData(@"unix://")]
        public void ParseServer_Given_AnythingElse_Then_UsageExceptionNamingWhereItCameFrom(string server)
        {
            Should.Throw<UsageException>(() => ClientSettingsHelper.ParseServer(server, ClientSettingsHelper.ServerVariable))
                .Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServerNotValid, server, ClientSettingsHelper.ServerVariable));
        }

        [Fact]
        public void Resolve_Given_AKeyInTheEnvironment_Then_TheKey()
        {
            Resolve(new Options { InputFilename = @"plan.zpp", Server = c_Server }, (ClientSettingsHelper.ApiKeyVariable, @" s3cr3t "))
                .ShouldNotBeNull().ApiKey.ShouldBe(@"s3cr3t");
        }

        [Fact]
        public void Resolve_Given_AKeyFile_Then_ItsKeyRatherThanTheEnvironments()
        {
            string keyFile = Path.GetTempFileName();

            try
            {
                File.WriteAllText(keyFile, @"from-the-file" + Environment.NewLine);

                Resolve(new Options { InputFilename = @"plan.zpp", Server = c_Server, ApiKeyFile = keyFile }, (ClientSettingsHelper.ApiKeyVariable, @"from-the-environment"))
                    .ShouldNotBeNull().ApiKey.ShouldBe(@"from-the-file");
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
                Should.Throw<UsageException>(() => Resolve(new Options { InputFilename = @"plan.zpp", Server = c_Server, ApiKeyFile = keyFile }))
                    .Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_FileIsEmpty, keyFile));
            }
            finally
            {
                File.Delete(keyFile);
            }
        }

        [Fact]
        public void Resolve_Given_AKeyFileAndNoServer_Then_UsageException()
        {
            Should.Throw<UsageException>(() => Resolve(new Options { InputFilename = @"plan.zpp", ApiKeyFile = @"api-key", Local = true }, (ClientSettingsHelper.ServerVariable, c_Server)))
                .Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_OptionOnlyValidWithServer, @"--api-key-file", @"--server", ClientSettingsHelper.ServerVariable));
        }

        [Fact]
        public void Resolve_Given_AnMsProjectImportAndAServer_Then_UsageExceptionSayingToRunWithoutIt()
        {
            Should.Throw<UsageException>(() => Resolve(new Options { ImportFilename = @"plan.mpp", Server = c_Server }))
                .Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServerCannotImport, @"plan.mpp", @"--server"));
        }

        [Fact]
        public void Resolve_Given_AnMsProjectImportAndTheServerInTheEnvironment_Then_UsageExceptionSayingToUseLocal()
        {
            Should.Throw<UsageException>(() => Resolve(new Options { ImportFilename = @"plan.xml" }, (ClientSettingsHelper.ServerVariable, c_Server)))
                .Message.ShouldBe(string.Format(
                    Resource.ProjectPlan.Messages.Message_ServerFromEnvironmentCannotImport,
                    ClientSettingsHelper.ServerVariable,
                    @"plan.xml",
                    @"--local"));
        }

        [Fact]
        public void Resolve_Given_AWorkbookImportAndAServer_Then_RunsThere()
        {
            Resolve(new Options { ImportFilename = @"plan.xlsx", Server = c_Server }).ShouldNotBeNull();
        }

        [Fact]
        public void Resolve_Given_AnMsProjectImportAndLocal_Then_RunsHere()
        {
            Resolve(new Options { ImportFilename = @"plan.mpp", Local = true }, (ClientSettingsHelper.ServerVariable, c_Server)).ShouldBeNull();
        }
    }
}
