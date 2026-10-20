using System.Security.Cryptography;

namespace Polhem.OAuth2
{
    /// <summary>
    /// Reads the private key of a Sign in with Apple key: the PEM-encoded PKCS#8 file with the <c>.p8</c> extension, which
    /// holds a P-256 key together with its public key.
    /// </summary>
    /// <remarks>
    /// The key is read here rather than with <c>ECDsa.ImportFromPem</c>, which netstandard2.0 does not have, so that every
    /// target reads it the same way and accepts the same files. Error messages never include any part of the key.
    /// </remarks>
    internal static class AppleSigningKey
    {
        private const string PemHeader = "-----BEGIN PRIVATE KEY-----";
        private const string PemFooter = "-----END PRIVATE KEY-----";
        private const int CoordinateLength = 32;

        private const byte SequenceTag = 0x30;
        private const byte IntegerTag = 0x02;
        private const byte BitStringTag = 0x03;
        private const byte OctetStringTag = 0x04;
        private const byte ObjectIdentifierTag = 0x06;
        private const byte ParametersTag = 0xA0;
        private const byte PublicKeyTag = 0xA1;

        // 1.2.840.10045.2.1 (id-ecPublicKey) and 1.2.840.10045.3.1.7 (prime256v1, also called P-256), DER-encoded.
        private static readonly byte[] s_ecPublicKeyOid = { 0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x02, 0x01 };
        private static readonly byte[] s_prime256v1Oid = { 0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x03, 0x01, 0x07 };

        /// <summary>
        /// Creates the signing key from the PEM text of a <c>.p8</c> file.
        /// </summary>
        /// <param name="pem">The PEM text, with the <c>BEGIN PRIVATE KEY</c> and <c>END PRIVATE KEY</c> lines.</param>
        /// <returns>The key, which the caller disposes.</returns>
        /// <exception cref="FormatException">The text is not a PEM-encoded PKCS#8 P-256 private key with its public key.</exception>
        /// <exception cref="CryptographicException">The platform refused the key.</exception>
        public static ECDsa Create(string pem)
        {
            byte[] der = DecodePem(pem);
            var parameters = ReadPkcs8(der);
            try
            {
                return ECDsa.Create(parameters);
            }
            finally
            {
                Array.Clear(der, 0, der.Length);
                if (parameters.D is { } d)
                    Array.Clear(d, 0, d.Length);
            }
        }

        /// <summary>
        /// Checks whether a text is a private key that <see cref="Create"/> accepts.
        /// </summary>
        /// <param name="pem">The PEM text.</param>
        /// <returns>A message that describes the problem without revealing the key, or null if the key can be used.</returns>
        public static string? GetError(string pem)
        {
            try
            {
                using (var key = Create(pem))
                {
                    return HasMatchingPublicKey(key) ? null : "The private key does not match the public key in the file. Use the .p8 file as Apple issued it.";
                }
            }
            catch (FormatException ex)
            {
                return ex.Message;
            }
            catch (CryptographicException)
            {
                return "The platform refused the private key.";
            }
        }

        // Some platforms import a private key without checking that it matches the public key, so a file whose private key
        // was altered would only fail at Apple. A signature that the public key alone verifies shows that the two match.
        private static bool HasMatchingPublicKey(ECDsa key)
        {
            byte[] data = { 0x50, 0x6F, 0x6C, 0x68, 0x65, 0x6D };
            byte[] signature = key.SignData(data, HashAlgorithmName.SHA256);
            using (var publicKey = ECDsa.Create(key.ExportParameters(includePrivateParameters: false)))
            {
                return publicKey.VerifyData(data, signature, HashAlgorithmName.SHA256);
            }
        }

        private static byte[] DecodePem(string pem)
        {
            int start = pem.IndexOf(PemHeader, StringComparison.Ordinal);
            int end = start < 0 ? -1 : pem.IndexOf(PemFooter, start, StringComparison.Ordinal);
            if (start < 0 || end < 0)
                throw new FormatException("The private key must be the PEM text of the .p8 file, from its BEGIN PRIVATE KEY line to its END PRIVATE KEY line.");

            string base64 = pem.Substring(start + PemHeader.Length, end - start - PemHeader.Length);
            try
            {
                return Convert.FromBase64String(base64);
            }
            catch (FormatException ex)
            {
                // The message of the inner exception does not quote the text, but a new message keeps that certain.
                throw new FormatException("The private key is not valid base64 between its PEM lines.", ex);
            }
        }

        // RFC 5208 PrivateKeyInfo around the RFC 5915 ECPrivateKey:
        //   SEQUENCE { INTEGER 0, SEQUENCE { OID id-ecPublicKey, OID prime256v1 }, OCTET STRING { ECPrivateKey } }
        //   ECPrivateKey: SEQUENCE { INTEGER 1, OCTET STRING d, [0] parameters OPTIONAL, [1] BIT STRING publicKey OPTIONAL }
        private static ECParameters ReadPkcs8(byte[] der)
        {
            var info = new DerReader(der).ReadSequence();
            if (!info.ReadInteger().SequenceEqual(new byte[] { 0 }))
                throw NotP256();

            var algorithm = info.ReadSequence();
            if (!algorithm.Read(ObjectIdentifierTag).SequenceEqual(s_ecPublicKeyOid) || !algorithm.Read(ObjectIdentifierTag).SequenceEqual(s_prime256v1Oid))
                throw NotP256();

            var key = new DerReader(info.Read(OctetStringTag)).ReadSequence();
            if (!key.ReadInteger().SequenceEqual(new byte[] { 1 }))
                throw NotP256();

            byte[] d = key.Read(OctetStringTag);
            if (d.Length == 0 || d.Length > CoordinateLength)
                throw NotP256();

            if (key.PeekTag() == ParametersTag)
            {
                var curve = new DerReader(key.Read(ParametersTag));
                if (!curve.Read(ObjectIdentifierTag).SequenceEqual(s_prime256v1Oid))
                    throw NotP256();
            }
            if (key.PeekTag() != PublicKeyTag)
                throw new FormatException("The private key does not include its public key. Use the .p8 file as Apple issued it.");

            // An uncompressed point after the byte that counts the unused bits: 0x00, 0x04, X, Y.
            byte[] point = new DerReader(key.Read(PublicKeyTag)).Read(BitStringTag);
            if (point.Length != 2 + (2 * CoordinateLength) || point[0] != 0 || point[1] != 0x04)
                throw NotP256();

            var parameters = new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                D = PadLeft(d),
                Q = new ECPoint
                {
                    X = point.Skip(2).Take(CoordinateLength).ToArray(),
                    Y = point.Skip(2 + CoordinateLength).ToArray()
                }
            };
            Array.Clear(d, 0, d.Length);
            return parameters;
        }

        // RFC 5915 fixes the length of d, but a shorter value is padded rather than refused.
        private static byte[] PadLeft(byte[] value)
        {
            byte[] padded = new byte[CoordinateLength];
            Buffer.BlockCopy(value, 0, padded, CoordinateLength - value.Length, value.Length);
            return padded;
        }

        private static FormatException NotP256()
        {
            return new FormatException("The private key is not a PKCS#8 P-256 private key, which a Sign in with Apple key file holds.");
        }

        // Reads the definite-length DER that a key file uses: one-byte tags and lengths of up to four bytes.
        private sealed class DerReader
        {
            private readonly byte[] _data;
            private int _position;

            public DerReader(byte[] data)
            {
                _data = data;
            }

            public int PeekTag()
            {
                return _position < _data.Length ? _data[_position] : -1;
            }

            public DerReader ReadSequence()
            {
                return new DerReader(Read(SequenceTag));
            }

            public byte[] ReadInteger()
            {
                return Read(IntegerTag);
            }

            public byte[] Read(byte tag)
            {
                if (PeekTag() != tag)
                    throw NotP256();
                _position++;

                int length = ReadLength();
                if (length > _data.Length - _position)
                    throw NotP256();

                byte[] value = new byte[length];
                Buffer.BlockCopy(_data, _position, value, 0, length);
                _position += length;
                return value;
            }

            private int ReadLength()
            {
                if (_position >= _data.Length)
                    throw NotP256();

                int first = _data[_position++];
                if (first < 0x80)
                    return first;

                int count = first & 0x7F;
                if (count == 0 || count > 3 || count > _data.Length - _position)
                    throw NotP256();

                int length = 0;
                for (int i = 0; i < count; i++)
                    length = (length << 8) | _data[_position++];
                return length;
            }
        }
    }
}
