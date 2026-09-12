using System.Text;
using Microsoft.AspNetCore.Http;

namespace Polhem.OAuth2.AspNetCore
{
    /// <summary>
    /// Keeps the state in a cookie and the PKCE code verifier in session state of the current ASP.NET Core request.
    /// Each method does nothing, or returns null, when there is no current HTTP context.
    /// </summary>
    public class StateStorage : IStateStorage
    {
        private const string StateKey = "_StateKey";
        private const string CodeVerifierKey = "_CodeVerifierKey";
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="StateStorage"/> class.
        /// </summary>
        /// <param name="httpContextAccessor">Provides the current HTTP context.</param>
        public StateStorage(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        /// <inheritdoc/>
        /// <remarks>The cookie is HTTP-only, sent only over HTTPS, and expires after 10 minutes.</remarks>
        public void SaveState(string value)
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return;

            // NOTE: A GET redirect back from the provider also carries a SameSite=Lax cookie.
            // SameSite=None is only needed when the callback arrives as a cross-site POST.
            context.Response.Cookies.Append(StateKey, value, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = DateTimeOffset.UtcNow.AddMinutes(10)
            });
        }

        /// <inheritdoc/>
        public string? GetState()
        {
            var context = _httpContextAccessor.HttpContext;
            return context?.Request.Cookies[StateKey];
        }

        /// <inheritdoc/>
        public void RemoveState()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return;

            context.Response.Cookies.Delete(StateKey);
        }

        /// <inheritdoc/>
        public void SaveCodeVerifier(string codeVerifier)
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return;

            context.Session.Set(CodeVerifierKey, Encoding.UTF8.GetBytes(codeVerifier));
        }

        /// <inheritdoc/>
        public string? GetCodeVerifier()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return null;

            if (context.Session.TryGetValue(CodeVerifierKey, out var codeVerifierBytes))
            {
                return Encoding.UTF8.GetString(codeVerifierBytes);
            }

            return null;
        }

        /// <inheritdoc/>
        public void RemoveCodeVerifier()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return;

            context.Session.Remove(CodeVerifierKey);
        }
    }
}
