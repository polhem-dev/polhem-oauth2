namespace Polhem.OAuth2
{
    // Compiled by Polhem.OAuth2.AspNetCore only, which has the back-end relay of ADR-006.
    internal static partial class PendingAuthorizationCookie
    {
        /// <summary>
        /// Encodes a pending sign-in relayed to an application, before it is protected.
        /// </summary>
        /// <param name="clientName">The name the client is registered under.</param>
        /// <param name="pending">The values to keep until the callback.</param>
        /// <param name="issuedAt">When the sign-in started.</param>
        /// <param name="appRedirectUri">The application redirect URI to return to.</param>
        /// <param name="appCodeChallenge">The S256 code challenge of the application.</param>
        /// <returns>The encoded bytes.</returns>
        public static byte[] SerializeRelayed(
            string clientName, PendingAuthorization pending, DateTimeOffset issuedAt, string appRedirectUri, string appCodeChallenge)
        {
            return Write(clientName, pending, issuedAt, appRedirectUri, appCodeChallenge);
        }
    }
}
