using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Polhem.OAuth2
{
    /// <summary>
    /// Creates the client secret of Sign in with Apple: a JSON Web Token signed with ES256 by the private key of a Sign in
    /// with Apple key, and keeps it until shortly before it expires.
    /// </summary>
    /// <remarks>
    /// Apple refuses a secret that expires more than 15777000 seconds, about six months, after it is created. A secret that
    /// lives an hour keeps the damage small if one ends up in a log, and signing one costs far less than the request it is
    /// sent with.
    /// </remarks>
    internal sealed class AppleClientSecret
    {
        /// <summary>
        /// How long a created secret is valid.
        /// </summary>
        public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

        /// <summary>
        /// How long before it expires a secret is replaced, so that a secret never expires on its way to Apple.
        /// </summary>
        public static readonly TimeSpan RenewalMargin = TimeSpan.FromMinutes(10);

        private const string Audience = "https://appleid.apple.com";

        private readonly string _teamId;
        private readonly string _keyId;
        private readonly string _clientId;
        private readonly string _privateKey;
        private readonly Func<DateTimeOffset> _clock;
        private volatile Secret? _current;

        /// <summary>
        /// Initializes a new instance of the <see cref="AppleClientSecret"/> class.
        /// </summary>
        /// <param name="teamId">The Team ID, the issuer of the secret.</param>
        /// <param name="keyId">The Key ID of the private key.</param>
        /// <param name="clientId">The Services ID or App ID, the subject of the secret.</param>
        /// <param name="privateKey">The PEM text of the <c>.p8</c> file, which <see cref="AppleSigningKey.GetError"/> accepts.</param>
        /// <param name="clock">Returns the current time.</param>
        public AppleClientSecret(string teamId, string keyId, string clientId, string privateKey, Func<DateTimeOffset> clock)
        {
            _teamId = teamId;
            _keyId = keyId;
            _clientId = clientId;
            _privateKey = privateKey;
            _clock = clock;
        }

        /// <summary>
        /// Gets a secret that is valid for at least <see cref="RenewalMargin"/>, creating a new one when the current one is
        /// about to expire.
        /// </summary>
        /// <returns>The secret.</returns>
        /// <remarks>
        /// Concurrent calls may each create a secret when the current one runs out. Every one of them is valid, so the
        /// last one written is kept and nothing is locked.
        /// </remarks>
        public string GetValue()
        {
            DateTimeOffset now = _clock();
            var current = _current;
            if (current is not null && current.Expires - now > RenewalMargin && current.IssuedAt <= now)
                return current.Value;

            var created = new Secret(Create(now), now, now + Lifetime);
            _current = created;
            return created.Value;
        }

        private string Create(DateTimeOffset now)
        {
            string signingInput = Encode(writer =>
            {
                writer.WriteString("alg", "ES256");
                writer.WriteString("kid", _keyId);
            }) + "." + Encode(writer =>
            {
                writer.WriteString("iss", _teamId);
                writer.WriteNumber("iat", now.ToUnixTimeSeconds());
                writer.WriteNumber("exp", (now + Lifetime).ToUnixTimeSeconds());
                writer.WriteString("aud", Audience);
                writer.WriteString("sub", _clientId);
            });

            using (ECDsa key = AppleSigningKey.Create(_privateKey))
            {
                // IMPORTANT: A JSON Web Signature carries the ES256 signature as r and s of 32 bytes each (RFC 7518, section
                // 3.4). SignData produces that IEEE P1363 form on every target, not the DER form that X.509 uses.
                byte[] signature = key.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256);
                return signingInput + "." + Base64Url.Encode(signature);
            }
        }

        private static string Encode(Action<Utf8JsonWriter> writeProperties)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream))
                {
                    writer.WriteStartObject();
                    writeProperties(writer);
                    writer.WriteEndObject();
                }
                return Base64Url.Encode(stream.ToArray());
            }
        }

        private sealed class Secret
        {
            public Secret(string value, DateTimeOffset issuedAt, DateTimeOffset expires)
            {
                Value = value;
                IssuedAt = issuedAt;
                Expires = expires;
            }

            public string Value { get; }

            public DateTimeOffset IssuedAt { get; }

            public DateTimeOffset Expires { get; }
        }
    }
}
