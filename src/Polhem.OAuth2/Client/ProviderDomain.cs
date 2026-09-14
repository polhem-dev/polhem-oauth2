namespace Polhem.OAuth2
{
    /// <summary>
    /// Normalizes the domain of a provider tenant, from which the endpoints of Auth0 and Okta are built.
    /// </summary>
    internal static class ProviderDomain
    {
        /// <summary>
        /// Normalizes a domain such as <c>tenant.auth0.com</c> or <c>https://tenant.auth0.com/</c>.
        /// </summary>
        /// <param name="domain">The domain, with or without the https scheme and a trailing slash.</param>
        /// <returns>
        /// The host name, followed by the port when it is not 443, or an empty string when <paramref name="domain"/> is null,
        /// empty or white space.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="domain"/> has a scheme other than https, or includes user information, a path, a query or a fragment.
        /// </exception>
        public static string Normalize(string? domain)
        {
            string value = domain?.Trim() ?? string.Empty;
            if (value.Length == 0)
                return string.Empty;

            string candidate = value.IndexOf("://", StringComparison.Ordinal) >= 0 ? value : Uri.UriSchemeHttps + "://" + value;
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
                || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
                || uri.UserInfo.Length != 0
                || !string.Equals(uri.AbsolutePath, "/", StringComparison.Ordinal)
                || uri.Query.Length != 0
                || uri.Fragment.Length != 0)
            {
                throw new ArgumentException("The domain must be a host name such as tenant.example.com, optionally with the https scheme.", nameof(domain));
            }
            return uri.Authority;
        }
    }
}
