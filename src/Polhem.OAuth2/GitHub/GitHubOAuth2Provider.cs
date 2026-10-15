using System.Net.Http.Headers;
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
        /// <c>/user</c> has an email address only when the user made one public. Otherwise the primary address is read from
        /// <c>/user/emails</c>, if it is verified. That request needs the <c>user:email</c> scope, and when it fails the user
        /// information has no email address rather than the sign-in failing.
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
