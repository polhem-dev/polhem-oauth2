using System.Security.Cryptography;
using System.Text;

namespace Polhem.OAuth2.UnitTests
{
    /// <summary>
    /// Creates Sign in with Apple keys for tests: a new P-256 key for each call, written as the PEM-encoded PKCS#8 text of a
    /// <c>.p8</c> file. No key is kept in the repository, and the text is built by hand because .NET Framework cannot export
    /// PKCS#8.
    /// </summary>
    internal sealed class TestAppleKey : IDisposable
    {
        // The DER of RFC 5208 PrivateKeyInfo with an RFC 5915 ECPrivateKey on prime256v1, up to the private key, and the
        // bytes between the private key and the uncompressed public key. This is the layout of the files Apple issues.
        private const string Pkcs8Prefix = "308187020100301306072a8648ce3d020106082a8648ce3d030107046d306b0201010420";
        private const string PublicKeyPrefix = "a14403420004";
        private const string Pkcs8PrefixWithoutPublicKey = "3041020100301306072a8648ce3d020106082a8648ce3d03010704273025020101" + "0420";

        private readonly ECParameters _parameters;

        public TestAppleKey()
        {
            Key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            _parameters = Key.ExportParameters(includePrivateParameters: true);
        }

        /// <summary>
        /// Gets the key, which verifies the signatures made with <see cref="Pem"/>.
        /// </summary>
        public ECDsa Key { get; }

        /// <summary>
        /// Gets the key as the text of a <c>.p8</c> file.
        /// </summary>
        public string Pem => ToPem(Pkcs8Prefix + Hex(_parameters.D!) + PublicKeyPrefix + Hex(_parameters.Q.X!) + Hex(_parameters.Q.Y!));

        /// <summary>
        /// Gets the private key of this key with the public key of another, as in a file whose private key was altered.
        /// </summary>
        /// <param name="other">The key whose public key the text holds.</param>
        /// <returns>The text.</returns>
        public string PemWithPublicKeyOf(TestAppleKey other)
        {
            return ToPem(Pkcs8Prefix + Hex(_parameters.D!) + PublicKeyPrefix + Hex(other._parameters.Q.X!) + Hex(other._parameters.Q.Y!));
        }

        /// <summary>
        /// Gets the key without its public key, which a <c>.p8</c> file from Apple always includes.
        /// </summary>
        public string PemWithoutPublicKey => ToPem(Pkcs8PrefixWithoutPublicKey + Hex(_parameters.D!));

        /// <summary>
        /// Gets the key with the last byte of the curve identifier changed, so that it names a curve other than P-256.
        /// </summary>
        public string PemWithOtherCurve => ToPem(
            Pkcs8Prefix.Replace("2a8648ce3d030107", "2a8648ce3d030108") + Hex(_parameters.D!) + PublicKeyPrefix + Hex(_parameters.Q.X!) + Hex(_parameters.Q.Y!));

        public void Dispose()
        {
            Key.Dispose();
        }

        private static string ToPem(string hex)
        {
            byte[] der = new byte[hex.Length / 2];
            for (int i = 0; i < der.Length; i++)
                der[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);

            string base64 = Convert.ToBase64String(der);
            var pem = new StringBuilder("-----BEGIN PRIVATE KEY-----\n");
            for (int i = 0; i < base64.Length; i += 64)
                pem.Append(base64, i, Math.Min(64, base64.Length - i)).Append('\n');
            return pem.Append("-----END PRIVATE KEY-----\n").ToString();
        }

        private static string Hex(byte[] bytes)
        {
#if NET
            return Convert.ToHexString(bytes);
#else
            return BitConverter.ToString(bytes).Replace("-", string.Empty);
#endif
        }
    }
}
