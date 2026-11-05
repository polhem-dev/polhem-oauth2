using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Polhem.OAuth2.AspNetCore
{
    /// <summary>
    /// Settings of the back-end relay, through which a mobile or desktop application signs in with this ASP.NET Core
    /// application (ADR-006). Register them with
    /// <see cref="Microsoft.Extensions.DependencyInjection.OAuth2ServiceCollectionExtensions.AddOAuth2AppRelay(Microsoft.Extensions.DependencyInjection.IServiceCollection, Action{OAuth2AppRelayOptions})"/>,
    /// or read them from a configuration section with
    /// <see cref="Microsoft.Extensions.DependencyInjection.OAuth2ServiceCollectionExtensions.AddOAuth2AppRelay(Microsoft.Extensions.DependencyInjection.IServiceCollection, IConfiguration)"/>.
    /// </summary>
    public sealed class OAuth2AppRelayOptions
    {
        /// <summary>
        /// Gets the application redirect URIs that a relayed sign-in may return to, such as <c>com.example.app:/signin</c>
        /// for a mobile application or <c>http://127.0.0.1/callback</c> for a desktop application.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Each URI must pass <see cref="OAuth2Options.IsAppRedirectUri"/>, the rule that <see cref="AppOAuth2Client"/> applies
        /// to its redirect URI, or be a loopback URI: <c>http</c>, the address <c>127.0.0.1</c> or <c>[::1]</c>, a path, and no
        /// query. <c>localhost</c> is not accepted.
        /// </para>
        /// <para>
        /// A requested URI must equal one of them exactly, with two exceptions for loopback URIs, which
        /// <see cref="LoopbackRelayClient"/> relies on. A loopback URI registered without a port matches the same address and
        /// path on any port, as RFC 8252, section 7.3, describes. A requested loopback URI may also end with a <c>state</c>
        /// query of 43 to 128 base64url characters, which the relay returns ahead of the code.
        /// </para>
        /// </remarks>
        public IList<string> AppRedirectUris { get; } = [];

        /// <summary>
        /// Gets or sets how long the code that a relayed sign-in returns to the application can be redeemed. The default is
        /// one minute, and the longest is 10 minutes, which RFC 6749, section 4.1.2, recommends for an authorization code.
        /// </summary>
        /// <remarks>An application redeems the code as soon as it receives it, so a short lifetime costs nothing.</remarks>
        public TimeSpan CodeLifetime { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Reads the options from a configuration section. The settings are read by name, so the package needs no reflection
        /// and stays safe to trim.
        /// </summary>
        /// <param name="configuration">The section.</param>
        /// <param name="paramName">The name of the parameter of the caller that passed the section, for the exception.</param>
        /// <returns>The options.</returns>
        /// <exception cref="ArgumentException">
        /// The section has a key other than <see cref="AppRedirectUris"/> and <see cref="CodeLifetime"/>, or the code lifetime
        /// is not a time span.
        /// </exception>
        internal static OAuth2AppRelayOptions FromConfiguration(IConfiguration configuration, string paramName)
        {
            var options = new OAuth2AppRelayOptions();
            foreach (var setting in configuration.GetChildren())
            {
                if (string.Equals(setting.Key, nameof(AppRedirectUris), StringComparison.OrdinalIgnoreCase))
                {
                    // One URI can be written as a plain value, as an environment variable does, and several as an array.
                    if (setting.Value is { } single)
                        options.AppRedirectUris.Add(single);
                    foreach (var uri in setting.GetChildren())
                        options.AppRedirectUris.Add(uri.Value ?? string.Empty);
                }
                else if (string.Equals(setting.Key, nameof(CodeLifetime), StringComparison.OrdinalIgnoreCase))
                {
                    if (!TimeSpan.TryParse(setting.Value, CultureInfo.InvariantCulture, out var codeLifetime))
                        throw new ArgumentException($"{nameof(CodeLifetime)} must be a time span, such as 00:01:00.", paramName);
                    options.CodeLifetime = codeLifetime;
                }
                else
                {
                    // A misspelled setting would otherwise leave its default in place without a word.
                    throw new ArgumentException($"'{setting.Key}' is not a setting of the application relay.", paramName);
                }
            }
            return options;
        }
    }
}
