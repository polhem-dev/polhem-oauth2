using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Polhem.OAuth2;
using Polhem.OAuth2.AspNetCore;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Registers Polhem OAuth2 clients with an <see cref="IServiceCollection"/>.
    /// </summary>
    public static class OAuth2ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers an OAuth2 client under a name, together with <see cref="OAuth2Manager"/>, which runs its sign-in, and
        /// ASP.NET Core data protection, which protects the pending sign-in cookie.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="clientName">The name that identifies the client, such as <c>Google</c>.</param>
        /// <param name="options">The OAuth2 options. Their type selects the provider, and they are copied.</param>
        /// <param name="httpClient">
        /// The HTTP client for requests to the provider, or null to use a shared instance. The client does not dispose it.
        /// </param>
        /// <returns>The service collection.</returns>
        /// <remarks>
        /// The client is created by this call, so invalid options are reported when the application starts. Clients cannot
        /// be added once the service provider has been built.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="clientName"/> is null, empty or white space, or <paramref name="options"/> is not valid.
        /// </exception>
        /// <exception cref="InvalidOperationException">A client is already registered under <paramref name="clientName"/>.</exception>
        public static IServiceCollection AddOAuth2Client(
            this IServiceCollection services, string clientName, OAuth2Options options, HttpClient? httpClient = null)
        {
            if (services is null)
                throw new ArgumentNullException(nameof(services));
            if (string.IsNullOrWhiteSpace(clientName))
                throw new ArgumentException("The client name cannot be null, empty or white space.", nameof(clientName));
            if (IsRegistered(services, clientName))
                throw new InvalidOperationException($"An OAuth2 client is already registered under the name '{clientName}'.");

            return AddRegistration(services, clientName, new OAuth2ClientRegistration(clientName, new OAuth2Client(options, httpClient)));
        }

        /// <summary>
        /// Registers an OAuth2 client under a name, as <see cref="AddOAuth2Client"/> does, with an HTTP client that the service
        /// provider supplies for each request to the provider, for example one from <c>IHttpClientFactory</c>. The name differs
        /// because an overload of <see cref="AddOAuth2Client"/> would make a call that passes null for the HTTP client ambiguous.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="clientName">The name that identifies the client, such as <c>Google</c>.</param>
        /// <param name="options">The OAuth2 options. Their type selects the provider, and they are copied.</param>
        /// <param name="httpClientFactory">
        /// Returns the HTTP client for a request to the provider, from the service provider. It is called for each request,
        /// so a factory that rotates its handlers is honored; the client is not disposed.
        /// </param>
        /// <returns>The service collection.</returns>
        /// <remarks>
        /// The client is created by this call, so invalid options are reported when the application starts. Clients cannot
        /// be added once the service provider has been built.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="services"/>, <paramref name="options"/> or <paramref name="httpClientFactory"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="clientName"/> is null, empty or white space, or <paramref name="options"/> is not valid.
        /// </exception>
        /// <exception cref="InvalidOperationException">A client is already registered under <paramref name="clientName"/>.</exception>
        public static IServiceCollection AddOAuth2ClientWithHttpClientFactory(
            this IServiceCollection services, string clientName, OAuth2Options options, Func<IServiceProvider, HttpClient> httpClientFactory)
        {
            if (services is null)
                throw new ArgumentNullException(nameof(services));
            if (httpClientFactory is null)
                throw new ArgumentNullException(nameof(httpClientFactory));
            if (string.IsNullOrWhiteSpace(clientName))
                throw new ArgumentException("The client name cannot be null, empty or white space.", nameof(clientName));
            if (IsRegistered(services, clientName))
                throw new InvalidOperationException($"An OAuth2 client is already registered under the name '{clientName}'.");

            return AddRegistration(services, clientName, new OAuth2ClientRegistration(clientName, options, httpClientFactory));
        }

        private static IServiceCollection AddRegistration(IServiceCollection services, string clientName, OAuth2ClientRegistration registration)
        {
            services.AddSingleton(registration);
            services.AddDataProtection();
            services.TryAddSingleton(provider => new OAuth2Manager(
                Attach(provider, provider.GetServices<OAuth2ClientRegistration>()),
                provider.GetRequiredService<IDataProtectionProvider>(),
                provider.GetService<AppRelaySettings>(),
                provider.GetService<IDistributedCache>(),
                provider.GetService<TimeProvider>()));
            return services;
        }

        private static List<OAuth2ClientRegistration> Attach(IServiceProvider provider, IEnumerable<OAuth2ClientRegistration> registrations)
        {
            var attached = registrations.ToList();
            foreach (var registration in attached)
                registration.Attach(provider);
            return attached;
        }

        /// <summary>
        /// Registers the back-end relay, through which mobile applications sign in with the clients registered by
        /// <see cref="AddOAuth2Client"/> (ADR-006), and the in-memory <see cref="IDistributedCache"/> unless another
        /// distributed cache is already registered.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Sets the application redirect URIs and the code lifetime.</param>
        /// <returns>The service collection.</returns>
        /// <remarks>
        /// Relay codes are kept in <see cref="IDistributedCache"/>. When several servers receive callbacks, register a
        /// distributed cache that they share, as well as a shared data protection key ring, which also protects the user
        /// information in the cache. The options are validated and copied by this call.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// No application redirect URI is set, one of them is not an absolute https URI or custom scheme URI without a
        /// fragment, or the code lifetime is not positive or is longer than 10 minutes.
        /// </exception>
        /// <exception cref="InvalidOperationException">The relay is already registered.</exception>
        public static IServiceCollection AddOAuth2AppRelay(this IServiceCollection services, Action<OAuth2AppRelayOptions> configure)
        {
            if (services is null)
                throw new ArgumentNullException(nameof(services));
            if (configure is null)
                throw new ArgumentNullException(nameof(configure));
            ThrowIfRelayRegistered(services);

            var options = new OAuth2AppRelayOptions();
            configure(options);
            return AddRelay(services, AppRelaySettings.Create(options, nameof(configure)));
        }

        /// <summary>
        /// Registers the back-end relay, as <see cref="AddOAuth2AppRelay(IServiceCollection, Action{OAuth2AppRelayOptions})"/>
        /// does, with the settings of a configuration section, such as <c>builder.Configuration.GetSection("AppRelay")</c>.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">
        /// The section. <c>AppRedirectUris</c> holds the application redirect URIs, as an array or as one value, and
        /// <c>CodeLifetime</c>, which may be left out, a time span such as <c>00:01:00</c>.
        /// </param>
        /// <returns>The service collection.</returns>
        /// <remarks>
        /// The section is read and validated by this call, so a later change to the configuration has no effect. Any other key
        /// in the section is an error, so that a misspelled setting does not leave its default in place unnoticed.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configuration"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// The section has another key, the code lifetime is not a time span, no application redirect URI is set, one of them
        /// is not an absolute https URI or custom scheme URI without a fragment, or the code lifetime is not positive or is
        /// longer than 10 minutes.
        /// </exception>
        /// <exception cref="InvalidOperationException">The relay is already registered.</exception>
        public static IServiceCollection AddOAuth2AppRelay(this IServiceCollection services, IConfiguration configuration)
        {
            if (services is null)
                throw new ArgumentNullException(nameof(services));
            if (configuration is null)
                throw new ArgumentNullException(nameof(configuration));
            ThrowIfRelayRegistered(services);

            var options = OAuth2AppRelayOptions.FromConfiguration(configuration, nameof(configuration));
            return AddRelay(services, AppRelaySettings.Create(options, nameof(configuration)));
        }

        private static void ThrowIfRelayRegistered(IServiceCollection services)
        {
            if (services.Any(descriptor => descriptor.ServiceType == typeof(AppRelaySettings)))
                throw new InvalidOperationException("The OAuth2 application relay is already registered.");
        }

        private static IServiceCollection AddRelay(IServiceCollection services, AppRelaySettings settings)
        {
            services.AddSingleton(settings);
            services.AddDistributedMemoryCache();
            return services;
        }

        private static bool IsRegistered(IServiceCollection services, string clientName)
        {
            // The service type is compared first: reading ImplementationInstance of a keyed registration throws.
            return services.Any(descriptor =>
                descriptor.ServiceType == typeof(OAuth2ClientRegistration)
                && !descriptor.IsKeyedService
                && descriptor.ImplementationInstance is OAuth2ClientRegistration registration
                && string.Equals(registration.Name, clientName, StringComparison.Ordinal));
        }
    }
}
