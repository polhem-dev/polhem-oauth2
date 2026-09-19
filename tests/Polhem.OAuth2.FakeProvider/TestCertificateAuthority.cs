using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Polhem.OAuth2.FakeProvider
{
    /// <summary>
    /// A certificate authority created for one run, and the <c>localhost</c> server certificate it issues. Only devices
    /// that the test scripts set up trust it, and its private key never leaves the process.
    /// </summary>
    internal sealed class TestCertificateAuthority : IDisposable
    {
        private TestCertificateAuthority(X509Certificate2 authority, X509Certificate2 serverCertificate)
        {
            Authority = authority;
            ServerCertificate = serverCertificate;
        }

        /// <summary>
        /// Gets the certificate of the authority, without its private key.
        /// </summary>
        public X509Certificate2 Authority { get; }

        /// <summary>
        /// Gets the server certificate for <c>localhost</c> and <c>127.0.0.1</c>, with its private key.
        /// </summary>
        public X509Certificate2 ServerCertificate { get; }

        public static TestCertificateAuthority Create()
        {
            var now = DateTimeOffset.UtcNow;

            // RSA 2048 and a validity of a few days meet the limits that Apple platforms place on trusted certificates.
            using var authorityKey = RSA.Create(2048);
            var authorityRequest = new CertificateRequest(
                "CN=Polhem.OAuth2 Fake Provider Test CA", authorityKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            authorityRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
            authorityRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            authorityRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(authorityRequest.PublicKey, false));
            using var authority = authorityRequest.CreateSelfSigned(now.AddHours(-1), now.AddDays(3));

            using var serverKey = RSA.Create(2048);
            var serverRequest = new CertificateRequest("CN=localhost", serverKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var names = new SubjectAlternativeNameBuilder();
            names.AddDnsName("localhost");
            names.AddIpAddress(IPAddress.Loopback);
            serverRequest.CertificateExtensions.Add(names.Build());
            serverRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            serverRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
            serverRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
            serverRequest.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(authority, true, false));
            byte[] serial = RandomNumberGenerator.GetBytes(16);
            serial[0] &= 0x7F;
            using var issued = serverRequest.Create(authority, now.AddHours(-1), now.AddDays(2), serial);
            using var server = issued.CopyWithPrivateKey(serverKey);

            // Kestrel on macOS needs the key in a form the platform can load, which a PKCS#12 round trip gives.
            var serverCertificate = X509CertificateLoader.LoadPkcs12(server.Export(X509ContentType.Pkcs12), null);
            return new TestCertificateAuthority(X509CertificateLoader.LoadCertificate(authority.RawData), serverCertificate);
        }

        /// <summary>
        /// Writes the certificate of the authority as PEM.
        /// </summary>
        public void WriteAuthorityPem(string path)
        {
            string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (directory is not null)
                Directory.CreateDirectory(directory);
            File.WriteAllText(path, Authority.ExportCertificatePem() + "\n");
        }

        /// <summary>
        /// Creates an HTTP client that trusts only this authority, for the requests the relay sends to this process.
        /// </summary>
        public HttpClient CreateHttpClient()
        {
            var handler = new SocketsHttpHandler
            {
                SslOptions = new SslClientAuthenticationOptions
                {
                    CertificateChainPolicy = new X509ChainPolicy
                    {
                        TrustMode = X509ChainTrustMode.CustomRootTrust,
                        RevocationMode = X509RevocationMode.NoCheck,
                        CustomTrustStore = { Authority }
                    }
                }
            };
            return new HttpClient(handler);
        }

        public void Dispose()
        {
            Authority.Dispose();
            ServerCertificate.Dispose();
        }
    }
}
