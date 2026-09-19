using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Polhem.OAuth2.FakeProvider
{
    /// <summary>
    /// The authorization, token and user information endpoints of Auth0, reduced to what a sign-in needs. The authorization
    /// endpoint consents without a page, so a browser that follows the redirects completes the sign-in on its own.
    /// </summary>
    internal sealed class FakeAuthorizationServer
    {
        private const string AccessToken = "fake-access-token";

        private readonly ConcurrentDictionary<string, FakeAuthorizationCode> _codes = new(StringComparer.Ordinal);
        private readonly string _webClientId;
        private readonly string _webClientSecret;

        public FakeAuthorizationServer(string webClientId, string webClientSecret)
        {
            _webClientId = webClientId;
            _webClientSecret = webClientSecret;
        }

        public void Map(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/authorize", Authorize);
            endpoints.MapPost("/oauth/token", ExchangeAsync);
            endpoints.MapGet("/userinfo", GetUser);
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

            string code = Base64Url(RandomNumberGenerator.GetBytes(24));
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
            string grantType = form["grant_type"].ToString();

            if (grantType == "refresh_token")
            {
                return form["refresh_token"] == "fake-refresh-token"
                    ? Results.Json(CreateToken())
                    : TokenError("invalid_grant");
            }
            if (grantType != "authorization_code")
                return TokenError("unsupported_grant_type");

            // A code is used once, whatever the outcome, as RFC 6749 requires.
            if (!_codes.TryRemove(form["code"].ToString(), out var issued))
                return TokenError("invalid_grant");
            if (form["client_id"] != issued.ClientId || form["redirect_uri"] != issued.RedirectUri)
                return TokenError("invalid_grant");

            if (issued.CodeChallenge is { } challenge)
            {
                string verifier = form["code_verifier"].ToString();
                string expected = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
                if (verifier.Length == 0 || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(challenge)))
                    return TokenError("invalid_grant");
            }
            else if (issued.ClientId != _webClientId || form["client_secret"] != _webClientSecret)
            {
                return TokenError("invalid_client");
            }

            return Results.Json(CreateToken());
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

        private static string Base64Url(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}
