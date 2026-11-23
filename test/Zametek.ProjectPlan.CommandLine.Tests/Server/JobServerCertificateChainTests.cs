using Shouldly;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// A certificate from a certificate authority comes with intermediate certificates, in a fullchain.pem file or in a .pfx
    /// file with its chain. The server loaded one certificate of the file, and it sent that certificate alone. A client that
    /// does not have the intermediate certificates then cannot build the chain to a root that it trusts (from the review of
    /// src, test and pkg). The server now sends the other certificates of the file too.
    /// </summary>
    [Collection(ProgramExitCodeTests.CollectionName)]
    public class JobServerCertificateChainTests
        : IClassFixture<EngineFixture>
    {
        private readonly EngineFixture m_Engine;

        public JobServerCertificateChainTests(EngineFixture engine)
        {
            m_Engine = engine;
        }

        [Theory]
        [InlineData(@"pfx")]
        [InlineData(@"pem and key")]
        public async Task Request_Given_ACertificateWithAnIntermediate_Then_TheServerSendsTheIntermediate(string form)
        {
            string directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $@"zpp-chain-{Guid.NewGuid():N}")).FullName;

            try
            {
                (X509Certificate2 root, X509Certificate2 intermediate, string certificateFile, string? keyFile, string? password) = WriteChain(form, directory);
                using X509Certificate2 _ = root;
                using X509Certificate2 __ = intermediate;
                using X509Certificate2 certificate = ServeSettingsHelper.LoadCertificate(certificateFile, keyFile, password);
                X509Certificate2Collection chain = ServeSettingsHelper.LoadCertificateChain(certificateFile, keyFile, password, certificate);

                chain.Count.ShouldBe(1);
                chain[0].Thumbprint.ShouldBe(intermediate.Thumbprint);

                // The client knows neither the intermediate nor the root. It builds the chain from what the server sends.
                List<string> built = [];
                var handler = new SocketsHttpHandler();
                handler.SslOptions.RemoteCertificateValidationCallback = (_, presented, clientChain, _) =>
                {
                    if (clientChain is not null)
                    {
                        built.AddRange(clientChain.ChainElements.Select(x => x.Certificate.Thumbprint));
                    }

                    return presented is not null && presented.GetCertHashString() == certificate.GetCertHashString();
                };

                await using RunningServer server = await RunningServer.StartAsync(
                    m_Engine.JobRunner,
                    new ServeSettings
                    {
                        Listen = [ServeSettingsHelper.ParseListenAddress(@"https://127.0.0.1:0")],
                        Certificate = certificate,
                        CertificateChain = chain,
                    },
                    handler: handler);

                using HttpResponseMessage response = await server.Client.GetAsync(@"/v1/info");

                response.StatusCode.ShouldBe(HttpStatusCode.OK);
                built.ShouldContain(certificate.Thumbprint);
                built.ShouldContain(intermediate.Thumbprint);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void LoadCertificateChain_Given_AFileWithOneCertificate_Then_None()
        {
            string directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $@"zpp-chain-{Guid.NewGuid():N}")).FullName;

            try
            {
                using X509Certificate2 certificate = TestCertificates.CreateSelfSigned(directory);
                string file = Path.Combine(directory, @"server.pfx");

                ServeSettingsHelper.LoadCertificateChain(file, null, @"p4ssw0rd", certificate).ShouldBeEmpty();
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        // A root, an intermediate that the root signs, and a certificate for this computer that the intermediate signs. The
        // file holds the certificate and the intermediate, as a certificate authority gives them, and not the root.
        private static (X509Certificate2 Root, X509Certificate2 Intermediate, string CertificateFile, string? KeyFile, string? Password) WriteChain(
            string form,
            string directory)
        {
            using RSA rootKey = RSA.Create(2048);
            var rootRequest = new CertificateRequest(@"CN=Test Root", rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            X509Certificate2 root = rootRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(2));

            using RSA intermediateKey = RSA.Create(2048);
            var intermediateRequest = new CertificateRequest(@"CN=Test Intermediate", intermediateKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            intermediateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            intermediateRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            using X509Certificate2 intermediateWithoutKey = intermediateRequest.Create(root, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1), [1, 2, 3, 4]);
            X509Certificate2 intermediate = intermediateWithoutKey.CopyWithPrivateKey(intermediateKey);

            using RSA key = RSA.Create(2048);
            var request = new CertificateRequest(@"CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var names = new SubjectAlternativeNameBuilder();
            names.AddIpAddress(IPAddress.Loopback);
            names.AddDnsName(@"localhost");
            request.CertificateExtensions.Add(names.Build());
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid(@"1.3.6.1.5.5.7.3.1")], critical: false));
            using X509Certificate2 signed = request.Create(intermediate, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(12), [5, 6, 7, 8]);
            using X509Certificate2 leaf = signed.CopyWithPrivateKey(key);

            if (form == @"pfx")
            {
                string file = Path.Combine(directory, @"server.pfx");
                using X509Certificate2 intermediatePublic = X509CertificateLoader.LoadCertificate(intermediate.RawData);
                X509Certificate2Collection collection = [leaf, intermediatePublic];
                File.WriteAllBytes(file, collection.Export(X509ContentType.Pkcs12, @"p4ssw0rd")!);
                return (root, intermediate, file, null, @"p4ssw0rd");
            }

            string certificateFile = Path.Combine(directory, @"fullchain.pem");
            string keyFile = Path.Combine(directory, @"server.key");
            File.WriteAllText(certificateFile, leaf.ExportCertificatePem() + Environment.NewLine + intermediate.ExportCertificatePem() + Environment.NewLine);
            File.WriteAllText(keyFile, key.ExportPkcs8PrivateKeyPem());
            return (root, intermediate, certificateFile, keyFile, null);
        }
    }
}
