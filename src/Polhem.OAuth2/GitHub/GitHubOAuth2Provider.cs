using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Polhem.OAuth2
{
    /// <summary>
    /// The GitHub OAuth2 provider.
    /// </summary>
    internal sealed class GitHubOAuth2Provider : OAuth2Provider
    {
        // https://docs.github.com/en/rest/about-the-rest-api/api-versions lists the supported versions and when each one ends.
        internal const string ApiVersion = "2022-11-28";

        /// <summary>
        /// Initializes a new instance of the <see cref="GitHubOAuth2Provider"/> class.
        /// </summary>
        /// <param name="options">The GitHub OAuth2 options.</param>
        /// <param name="httpClientFactory">Returns the HTTP client for a request to the provider, or null to use a shared instance.</param>
        public GitHubOAuth2Provider(GitHubOAuth2Options options, Func<HttpClient>? httpClientFactory)
            : base(options, httpClientFactory)
        {
        }

        /// <inheritdoc/>
        public override string ProviderName => "GitHub";

        /// <inheritdoc/>
        /// <remarks>
        /// GitHub documents the client secret as required for the code exchange and does not say that PKCE replaces it, so a
        /// secret that is set is sent from a public client as well, as for Google and LINE.
        /// </remarks>
        protected override bool RequiresClientSecret => true;

        /// <inheritdoc/>
        /// <remarks>
        /// <para>
        /// <c>/user</c> has an email address only when the user made one public, which GitHub allows only for a verified
        /// address. Otherwise the primary address is read from <c>/user/emails</c>, if it is verified. That request needs
        /// the <c>user:email</c> scope. When it returns an unsuccessful status, fails, times out, or returns a body that is
        /// not JSON, the user information has no email address rather than the sign-in failing.
        /// </para>
        /// <para>
        /// <see cref="UserInfo.RawJson"/> is the response of <c>/user</c>, so it does not contain an address read from
        /// <c>/user/emails</c>.
        /// </para>
        /// </remarks>
        public override async Task<UserInfo> GetUserInfoAsync(TokenResponse token, CancellationToken cancellationToken)
        {
            var user = await base.GetUserInfoAsync(token, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(user.Email))
                return user;

            string? email = await GetPrimaryEmailAsync(token.AccessToken, cancellationToken).ConfigureAwait(false);
            return email is null ? user : new UserInfo(user.UserId, user.UserName, email, user.RawJson);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <para>
        /// GitHub checks a token only for the application whose client ID and client secret authenticate the request, so the
        /// check uses <see cref="OAuth2Options.ClientId"/> and <see cref="OAuth2Options.ClientSecret"/>. A token issued to a
        /// client in <see cref="OAuth2Options.AcceptedClientIds"/> is not found and refused: the front end and the back end
        /// use the same GitHub application.
        /// </para>
        /// <para>The token travels in a JSON body, not in the URL.</para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// The options have no client secret, or GitHub refused the client ID and client secret.
        /// </exception>
        protected override async Task<IReadOnlyCollection<string>> GetTokenClientIdsAsync(string accessToken, CancellationToken cancellationToken)
        {
            if (Options.ClientSecret is not { Length: > 0 } clientSecret)
            {
                throw new InvalidOperationException(
                    $"GitHub checks an access token with the client secret of the application, so {nameof(OAuth2Options.ClientSecret)} must be set.");
            }

            string url = GetCheckTokenUrl();
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Content = CreateCheckTokenBody(accessToken);
                string credentials = Options.ClientId + ":" + clientSecret;
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));
                PrepareUserInfoRequest(request);

                using (var response = await SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    // GitHub answers 404 both for a token of another application and for a token that is not valid.
                    if (response.StatusCode == HttpStatusCode.NotFound)
                        throw new OAuth2Exception("GitHub reports that the access token is not valid, or was not issued to this application.");
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                        throw new InvalidOperationException("GitHub refused the client ID and client secret of the application.");
                    if (!response.IsSuccessStatusCode)
                        throw new HttpRequestException($"The token verification endpoint returned status code {(int)response.StatusCode}.");

                    string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    using (var document = OAuth2Json.ParseObject(json))
                    {
                        if (!document.RootElement.TryGetProperty("app", out var app) || app.ValueKind != JsonValueKind.Object
                            || OAuth2Json.GetProtocolString(app, "client_id") is not { } clientId)
                        {
                            return Array.Empty<string>();
                        }
                        // The request named ClientId, so a response for any other client, even an accepted one, is refused.
                        if (!string.Equals(clientId, Options.ClientId, StringComparison.Ordinal))
                            throw new OAuth2Exception("GitHub reported a client other than the one the check was made for.");
                        return new[] { clientId };
                    }
                }
            }
        }

        /// <inheritdoc/>
        protected override void PrepareUserInfoRequest(HttpRequestMessage request)
        {
            // https://docs.github.com/en/rest/using-the-rest-api/getting-started-with-the-rest-api
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.Add("X-GitHub-Api-Version", ApiVersion);
        }

        /// <inheritdoc/>
        protected override UserInfo CreateUserInfo(JsonElement user, string json, TokenResponse? token)
        {
            // The numeric id identifies the account for good. The login can be renamed, and then registered by someone else.
            return new UserInfo(
                OAuth2Json.GetString(user, "id"),
                OAuth2Json.GetString(user, "name") ?? OAuth2Json.GetString(user, "login"),
                OAuth2Json.GetString(user, "email"),
                json);
        }

        private static ByteArrayContent CreateCheckTokenBody(string accessToken)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream))
                {
                    writer.WriteStartObject();
                    writer.WriteString("access_token", accessToken);
                    writer.WriteEndObject();
                }
                var content = new ByteArrayContent(stream.ToArray());
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                return content;
            }
        }

        // https://docs.github.com/en/rest/apps/oauth-applications#check-a-token. The endpoint sits at the root of the API that
        // serves the user information, https://api.github.com/user, so a GitHub Enterprise Server API is followed as well.
        private string GetCheckTokenUrl()
        {
            const string UserPath = "/user";
            string endpoint = Options.UserInfoEndpoint;
            int query = endpoint.IndexOf('?');
            string path = (query < 0 ? endpoint : endpoint.Substring(0, query)).TrimEnd('/');
            if (!path.EndsWith(UserPath, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"The GitHub token check is found next to {nameof(OAuth2Options.UserInfoEndpoint)}, which must end with {UserPath}.");
            }
            return path.Substring(0, path.Length - UserPath.Length) + "/applications/" + Uri.EscapeDataString(Options.ClientId) + "/token";
        }

        // The address list sits next to the user information endpoint: https://api.github.com/user/emails.
        private string GetEmailsUrl()
        {
            string endpoint = Options.UserInfoEndpoint;
            int query = endpoint.IndexOf('?');
            string path = query < 0 ? endpoint : endpoint.Substring(0, query);
            string url = path.TrimEnd('/') + "/emails" + (query < 0 ? string.Empty : endpoint.Substring(query));
            // A user with more addresses than one page holds would otherwise have the primary one left out.
            return AppendQuery(url, "per_page=100");
        }

        private async Task<string?> GetPrimaryEmailAsync(string accessToken, CancellationToken cancellationToken)
        {
            try
            {
                using (var request = CreateUserInfoRequest(GetEmailsUrl(), accessToken))
                using (var response = await SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    // 403 or 404 when the user did not grant the user:email scope.
                    if (!response.IsSuccessStatusCode)
                        return null;

                    string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    return ReadPrimaryEmail(json);
                }
            }
            catch (HttpRequestException)
            {
                return null;
            }
            catch (JsonException)
            {
                return null;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // A timeout. Cancellation by the caller still propagates.
                return null;
            }
        }

        private static string? ReadPrimaryEmail(string json)
        {
            using (var document = JsonDocument.Parse(json))
            {
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                    return null;

                foreach (var address in document.RootElement.EnumerateArray())
                {
                    if (address.ValueKind == JsonValueKind.Object
                        && IsTrue(address, "primary")
                        && IsTrue(address, "verified")
                        && OAuth2Json.GetProtocolString(address, "email") is { Length: > 0 } email)
                    {
                        return email;
                    }
                }
                return null;
            }
        }

        private static bool IsTrue(JsonElement obj, string propertyName)
        {
            return obj.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.True;
        }
    }
}
