using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    // A certificate for this machine that the server made for itself - which nothing on the machine vouches for - for a test of
    // what a server does over https, read as zpp serve reads one.
    internal static class TestCertificates
    {
        public static X509Certificate2 CreateSelfSigned(string directory)
        {
            ArgumentNullException.ThrowIfNull(directory);

            using RSA key = RSA.Create(2048);
            var request = new CertificateRequest(@"CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var names = new SubjectAlternativeNameBuilder();
            names.AddIpAddress(IPAddress.Loopback);
            names.AddDnsName(@"localhost");
            request.CertificateExtensions.Add(names.Build());
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid(@"1.3.6.1.5.5.7.3.1")], critical: false));
            using X509Certificate2 created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

            string file = Path.Combine(directory, @"server.pfx");
            File.WriteAllBytes(file, created.Export(X509ContentType.Pkcs12, @"p4ssw0rd"));
            return ServeSettingsHelper.LoadCertificate(file, null, @"p4ssw0rd");
        }

        // A handler that trusts the certificate, and no other.
        public static SocketsHttpHandler Trusting(X509Certificate2 certificate)
        {
            ArgumentNullException.ThrowIfNull(certificate);

            var handler = new SocketsHttpHandler();
            handler.SslOptions.RemoteCertificateValidationCallback = (_, presented, _, _) =>
                presented is not null && presented.GetCertHashString() == certificate.GetCertHashString();
            return handler;
        }
    }
}
