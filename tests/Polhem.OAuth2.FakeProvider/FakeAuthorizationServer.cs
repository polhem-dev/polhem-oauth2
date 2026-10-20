using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace Polhem.OAuth2.FakeProvider
{
    /// <summary>
    /// The authorization, token and user information endpoints of Auth0, reduced to what a sign-in needs. The authorization
    /// endpoint consents without a page, so a browser that follows the redirects completes the sign-in on its own.
    /// </summary>
    /// <remarks>
    /// Under <c>/github</c> the same server answers the way GitHub does: the token endpoint reports an error with status 200,
    /// answers in form encoding unless the request asks for JSON, and issues no refresh token; the API refuses a request
    /// without a <c>User-Agent</c> header; and <c>/user</c> has no email address, which <c>/user/emails</c> lists instead.
    /// Under <c>/oidc</c> it is an OpenID Connect provider with a discovery document and no user information endpoint, whose
    /// token endpoint returns the user in an ID token.
    /// </remarks>
    internal sealed class FakeAuthorizationServer
    {
        private const string AccessToken = "fake-access-token";
        private const string InvalidGrant = "invalid_grant";

        private readonly ConcurrentDictionary<string, FakeAuthorizationCode> _codes = new(StringComparer.Ordinal);
        private readonly string _origin;
        private readonly string _webClientId;
        private readonly string _webClientSecret;

        public FakeAuthorizationServer(string origin, string webClientId, string webClientSecret)
        {
            _origin = origin;
            _webClientId = webClientId;
            _webClientSecret = webClientSecret;
        }

        private string OidcIssuer => _origin + FakeProviderValues.OidcPath;

        public void Map(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/authorize", Authorize);
            endpoints.MapPost("/oauth/token", ExchangeAsync);
            endpoints.MapGet("/userinfo", GetUser);

            endpoints.MapGet("/github/login/oauth/authorize", Authorize);
            endpoints.MapPost("/github/login/oauth/access_token", ExchangeGitHubAsync);
            endpoints.MapGet("/github/user", GetGitHubUser);
            endpoints.MapGet("/github/user/emails", GetGitHubEmails);

            endpoints.MapGet(FakeProviderValues.OidcPath + "/.well-known/openid-configuration", GetDiscoveryDocument);
            endpoints.MapPost(FakeProviderValues.OidcPath + "/token", ExchangeOidcAsync);
        }

        // The authorization endpoint is shared with the other modes. The document lists no user information endpoint.
        private IResult GetDiscoveryDocument()
        {
            return Results.Json(new Dictionary<string, object>
            {
                ["issuer"] = OidcIssuer,
                ["authorization_endpoint"] = _origin + "/authorize",
                ["token_endpoint"] = OidcIssuer + "/token",
                ["jwks_uri"] = OidcIssuer + "/jwks",
                ["response_types_supported"] = new[] { "code" },
                ["subject_types_supported"] = new[] { "public" },
                ["id_token_signing_alg_values_supported"] = new[] { "RS256" },
                ["code_challenge_methods_supported"] = new[] { "S256" },
                ["token_endpoint_auth_methods_supported"] = new[] { "client_secret_post" }
            });
        }

        private async Task<IResult> ExchangeOidcAsync(HttpRequest request)
        {
            var form = await request.ReadFormAsync(request.HttpContext.RequestAborted);
            string clientId = form["client_id"].ToString();
            if (Redeem(form, refreshable: true) is { } error)
                return TokenError(error);

            var token = CreateToken();
            token["id_token"] = CreateIdToken(clientId);
            return Results.Json(token);
        }

        // The library reads an ID token from the token endpoint without verifying its signature, so the signature is a placeholder.
        private string CreateIdToken(string clientId)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
            {
                ["iss"] = OidcIssuer,
                ["aud"] = clientId,
                ["sub"] = FakeProviderValues.UserId,
                ["name"] = FakeProviderValues.UserName,
                ["email"] = FakeProviderValues.Email,
                ["iat"] = now,
                ["exp"] = now + 300
            });
            return WebEncoders.Base64UrlEncode("""{"alg":"RS256","typ":"JWT"}"""u8.ToArray()) + "." + WebEncoders.Base64UrlEncode(payload) + ".fake-signature";
        }

        private IResult Authorize(HttpRequest request)
        {
            string clientId = request.Query["client_id"].ToString();
            string redirectUri = request.Query["redirect_uri"].ToString();
            string state = request.Query["state"].ToString();
            string? challenge = request.Query["code_challenge"].FirstOrDefault();

            if (request.Query["response_type"] != "code" || redirectUri.Length == 0 || !IsKnownClient(clientId))
                return Results.BadRequest("The authorization request is not valid.");
            if (challenge is not null && request.Query["code_challenge_method"] != "S256")
                return Results.BadRequest("Only the S256 code challenge method is supported.");

            if (clientId == FakeProviderValues.DeniedClientId)
            {
                return Results.Redirect(QueryHelpers.AddQueryString(redirectUri, new Dictionary<string, string?>
                {
                    ["error"] = "access_denied",
                    ["error_description"] = "The user declined.",
                    ["state"] = state
                }));
            }

            string code = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(24));
            _codes[code] = new FakeAuthorizationCode(clientId, redirectUri, challenge);
            return Results.Redirect(QueryHelpers.AddQueryString(redirectUri, new Dictionary<string, string?>
            {
                ["code"] = code,
                ["state"] = state
            }));
        }

        private async Task<IResult> ExchangeAsync(HttpRequest request)
        {
            var form = await request.ReadFormAsync(request.HttpContext.RequestAborted);
            return Redeem(form, refreshable: true) is { } error ? TokenError(error) : Results.Json(CreateToken());
        }

        private async Task<IResult> ExchangeGitHubAsync(HttpRequest request)
        {
            if (!HasUserAgent(request))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var form = await request.ReadFormAsync(request.HttpContext.RequestAborted);
            // GitHub's OAuth Apps issue no refresh token, so a refresh grant fails like any unknown grant.
            var fields = Redeem(form, refreshable: false) is { } error
                ? new Dictionary<string, string> { ["error"] = error, ["error_description"] = "The fake provider refused the request." }
                : new Dictionary<string, string> { ["access_token"] = AccessToken, ["token_type"] = "bearer", ["scope"] = "read:user,user:email" };

            // Status 200 in both cases, as GitHub answers.
            bool json = request.Headers.Accept.Any(value => value is not null && value.Contains("application/json", StringComparison.OrdinalIgnoreCase));
            return json
                ? Results.Json(fields)
                : Results.Text(QueryString.Create(fields!).ToUriComponent().TrimStart('?'), "application/x-www-form-urlencoded");
        }

        // Returns the error code of the token request, or null when it succeeds.
        private string? Redeem(IFormCollection form, bool refreshable)
        {
            string grantType = form["grant_type"].ToString();

            if (grantType == "refresh_token" && refreshable)
                return form["refresh_token"] == "fake-refresh-token" ? null : InvalidGrant;
            // GitHub does not require grant_type for the code exchange.
            if (grantType != "authorization_code" && (refreshable || grantType.Length > 0))
                return "unsupported_grant_type";

            // A code is used once, whatever the outcome, as RFC 6749 requires.
            if (!_codes.TryRemove(form["code"].ToString(), out var issued))
                return InvalidGrant;
            if (form["client_id"] != issued.ClientId || form["redirect_uri"] != issued.RedirectUri)
                return InvalidGrant;

            if (issued.CodeChallenge is { } challenge)
            {
                string verifier = form["code_verifier"].ToString();
                string expected = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
                if (verifier.Length == 0 || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(challenge)))
                    return InvalidGrant;
            }
            else if (issued.ClientId != _webClientId || form["client_secret"] != _webClientSecret)
            {
                return "invalid_client";
            }

            return null;
        }

        private static IResult GetUser(HttpRequest request)
        {
            if (request.Headers.Authorization != "Bearer " + AccessToken)
                return Results.Unauthorized();

            return Results.Json(new Dictionary<string, string>
            {
                ["sub"] = FakeProviderValues.UserId,
                ["name"] = FakeProviderValues.UserName,
                ["email"] = FakeProviderValues.Email
            });
        }

        private static IResult GetGitHubUser(HttpRequest request)
        {
            if (!HasUserAgent(request))
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (request.Headers.Authorization != "Bearer " + AccessToken)
                return Results.Unauthorized();

            // A user who keeps the email address private has none here.
            return Results.Json(new Dictionary<string, object?>
            {
                ["id"] = FakeProviderValues.GitHubUserId,
                ["login"] = FakeProviderValues.GitHubLogin,
                ["name"] = FakeProviderValues.UserName,
                ["email"] = null
            });
        }

        private static IResult GetGitHubEmails(HttpRequest request)
        {
            if (!HasUserAgent(request))
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (request.Headers.Authorization != "Bearer " + AccessToken)
                return Results.Unauthorized();

            // The primary address is not the first, and an unverified address comes before it, so a client has to look at both flags.
            return Results.Json(new[]
            {
                new Dictionary<string, object?> { ["email"] = "unverified@example.com", ["primary"] = false, ["verified"] = false, ["visibility"] = null },
                new Dictionary<string, object?> { ["email"] = "secondary@example.com", ["primary"] = false, ["verified"] = true, ["visibility"] = null },
                new Dictionary<string, object?> { ["email"] = FakeProviderValues.Email, ["primary"] = true, ["verified"] = true, ["visibility"] = "private" }
            });
        }

        private static bool HasUserAgent(HttpRequest request)
        {
            return !string.IsNullOrWhiteSpace(request.Headers.UserAgent);
        }

        private bool IsKnownClient(string clientId)
        {
            return clientId == FakeProviderValues.ClientId || clientId == FakeProviderValues.DeniedClientId || clientId == _webClientId;
        }

        private static Dictionary<string, object> CreateToken()
        {
            return new Dictionary<string, object>
            {
                ["access_token"] = AccessToken,
                ["token_type"] = "Bearer",
                ["expires_in"] = 3600,
                ["refresh_token"] = "fake-refresh-token"
            };
        }

        private static IResult TokenError(string error)
        {
            return Results.Json(new Dictionary<string, string> { ["error"] = error }, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
