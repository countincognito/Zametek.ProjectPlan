using Shouldly;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for the proxy that zpp --server uses. The environment can give a proxy for the web (http_proxy), and .NET then
    /// sends each request through it, also a request for localhost. A server on this computer gets the key over plain http,
    /// because the key stays on this computer. Thus, zpp connects to such a server directly, as it does to a socket, and
    /// the proxy does not see the key or the plan.
    /// </summary>
    public class JobClientProxyTests
    {
        private static ClientSettings Settings(string server)
        {
            (Uri baseAddress, string? socket) = ClientSettingsHelper.ParseServer(server, @"--server");
            return new ClientSettings { Server = server, BaseAddress = baseAddress, UnixSocket = socket };
        }

        [Theory]
        [InlineData(@"http://localhost:9781")]
        [InlineData(@"http://LOCALHOST:9781")]
        [InlineData(@"http://127.0.0.1:9781")]
        [InlineData(@"http://127.0.0.2:9781")]
        [InlineData(@"http://[::1]:9781")]
        [InlineData(@"https://localhost:9771/zpp/")]
        public void CreateHandler_Given_AServerOnThisComputer_Then_NoProxy(string server)
        {
            using var handler = JobClient.CreateHandler(Settings(server));

            handler.UseProxy.ShouldBeFalse();
        }

        [Fact]
        public void CreateHandler_Given_ASocket_Then_NoProxy()
        {
            using var handler = JobClient.CreateHandler(Settings($@"unix:{Path.Combine(Path.GetTempPath(), @"zpp.sock")}"));

            handler.UseProxy.ShouldBeFalse();
        }

        // A server on a different computer can need the proxy to be reached at all.
        [Theory]
        [InlineData(@"http://plan.example.com:9770")]
        [InlineData(@"https://plan.example.com")]
        [InlineData(@"http://10.0.0.5:9770")]
        [InlineData(@"http://[2001:db8::1]:9770")]
        public void CreateHandler_Given_AServerOnAnotherComputer_Then_TheProxyOfTheEnvironment(string server)
        {
            using var handler = JobClient.CreateHandler(Settings(server));

            handler.UseProxy.ShouldBeTrue();
            handler.Proxy.ShouldBeNull();
        }

        // The other settings of the handler are the same for each server.
        [Theory]
        [InlineData(@"http://localhost:9781")]
        [InlineData(@"https://plan.example.com")]
        public void CreateHandler_Given_AServer_Then_NoRedirect(string server)
        {
            using var handler = JobClient.CreateHandler(Settings(server));

            handler.AllowAutoRedirect.ShouldBeFalse();
        }
    }
}
