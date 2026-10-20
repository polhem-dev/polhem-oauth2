using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Polhem.OAuth2
{
    /// <summary>
    /// Reads the discovery document of an OpenID Connect provider (OpenID Connect Discovery 1.0) into options.
    /// </summary>
    internal static class OidcDiscovery
    {
        private const string DocumentPath = "/.well-known/openid-configuration";

        // Discovery documents of real providers are a few kilobytes. The limit keeps a provider that answers with a huge
        // body from exhausting the memory of the application.
        private const int MaxDocumentBytes = 1024 * 1024;

        private const string TooLargeMessage = "The discovery document is larger than 1 MiB.";

        /// <summary>
        /// Requests the discovery document of an authority and creates the options it describes.
        /// </summary>
        /// <param name="authority">The issuer identifier of the provider.</param>
        /// <param name="httpClient">The HTTP client for the request, or null to use a shared instance.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <returns>The options, with the endpoints and the issuer of the document.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="authority"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="authority"/> is not an absolute https URI without user information, a query or a fragment.</exception>
        /// <exception cref="HttpRequestException">The request failed, the provider returned an unsuccessful status code, or the document is too large.</exception>
        /// <exception cref="JsonException">The response is not a JSON object.</exception>
        /// <exception cref="OAuth2Exception">The document does not describe a provider that the options can use.</exception>
        /// <exception cref="OperationCanceledException">The request was canceled or timed out.</exception>
        public static async Task<OidcOAuth2Options> LoadAsync(string authority, HttpClient? httpClient, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(authority);
            if (!OidcOAuth2Options.IsIssuerUri(authority))
                throw new ArgumentException("The authority must be an absolute https URI without user information, a query or a fragment.", nameof(authority));

            // Section 4: the path is appended to the issuer after a trailing slash is removed.
            string documentUrl = TrimTrailingSlash(authority) + DocumentPath;
            if (httpClient is null)
                SharedHttpClient.UseEndpoint(documentUrl);

            var client = httpClient ?? SharedHttpClient.Instance;
            // The response is read after its headers, so that its size can be limited, and HttpClient.Timeout covers only the
            // headers then. A token that the timeout cancels covers the body as well.
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            using (var request = new HttpRequestMessage(HttpMethod.Get, documentUrl))
            {
                if (client.Timeout != Timeout.InfiniteTimeSpan)
                    timeout.CancelAfter(client.Timeout);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                string json;
                try
                {
                    using (var response = await LibraryUserAgent.SendAsync(client, request, timeout.Token, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode)
                            throw new HttpRequestException($"The discovery document request returned status code {(int)response.StatusCode}.");
                        json = await ReadBodyAsync(response, timeout.Token).ConfigureAwait(false);
                    }
                }
                // The caller's token is the one that a cancellation by the caller carries, and a timeout is reported the way
                // HttpClient reports its own, with a TimeoutException inside.
                catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException or IOException
                    && timeout.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new TaskCanceledException("The discovery document request timed out.", new TimeoutException(exception.Message, exception));
                }
                catch (IOException exception)
                {
                    // The connection failed while the body was read, which the request reports as HttpRequestException elsewhere.
                    throw new HttpRequestException("The discovery document could not be read.", exception);
                }

                using (var document = OAuth2Json.ParseObject(json))
                {
                    return Read(document.RootElement, authority);
                }
            }
        }

        // Reads the body as UTF-8, the encoding of JSON (RFC 8259, section 8.1), up to the limit, whether or not the response
        // states its length.
        private static async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            if (response.Content.Headers.ContentLength > MaxDocumentBytes)
                throw new HttpRequestException(TooLargeMessage);

            cancellationToken.ThrowIfCancellationRequested();
            // The registration also disposes the response, for a stream whose reads do not observe the token.
            using (cancellationToken.Register(response.Dispose))
            using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            using (var body = new MemoryStream())
            {
                byte[] buffer = new byte[16 * 1024];
                int read;
                while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    if (body.Length + read > MaxDocumentBytes)
                        throw new HttpRequestException(TooLargeMessage);
                    await body.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }
                return new UTF8Encoding(false).GetString(body.GetBuffer(), 0, (int)body.Length).TrimStart('\uFEFF');
            }
        }

        /// <summary>
        /// Creates the options that a discovery document describes, after checking that it belongs to the authority.
        /// </summary>
        /// <param name="document">The root object of the discovery document.</param>
        /// <param name="authority">The issuer identifier whose document it should be.</param>
        /// <returns>The options.</returns>
        /// <exception cref="OAuth2Exception">The document does not describe a provider that the options can use.</exception>
        internal static OidcOAuth2Options Read(JsonElement document, string authority)
        {
            // Section 4.3: the issuer must be the URL whose document was requested, or another site could pose as the provider.
            // Auth0 writes its issuer with a trailing slash and most other providers without one, so a single trailing slash
            // on either side does not count. The value of the document is kept as it is, because ID tokens carry it exactly.
            string issuer = OAuth2Json.GetProtocolString(document, "issuer") ?? throw MissingField("issuer");
            if (!string.Equals(TrimTrailingSlash(issuer), TrimTrailingSlash(authority), StringComparison.Ordinal))
                throw new OAuth2Exception($"The issuer of the discovery document does not match the authority '{authority}'.");

            if (GetStrings(document, "response_types_supported") is { } responseTypes && !responseTypes.Contains("code", StringComparer.Ordinal))
                throw new OAuth2Exception("The provider does not support the authorization code flow.");

            // The library always sends an S256 challenge when it uses PKCE, so a provider that lists only other methods would
            // refuse the sign-in. A provider that lists nothing may still support PKCE, and one that does not ignores it.
            if (GetStrings(document, "code_challenge_methods_supported") is { } challengeMethods && !challengeMethods.Contains("S256", StringComparer.Ordinal))
                throw new OAuth2Exception("The provider does not support the S256 code challenge method of PKCE.");

            var options = new OidcOAuth2Options
            {
                Issuer = issuer,
                AuthorizationEndpoint = OAuth2Json.GetProtocolString(document, "authorization_endpoint") ?? throw MissingField("authorization_endpoint"),
                TokenEndpoint = OAuth2Json.GetProtocolString(document, "token_endpoint") ?? throw MissingField("token_endpoint"),
                UserInfoEndpoint = OAuth2Json.GetProtocolString(document, "userinfo_endpoint") ?? string.Empty,
                // Section 3: a document that lists no methods supports client_secret_basic only.
                ClientAuthentication = GetStrings(document, "token_endpoint_auth_methods_supported") is { } methods
                    && methods.Contains("client_secret_post", StringComparer.Ordinal)
                        ? ClientAuthenticationMethod.ClientSecretPost
                        : ClientAuthenticationMethod.ClientSecretBasic
            };

            if (options.GetEndpointError() is { } error)
                throw new OAuth2Exception("The discovery document does not describe valid endpoints: " + error);
            return options;
        }

        // Returns the strings of an array field, or null when the field is missing. A field that is not an array of strings
        // makes the document invalid rather than missing, so that a malformed list cannot turn a check off.
        private static string[]? GetStrings(JsonElement document, string name)
        {
            if (!document.TryGetProperty(name, out var value))
                return null;
            if (value.ValueKind != JsonValueKind.Array || value.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
                throw new OAuth2Exception($"The discovery document has a '{name}' field that is not an array of strings.");
            return value.EnumerateArray().Select(item => item.GetString()!).ToArray();
        }

        private static OAuth2Exception MissingField(string name)
        {
            return new OAuth2Exception($"The discovery document has no '{name}' string.");
        }

        private static string TrimTrailingSlash(string value)
        {
            return value.Length > 0 && value[value.Length - 1] == '/' ? value.Substring(0, value.Length - 1) : value;
        }
    }
}
