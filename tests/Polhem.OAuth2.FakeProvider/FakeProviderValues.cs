namespace Polhem.OAuth2.FakeProvider
{
    /// <summary>
    /// The values that the fake provider and the end-to-end device tests share. The device tests compile this file too.
    /// </summary>
    internal static class FakeProviderValues
    {
        /// <summary>
        /// The client that the fake provider signs in without asking.
        /// </summary>
        public const string ClientId = "fake-app";

        /// <summary>
        /// The client that the fake provider sends back with <c>access_denied</c>, as when a user declines.
        /// </summary>
        public const string DeniedClientId = "fake-app-denied";

        /// <summary>
        /// The name under which the relay registers its web client.
        /// </summary>
        public const string RelayClientName = "Fake";

        /// <summary>
        /// The name under which the relay registers its web client for the Sign in with Apple mode under <c>/apple</c>,
        /// which returns to the callback with a form POST.
        /// </summary>
        public const string AppleRelayClientName = "FakeApple";

        /// <summary>
        /// The path under which the fake provider answers the way Sign in with Apple does.
        /// </summary>
        public const string ApplePath = "/apple";

        /// <summary>
        /// The callback scheme that the device test application registers.
        /// </summary>
        public const string AppScheme = "dev.polhem.oauth2.devicetests";

        /// <summary>
        /// The redirect URI of a direct sign-in from the device test application.
        /// </summary>
        public const string AppRedirectUri = AppScheme + ":/oauth2redirect";

        /// <summary>
        /// The redirect URI through which the relay returns to the device test application.
        /// </summary>
        public const string RelayRedirectUri = AppScheme + ":/relay";

        /// <summary>
        /// The path under which the fake provider is an OpenID Connect provider with a discovery document. The issuer is
        /// the origin followed by this path.
        /// </summary>
        public const string OidcPath = "/oidc";

        public const string UserId = "fake-user-1";
        public const string UserName = "Fake User";
        public const string Email = "fake.user@example.com";

        /// <summary>
        /// The numeric account ID under <c>/github</c>, where the user has no public email address.
        /// </summary>
        public const long GitHubUserId = 1234567;

        public const string GitHubLogin = "fake-user";
    }
}
