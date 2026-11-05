using System.Globalization;

namespace Polhem.OAuth2.AspNetCore
{
    /// <summary>
    /// The relay settings, validated and copied when they are registered, so later changes to the options have no effect.
    /// </summary>
    internal sealed class AppRelaySettings
    {
        // RFC 6749, section 4.1.2, recommends at most 10 minutes for an authorization code, which a relay code stands in for.
        private static readonly TimeSpan s_maxCodeLifetime = TimeSpan.FromMinutes(10);

        private readonly HashSet<string> _appRedirectUris;
        private readonly HashSet<string> _anyPortLoopbackUris;

        private AppRelaySettings(HashSet<string> appRedirectUris, HashSet<string> anyPortLoopbackUris, TimeSpan codeLifetime)
        {
            _appRedirectUris = appRedirectUris;
            _anyPortLoopbackUris = anyPortLoopbackUris;
            CodeLifetime = codeLifetime;
        }

        /// <summary>
        /// Gets how long a relay code can be redeemed.
        /// </summary>
        public TimeSpan CodeLifetime { get; }

        /// <summary>
        /// Validates and copies the options.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="paramName">The name of the parameter of the caller that produced the options, for the exception.</param>
        /// <returns>The settings.</returns>
        /// <exception cref="ArgumentException">
        /// No application redirect URI is registered, one of them is not valid, or the code lifetime is not positive or is
        /// longer than 10 minutes.
        /// </exception>
        public static AppRelaySettings Create(OAuth2AppRelayOptions options, string paramName)
        {
            if (options.AppRedirectUris.Count == 0)
                throw new ArgumentException("Register at least one application redirect URI.", paramName);
            var anyPortLoopbackUris = new HashSet<string>(StringComparer.Ordinal);
            foreach (string uri in options.AppRedirectUris)
            {
                if (TryParseLoopbackUri(uri, allowState: false, out var loopback))
                {
                    if (!loopback.HasPort)
                        anyPortLoopbackUris.Add(loopback.AnyPortForm);
                }
                else if (!OAuth2Options.IsAppRedirectUri(uri))
                {
                    throw new ArgumentException(
                        $"'{uri}' is not an absolute https URI or custom scheme URI without a fragment, or an http URI of 127.0.0.1 or [::1] with a path and no query.", paramName);
                }
            }
            if (options.CodeLifetime <= TimeSpan.Zero || options.CodeLifetime > s_maxCodeLifetime)
                throw new ArgumentException($"The code lifetime must be positive and at most {s_maxCodeLifetime.TotalMinutes:0} minutes.", paramName);

            return new AppRelaySettings(new HashSet<string>(options.AppRedirectUris, StringComparer.Ordinal), anyPortLoopbackUris, options.CodeLifetime);
        }

        /// <summary>
        /// Checks whether a relayed sign-in may return to an application redirect URI.
        /// </summary>
        /// <param name="appRedirectUri">The requested application redirect URI.</param>
        /// <returns>
        /// True if the URI is registered, or if it is a loopback URI whose address and path are registered without a port, or
        /// whose address, port and path are registered, optionally with a <c>state</c> query of the application.
        /// </returns>
        public bool IsRegistered(string appRedirectUri)
        {
            if (_appRedirectUris.Contains(appRedirectUri))
                return true;
            if (!TryParseLoopbackUri(appRedirectUri, allowState: true, out var loopback))
                return false;
            // RFC 8252, section 7.3: a desktop application listens on a port of its choosing for each sign-in, so a loopback
            // URI registered without a port matches that address and path on any port.
            return _appRedirectUris.Contains(loopback.WithoutQuery)
                || (loopback.HasPort && _anyPortLoopbackUris.Contains(loopback.AnyPortForm));
        }

        /// <summary>
        /// Reads a loopback redirect URI of a desktop application: http, the address 127.0.0.1 or [::1], an optional port, a
        /// path, and, in a requested URI only, an optional <c>state</c> query. The URI must already be in the form that
        /// <see cref="Uri"/> writes it, so the URI that the relay later redirects to is exactly the one it compared.
        /// </summary>
        /// <param name="uri">The URI to read.</param>
        /// <param name="allowState">
        /// Whether the URI may end with <c>?state=</c> and 43 to 128 base64url characters. A desktop application adds its own
        /// state, because any web page open in the browser can send a request to a loopback address (ADR-004), and the relay
        /// returns the code after it, so the application can tell the redirect of its sign-in from such a request.
        /// </param>
        /// <param name="loopback">The parts of the URI.</param>
        /// <returns>True if the URI is a loopback redirect URI.</returns>
        private static bool TryParseLoopbackUri(string uri, bool allowState, out LoopbackUri loopback)
        {
            loopback = default;
            // IMPORTANT: localhost is not accepted. A name can resolve to an address other than the loopback interface, as
            // ADR-004 explains for the loopback client, and the address of the listener must be the one the browser reaches.
            if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
                || parsed.Scheme != Uri.UriSchemeHttp
                || parsed.HostNameType is not (UriHostNameType.IPv4 or UriHostNameType.IPv6)
                || !parsed.IsLoopback
                || parsed.UserInfo.Length > 0
                || parsed.Fragment.Length > 0
                || parsed.AbsolutePath.Length <= 1
                || (parsed.Query.Length > 0 && !(allowState && IsStateQuery(parsed.Query))))
            {
                return false;
            }

            // Comparing with the canonical form below leaves only these two addresses, and no other spelling of them.
            // NOTE: Plain http is what RFC 8252, section 7.3, prescribes for a loopback redirect: the request never leaves the
            // computer, and a desktop application has no certificate for 127.0.0.1.
            string origin = Uri.UriSchemeHttp + Uri.SchemeDelimiter + (parsed.HostNameType == UriHostNameType.IPv6 ? "[::1]" : "127.0.0.1");
            bool hasPort = !parsed.IsDefaultPort || uri.StartsWith(origin + ":", StringComparison.Ordinal);
            if (hasPort && parsed.Port is < 1 or > 65535)
                return false;

            string anyPortForm = origin + parsed.AbsolutePath;
            string withoutQuery = hasPort
                ? origin + ":" + parsed.Port.ToString(CultureInfo.InvariantCulture) + parsed.AbsolutePath
                : anyPortForm;
            if (!string.Equals(uri, withoutQuery + parsed.Query, StringComparison.Ordinal))
                return false;

            loopback = new LoopbackUri(anyPortForm, withoutQuery, hasPort);
            return true;
        }

        private static bool IsStateQuery(string query)
        {
            const string prefix = "?state=";
            int length = query.Length - prefix.Length;
            return query.StartsWith(prefix, StringComparison.Ordinal)
                && length is >= 43 and <= 128
                && query.Skip(prefix.Length).All(IsBase64UrlChar);
        }

        private static bool IsBase64UrlChar(char c)
        {
            return c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_';
        }

        private readonly struct LoopbackUri
        {
            public LoopbackUri(string anyPortForm, string withoutQuery, bool hasPort)
            {
                AnyPortForm = anyPortForm;
                WithoutQuery = withoutQuery;
                HasPort = hasPort;
            }

            /// <summary>Gets the URI without its port and query, which a registration without a port is stored under.</summary>
            public string AnyPortForm { get; }

            /// <summary>Gets the URI without its query.</summary>
            public string WithoutQuery { get; }

            /// <summary>Gets a value indicating whether the URI names a port.</summary>
            public bool HasPort { get; }
        }
    }
}
