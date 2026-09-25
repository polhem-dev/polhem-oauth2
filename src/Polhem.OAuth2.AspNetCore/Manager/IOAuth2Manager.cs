using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;

namespace Polhem.OAuth2.AspNetCore
{
    /// <summary>
    /// The operations of <see cref="OAuth2Manager"/>, for code that should not depend on the class itself.
    /// </summary>
    /// <remarks>
    /// <see cref="Microsoft.Extensions.DependencyInjection.OAuth2ServiceCollectionExtensions.AddOAuth2Client"/> registers
    /// <see cref="OAuth2Manager"/> as this interface as well. A controller that depends on the interface can be tested with a
    /// fake in its place, without a service provider or a provider to sign in to. Later versions may add members, so
    /// implement it only for tests.
    /// </remarks>
    public interface IOAuth2Manager
    {
        /// <inheritdoc cref="OAuth2Manager.GetClient"/>
        OAuth2Client? GetClient(string clientName);

        /// <inheritdoc cref="OAuth2Manager.CreateAuthorizationUrl"/>
        string CreateAuthorizationUrl(HttpContext context, string clientName);

        /// <inheritdoc cref="OAuth2Manager.RedirectToAuthorization"/>
        void RedirectToAuthorization(HttpContext context, string clientName);

        /// <inheritdoc cref="OAuth2Manager.CompleteAuthorizationAsync"/>
        Task<AuthorizationResult> CompleteAuthorizationAsync(HttpContext context, CancellationToken cancellationToken = default);

        /// <inheritdoc cref="OAuth2Manager.RedirectToAppAuthorization"/>
        void RedirectToAppAuthorization(HttpContext context, string clientName, string appRedirectUri, string codeChallenge);

        /// <inheritdoc cref="OAuth2Manager.CreateAppAuthorizationUrl"/>
        string CreateAppAuthorizationUrl(HttpContext context, string clientName, string appRedirectUri, string codeChallenge);

        /// <inheritdoc cref="OAuth2Manager.TryRedirectToAppAuthorization"/>
        bool TryRedirectToAppAuthorization(HttpContext context, string? clientName, string? appRedirectUri, string? codeChallenge);

        /// <inheritdoc cref="OAuth2Manager.TryCreateAppAuthorizationUrl"/>
        bool TryCreateAppAuthorizationUrl(
            HttpContext context, string? clientName, string? appRedirectUri, string? codeChallenge, [NotNullWhen(true)] out string? url);

        /// <inheritdoc cref="OAuth2Manager.RedirectToAppAsync"/>
        Task<bool> RedirectToAppAsync(HttpContext context, AuthorizationResult result, CancellationToken cancellationToken = default);

        /// <inheritdoc cref="OAuth2Manager.CreateAppRedirectUrlAsync"/>
        Task<string?> CreateAppRedirectUrlAsync(HttpContext context, AuthorizationResult result, CancellationToken cancellationToken = default);

        /// <inheritdoc cref="OAuth2Manager.RedeemAppCodeAsync"/>
        Task<UserInfo?> RedeemAppCodeAsync(string clientName, string code, string codeVerifier, CancellationToken cancellationToken = default);
    }
}
