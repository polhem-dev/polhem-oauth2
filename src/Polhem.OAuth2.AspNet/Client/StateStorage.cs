using System.Web;

namespace Polhem.OAuth2.AspNet
{
    /// <summary>
    /// Keeps the state in a cookie and the PKCE code verifier in session state of the current System.Web request.
    /// </summary>
    public class StateStorage : IStateStorage
    {
        private const string StateKey = "_StateKey";
        private const string CodeVerifierKey = "_CodeVerifierKey";

        /// <inheritdoc/>
        /// <remarks>The cookie is HTTP-only, sent only over HTTPS, and expires after 10 minutes.</remarks>
        public void SaveState(string value)
        {
            // NOTE: A GET redirect back from the provider also carries a SameSite=Lax cookie.
            // SameSite=None is only needed when the callback arrives as a cross-site POST.
            var cookie = new HttpCookie(StateKey, value)
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = DateTime.Now.Add(TimeSpan.FromMinutes(10))
            };
            HttpContext.Current.Response.Cookies.Add(cookie);
        }

        /// <inheritdoc/>
        public string? GetState()
        {
            return HttpContext.Current.Request.Cookies[StateKey]?.Value;
        }

        /// <inheritdoc/>
        public void RemoveState()
        {
            if (HttpContext.Current.Request.Cookies[StateKey] != null)
            {
                // Sending the cookie back with an expiry date in the past makes the browser delete it.
                var cookie = new HttpCookie(StateKey) { Expires = DateTime.Now.AddDays(-1) };
                HttpContext.Current.Response.Cookies.Add(cookie);
            }
        }

        /// <inheritdoc/>
        public void SaveCodeVerifier(string codeVerifier)
        {
            HttpContext.Current.Session[CodeVerifierKey] = codeVerifier;
        }

        /// <inheritdoc/>
        public string? GetCodeVerifier()
        {
            return HttpContext.Current.Session[CodeVerifierKey] as string;
        }

        /// <inheritdoc/>
        public void RemoveCodeVerifier()
        {
            HttpContext.Current.Session.Remove(CodeVerifierKey);
        }
    }
}
