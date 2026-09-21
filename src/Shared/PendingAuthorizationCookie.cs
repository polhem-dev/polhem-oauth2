using System.Text.Json;

namespace Polhem.OAuth2
{
    /// <summary>
    /// Names and encodes the cookie that keeps a pending sign-in of a web application between the redirect to the provider
    /// and the callback.
    /// </summary>
    /// <remarks>
    /// Both web packages compile this file, so they share one format. Each package protects the encoded bytes with the data
    /// protection of its platform before they reach the browser.
    /// </remarks>
    internal static class PendingAuthorizationCookie
    {
        /// <summary>
        /// The prefix of the cookie name. Browsers accept a cookie whose name starts with <c>__Host-</c> only when it is
        /// Secure, has the path <c>/</c> and names no domain, so another host of the same site cannot set it.
        /// </summary>
        public const string NamePrefix = "__Host-oauth2.";

        /// <summary>
        /// The purpose that keeps the protected payload apart from other data protected with the same keys. The version
        /// changes when the format changes in a way that an earlier version cannot read. An optional value, as the relay added
        /// in 1.1.0, does not change it, so that a cookie written by one version is still read by the other during an upgrade.
        /// </summary>
        public const string ProtectionPurpose = "Polhem.OAuth2.PendingAuthorization.v1";

        private const int MaxStateLength = 256;
        private const long MaxUnixSeconds = 253402300799;
        private const string InvalidMessage = "The pending sign-in cookie is not valid.";

        /// <summary>
        /// How long a sign-in may take from the redirect to the provider until the callback.
        /// </summary>
        public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

        // Tolerates a clock that runs slightly behind on another server of the same application.
        private static readonly TimeSpan s_clockSkew = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Gets the name of the cookie that keeps the sign-in with a given state.
        /// </summary>
        /// <param name="state">The state of the sign-in.</param>
        /// <returns>
        /// The cookie name, or null when the state is empty, longer than 256 characters, or has a character outside base64url,
        /// which the states created by <see cref="OAuth2Client"/> never do.
        /// </returns>
        public static string? GetName(string? state)
        {
            if (state is null || state.Length == 0 || state.Length > MaxStateLength)
                return null;

            return Base64UrlText.IsBase64Url(state) ? NamePrefix + state : null;
        }

        /// <summary>
        /// Encodes a pending sign-in, before it is protected.
        /// </summary>
        /// <param name="clientName">The name the client is registered under.</param>
        /// <param name="pending">The values to keep until the callback.</param>
        /// <param name="issuedAt">When the sign-in started.</param>
        /// <param name="appRedirectUri">
        /// For a sign-in relayed to an application (ADR-006), the application redirect URI to return to; otherwise null.
        /// </param>
        /// <param name="appCodeChallenge">For a relayed sign-in, the S256 code challenge of the application; otherwise null.</param>
        /// <returns>The encoded bytes.</returns>
        public static byte[] Serialize(
            string clientName, PendingAuthorization pending, DateTimeOffset issuedAt, string? appRedirectUri = null, string? appCodeChallenge = null)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream))
                {
                    writer.WriteStartObject();
                    writer.WriteString("client", clientName);
                    writer.WriteString("state", pending.State);
                    if (pending.CodeVerifier is { } codeVerifier)
                        writer.WriteString("verifier", codeVerifier);
                    writer.WriteString("redirectUri", pending.RedirectUri);
                    writer.WriteNumber("issuedAt", issuedAt.ToUnixTimeSeconds());
                    if (appRedirectUri is not null && appCodeChallenge is not null)
                    {
                        writer.WriteString("appRedirectUri", appRedirectUri);
                        writer.WriteString("appChallenge", appCodeChallenge);
                    }
                    writer.WriteEndObject();
                }
                return stream.ToArray();
            }
        }

        /// <summary>
        /// Decodes a pending sign-in, after it is unprotected.
        /// </summary>
        /// <param name="data">The bytes written by <see cref="Serialize"/>.</param>
        /// <param name="now">The current time.</param>
        /// <returns>The name of the client and the pending values.</returns>
        /// <exception cref="OAuth2Exception">
        /// The data does not hold a pending sign-in, or the sign-in started longer ago than <see cref="Lifetime"/>.
        /// </exception>
        public static (string ClientName, PendingAuthorization Pending) Deserialize(byte[] data, DateTimeOffset now)
        {
            return Deserialize(data, now, out _, out _);
        }

        /// <summary>
        /// Decodes a pending sign-in, after it is unprotected, together with the application it is relayed to, if any.
        /// </summary>
        /// <param name="data">The bytes written by <see cref="Serialize"/>.</param>
        /// <param name="now">The current time.</param>
        /// <param name="appRedirectUri">The application redirect URI of a relayed sign-in, or null for a web sign-in.</param>
        /// <param name="appCodeChallenge">The code challenge of a relayed sign-in, or null for a web sign-in.</param>
        /// <returns>The name of the client and the pending values.</returns>
        /// <exception cref="OAuth2Exception">
        /// The data does not hold a pending sign-in, or the sign-in started longer ago than <see cref="Lifetime"/>.
        /// </exception>
        public static (string ClientName, PendingAuthorization Pending) Deserialize(
            byte[] data, DateTimeOffset now, out string? appRedirectUri, out string? appCodeChallenge)
        {
            string? clientName;
            string? state;
            string? codeVerifier;
            string? redirectUri;
            long issuedAt;
            try
            {
                using (var document = JsonDocument.Parse(data))
                {
                    var root = document.RootElement;
                    if (root.ValueKind != JsonValueKind.Object
                        || !root.TryGetProperty("issuedAt", out var issued)
                        || !(issued.ValueKind == JsonValueKind.Number && issued.TryGetInt64(out issuedAt)))
                    {
                        throw new OAuth2Exception(InvalidMessage);
                    }

                    clientName = root.GetStringProperty("client");
                    state = root.GetStringProperty("state");
                    codeVerifier = root.GetStringProperty("verifier");
                    redirectUri = root.GetStringProperty("redirectUri");
                    appRedirectUri = root.GetStringProperty("appRedirectUri");
                    appCodeChallenge = root.GetStringProperty("appChallenge");
                }
            }
            catch (JsonException ex)
            {
                throw new OAuth2Exception(InvalidMessage, ex);
            }

            if (clientName is not { Length: > 0 } || state is not { Length: > 0 } || redirectUri is not { Length: > 0 }
                || issuedAt <= 0 || issuedAt > MaxUnixSeconds
                || (appRedirectUri is { Length: > 0 }) != (appCodeChallenge is { Length: > 0 }))
            {
                throw new OAuth2Exception(InvalidMessage);
            }

            TimeSpan age = now - DateTimeOffset.FromUnixTimeSeconds(issuedAt);
            if (age > Lifetime || age < -s_clockSkew)
                throw new OAuth2Exception("The sign-in was started too long ago. Start it again.");

            return (clientName, new PendingAuthorization(state, codeVerifier, redirectUri));
        }
    }
}
