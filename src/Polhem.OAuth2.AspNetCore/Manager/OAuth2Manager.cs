using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;

namespace Polhem.OAuth2.AspNetCore
{
    /// <summary>
    /// Registers OAuth2 clients and runs the authorization code flow for ASP.NET Core applications.
    /// </summary>
    public class OAuth2Manager
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private Dictionary<string, OAuth2Client> Clients { get; } = [];

        /// <summary>
        /// Initializes a new instance of the <see cref="OAuth2Manager"/> class.
        /// </summary>
        /// <param name="httpContextAccessor">Provides the current HTTP context.</param>
        public OAuth2Manager(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Registers an OAuth2 client under a name.
        /// </summary>
        /// <param name="clientName">The name that identifies the client.</param>
        /// <param name="client">The OAuth2 client.</param>
        /// <exception cref="ArgumentException"><paramref name="clientName"/> is null, empty or white space.</exception>
        /// <exception cref="InvalidOperationException">A client is already registered under <paramref name="clientName"/>.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="client"/> is null.</exception>
        public void RegisterClient(string clientName, OAuth2Client client)
        {
            if (string.IsNullOrWhiteSpace(clientName))
                throw new ArgumentException("Client name cannot be null or empty.", nameof(clientName));
            if (Clients.ContainsKey(clientName))
                throw new InvalidOperationException($"Client '{clientName}' is already registered.");
            Clients[clientName] = client ?? throw new ArgumentNullException(nameof(client), "OAuth2 client instance cannot be null.");
        }

        /// <summary>
        /// Gets the client registered under a name.
        /// </summary>
        /// <param name="clientName">The name that identifies the client.</param>
        /// <returns>The client, or null if no client is registered under that name.</returns>
        public OAuth2Client? GetClient(string clientName)
        {
            if (Clients.TryGetValue(clientName, out var client))
            {
                return client;
            }

            return null;
        }

        /// <summary>
        /// Builds the authorization URL for a registered client. The client name travels in the state, protected by
        /// <see cref="OAuth2StateCryptor"/>.
        /// </summary>
        /// <param name="clientName">The name that identifies the client.</param>
        /// <returns>The URL to send the user to.</returns>
        /// <exception cref="InvalidOperationException">No client is registered under <paramref name="clientName"/>.</exception>
        public string GetAuthorizationUrl(string clientName)
        {
            var client = GetClient(clientName) ?? throw new InvalidOperationException($"Client '{clientName}' is not registered.");
            var state = OAuth2StateCryptor.EncryptClientName(clientName);
            return client.GetAuthorizationUrl(state);
        }

        /// <summary>
        /// Redirects the current response to the authorization URL of a registered client.
        /// </summary>
        /// <param name="clientName">The name that identifies the client.</param>
        /// <exception cref="InvalidOperationException">
        /// No client is registered under <paramref name="clientName"/>, or there is no current HTTP context.
        /// </exception>
        public void RedirectToAuthorization(string clientName)
        {
            string authUrl = GetAuthorizationUrl(clientName);
            var context = _httpContextAccessor.HttpContext ?? throw new InvalidOperationException("There is no current HTTP context.");
            context.Response.Redirect(authUrl);
        }

        /// <summary>
        /// Handles the OAuth2 callback of the current request: validates the state, exchanges the authorization code
        /// and retrieves the user information.
        /// </summary>
        /// <returns>A successful result, or a failed result that carries the exception.</returns>
        /// <remarks>
        /// A missing authorization code, or a state that is missing, cannot be decoded, fails authentication or does not
        /// match, becomes a failed result, in addition to the failures described on
        /// <see cref="BaseOAuth2Client.ValidateAuthorization"/>. Any other exception propagates to the caller.
        /// </remarks>
        /// <exception cref="InvalidOperationException">There is no current HTTP request.</exception>
        public async Task<AuthorizationResult> ValidateAuthorization()
        {
            var request = _httpContextAccessor.HttpContext?.Request ?? throw new InvalidOperationException("There is no current HTTP request.");

            string? code = request.Query["code"];
            string? state = request.Query["state"];

            try
            {
                if (string.IsNullOrEmpty(code) || state is not { Length: > 0 } returnedState)
                    throw new OAuth2Exception("The authorization code or the state is missing.");

                var clientName = OAuth2StateCryptor.DecryptClientName(returnedState);
                var client = GetClient(clientName) ?? throw new OAuth2Exception("The state does not name a registered client.");
                if (!client.ValidateState(returnedState))
                    throw new OAuth2Exception("The state does not match the stored state.");

                return await client.ValidateAuthorization(code);
            }
            catch (OAuth2Exception ex)
            {
                return Failure(ex);
            }
            catch (CryptographicException ex)
            {
                return Failure(ex);
            }
        }

        private static AuthorizationResult Failure(Exception exception)
        {
            return new AuthorizationResult()
            {
                IsSuccess = false,
                Exception = exception
            };
        }
    }
}
