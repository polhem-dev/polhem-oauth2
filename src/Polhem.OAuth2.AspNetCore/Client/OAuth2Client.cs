using Microsoft.AspNetCore.Http;

namespace Polhem.OAuth2.AspNetCore
{
    /// <summary>
    /// An OAuth2 client for ASP.NET Core applications. It keeps the state in a cookie and the PKCE code verifier in
    /// session state.
    /// </summary>
    public class OAuth2Client : BaseOAuth2Client
    {
        private StateStorage? _stateStorage;
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="OAuth2Client"/> class.
        /// </summary>
        /// <param name="options">The OAuth2 options. Their type selects the provider.</param>
        /// <param name="httpContextAccessor">Provides the current HTTP context.</param>
        public OAuth2Client(OAuth2Options options, IHttpContextAccessor httpContextAccessor) : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        /// <inheritdoc/>
        public override IStateStorage StateStorage
        {
            get
            {
                return _stateStorage ??= new StateStorage(_httpContextAccessor);
            }
        }
    }
}
