using System.Security.Cryptography;
using System.Web;

namespace Polhem.OAuth2.AspNet
{
    /// <summary>
    /// Registers OAuth2 clients and runs the authorization code flow for ASP.NET applications on System.Web.
    /// </summary>
    public static class OAuth2Manager
    {
        private static Dictionary<string, OAuth2Client> Clients { get; } = [];

        /// <summary>
        /// Registers an OAuth2 client under a name, replacing any client registered earlier under the same name.
        /// </summary>
        /// <param name="clientName">The name that identifies the client.</param>
        /// <param name="client">The OAuth2 client.</param>
        /// <exception cref="ArgumentException"><paramref name="clientName"/> is null, empty or white space.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="client"/> is null.</exception>
        public static void RegisterClient(string clientName, OAuth2Client client)
        {
            if (string.IsNullOrWhiteSpace(clientName))
                throw new ArgumentException("Client name cannot be null or empty.", nameof(clientName));

            if (client == null)
                throw new ArgumentNullException(nameof(client), "OAuth2 client instance cannot be null.");

            Clients[clientName] = client;
        }

        /// <summary>
        /// Gets the client registered under a name.
        /// </summary>
        /// <param name="clientName">The name that identifies the client.</param>
        /// <returns>The client, or null if no client is registered under that name.</returns>
        public static OAuth2Client? GetClient(string clientName)
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
        public static string GetAuthorizationUrl(string clientName)
        {
            var client = GetClient(clientName) ?? throw new InvalidOperationException($"Client '{clientName}' is not registered.");
            var state = OAuth2StateCryptor.EncryptClientName(clientName);
            return client.GetAuthorizationUrl(state);
        }

        /// <summary>
        /// Redirects the current response to the authorization URL of a registered client.
        /// </summary>
        /// <param name="clientName">The name that identifies the client.</param>
        /// <exception cref="InvalidOperationException">No client is registered under <paramref name="clientName"/>.</exception>
        public static void RedirectToAuthorization(string clientName)
        {
            var authUrl = GetAuthorizationUrl(clientName);
            HttpContext.Current.Response.Redirect(authUrl);
        }

        /// <summary>
        /// Handles the OAuth2 callback of the current request: validates the state, exchanges the authorization code
        /// and retrieves the user information.
        /// </summary>
        /// <returns>A successful result, or a failed result that carries the exception.</returns>
        /// <remarks>
        /// A state that is missing, cannot be decoded, fails authentication or does not match becomes a failed result,
        /// in addition to the failures described on <see cref="BaseOAuth2Client.ValidateAuthorization"/>.
        /// Any other exception propagates to the caller.
        /// </remarks>
        public static async Task<AuthorizationResult> ValidateAuthorization()
        {
            string? code = HttpContext.Current.Request.QueryString["code"];
            string? state = HttpContext.Current.Request.QueryString["state"];

            try
            {
                if (state is not { } returnedState || string.IsNullOrWhiteSpace(returnedState))
                    throw new OAuth2Exception("The state is missing.");

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
