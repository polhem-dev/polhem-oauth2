namespace Polhem.OAuth2.AspNet
{
    /// <summary>
    /// An OAuth2 client for ASP.NET applications on System.Web. It keeps the state in a cookie and the PKCE code
    /// verifier in session state.
    /// </summary>
    public class OAuth2Client : BaseOAuth2Client
    {
        private StateStorage? _stateStorage;

        /// <summary>
        /// Initializes a new instance of the <see cref="OAuth2Client"/> class.
        /// </summary>
        /// <param name="options">The OAuth2 options. Their type selects the provider.</param>
        public OAuth2Client(OAuth2Options options) : base(options)
        {
        }

        /// <inheritdoc/>
        public override IStateStorage StateStorage
        {
            get
            {
                return _stateStorage ??= new StateStorage();
            }
        }
    }
}
