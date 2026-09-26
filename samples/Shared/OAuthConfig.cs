#nullable enable

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Polhem.OAuth2;

namespace OAuthSamples
{
    /// <summary>
    /// The OAuth2 settings that the samples and the tools share. <c>OAuthConfig.json</c> lives in the repository root,
    /// and every project that imports <c>OAuthConfig.props</c> copies it to its output folder.
    /// </summary>
    /// <remarks>
    /// The file holds one section per provider under <c>Providers</c>, and each provider section holds one client per
    /// client type, because a provider registers a desktop client and a web client separately. Fields that both clients
    /// share and that are not credentials, such as the Okta domain, sit in the provider section and are merged into
    /// each client. The optional <c>AppRelay</c> section configures the back-end relay of the mobile sample.
    /// </remarks>
    public sealed class OAuthConfig
    {
        /// <summary>The name of the settings file.</summary>
        public const string FileName = "OAuthConfig.json";

        private const string ExampleFileName = "OAuthConfig.example.json";
        private const string ProvidersName = "Providers";
        private const string AppRelayName = "AppRelay";
        private const string ClientIdName = "ClientId";
        private const string ClientSecretName = "ClientSecret";

        private static readonly Provider[] s_providers =
        {
            new Provider("Google", typeof(GoogleOAuth2Options)),
            new Provider("Facebook", typeof(FacebookOAuth2Options)),
            new Provider("Line", typeof(LineOAuth2Options)),
            new Provider("Azure", typeof(AzureOAuth2Options), "Tenant"),
            new Provider("Auth0", typeof(Auth0OAuth2Options), "Domain"),
            new Provider("Okta", typeof(OktaOAuth2Options), "Domain", "AuthorizationServerId")
        };

        // Enumerations such as ClientAuthentication are written by name in the file.
        private static readonly JsonSerializerOptions s_readOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly string _filePath;
        private readonly Dictionary<OAuthClientType, List<OAuthClientEntry>> _clients;

        private OAuthConfig(string filePath, Dictionary<OAuthClientType, List<OAuthClientEntry>> clients)
        {
            _filePath = filePath;
            _clients = clients;
        }

        /// <summary>Gets the back-end relay settings, or null when the file has no <c>AppRelay</c> section.</summary>
        public OAuthAppRelay? AppRelay { get; private set; }

        /// <summary>Gets the provider names the settings file accepts, in the order the samples list them.</summary>
        public static IReadOnlyList<string> ProviderNames { get; } = Array.ConvertAll(s_providers, provider => provider.Name);

        /// <summary>Gets the path the settings file is read from: <see cref="FileName"/> in the output folder.</summary>
        public static string DefaultFilePath => Path.Combine(AppContext.BaseDirectory, FileName);

        /// <summary>
        /// Reads the settings file from <see cref="DefaultFilePath"/>.
        /// </summary>
        /// <returns>The settings.</returns>
        /// <exception cref="FileNotFoundException">The file does not exist.</exception>
        /// <exception cref="JsonException">The file is not valid JSON.</exception>
        /// <exception cref="InvalidDataException">The file has a section or a field the settings do not define.</exception>
        public static OAuthConfig Load()
        {
            return Load(DefaultFilePath);
        }

        /// <summary>
        /// Reads a settings file.
        /// </summary>
        /// <param name="filePath">The path of the file.</param>
        /// <returns>The settings.</returns>
        /// <exception cref="FileNotFoundException">The file does not exist.</exception>
        /// <exception cref="JsonException">The file is not valid JSON.</exception>
        /// <exception cref="InvalidDataException">The file has a section or a field the settings do not define.</exception>
        public static OAuthConfig Load(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    $"'{filePath}' was not found. Copy {ExampleFileName} in the repository root to {FileName} in the same " +
                    $"folder, fill it in and build again; the build copies that file to the output folder.",
                    filePath);
            }

            return Parse(File.ReadAllText(filePath), filePath);
        }

        /// <summary>
        /// Reads settings from JSON text, such as the file a mobile application carries in its package.
        /// </summary>
        /// <param name="json">The JSON text.</param>
        /// <param name="sourceName">The name that error messages give the source, such as its file name.</param>
        /// <returns>The settings.</returns>
        /// <exception cref="JsonException">The text is not valid JSON.</exception>
        /// <exception cref="InvalidDataException">The text has a section or a field the settings do not define.</exception>
        public static OAuthConfig Parse(string json, string sourceName)
        {
            string filePath = sourceName;
            if (JsonNode.Parse(json) is not JsonObject document)
                throw new InvalidDataException($"'{filePath}' does not hold a JSON object.");

            var clients = new Dictionary<OAuthClientType, List<OAuthClientEntry>>();
            foreach (OAuthClientType clientType in Enum.GetValues(typeof(OAuthClientType)))
                clients[clientType] = [];

            var config = new OAuthConfig(filePath, clients);
            foreach (var section in document)
            {
                if (string.Equals(section.Key, AppRelayName, StringComparison.OrdinalIgnoreCase))
                {
                    config.AppRelay = config.ReadAppRelay(section.Value);
                    continue;
                }

                if (!string.Equals(section.Key, ProvidersName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"'{filePath}' has an unknown top-level section '{section.Key}'. The sections are '{ProvidersName}' and '{AppRelayName}'.");
                }

                if (section.Value is not JsonObject providers)
                    throw new InvalidDataException($"'{filePath}' does not hold a JSON object in '{ProvidersName}'.");

                foreach (var provider in providers)
                    config.ReadProvider(provider.Key, provider.Value);
            }

            return config;
        }

        /// <summary>
        /// Gets the client one provider registered for one client type.
        /// </summary>
        /// <param name="providerName">The provider name, compared without regard to case.</param>
        /// <param name="clientType">The client type.</param>
        /// <returns>The options of that client.</returns>
        /// <exception cref="InvalidDataException">The provider is not supported, or the settings file has no such client.</exception>
        public OAuth2Options GetClient(string providerName, OAuthClientType clientType)
        {
            Provider provider = FindProvider(providerName)
                ?? throw new InvalidDataException(
                    $"'{providerName}' is not a supported provider. The providers are {string.Join(", ", ProviderNames)}.");

            if (_clients[clientType].FirstOrDefault(entry => string.Equals(entry.ProviderName, provider.Name, StringComparison.Ordinal)) is { } found)
                return found.Options;

            throw new InvalidDataException($"'{_filePath}' has no '{ProvidersName}.{provider.Name}.{clientType}' section.");
        }

        /// <summary>
        /// Gets the clients of one client type that carry a client ID, which is every provider the settings file has been
        /// filled in for. A provider section left with an empty <c>ClientId</c> is skipped.
        /// </summary>
        /// <param name="clientType">The client type.</param>
        /// <returns>The clients, in the order the settings file lists their providers.</returns>
        public IReadOnlyList<OAuthClientEntry> GetClients(OAuthClientType clientType)
        {
            return _clients[clientType]
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Options.ClientId))
                .ToList();
        }

        private static Provider? FindProvider(string name)
        {
            return s_providers.FirstOrDefault(provider => string.Equals(provider.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static bool TryReadClientType(string name, out OAuthClientType clientType)
        {
            foreach (OAuthClientType candidate in Enum.GetValues(typeof(OAuthClientType)))
            {
                if (string.Equals(candidate.ToString(), name, StringComparison.OrdinalIgnoreCase))
                {
                    clientType = candidate;
                    return true;
                }
            }

            clientType = default;
            return false;
        }

        private void ReadProvider(string providerName, JsonNode? providerNode)
        {
            Provider provider = FindProvider(providerName)
                ?? throw new InvalidDataException(
                    $"'{_filePath}' has an unknown provider '{ProvidersName}.{providerName}'. " +
                    $"The providers are {string.Join(", ", ProviderNames)}.");

            if (providerNode is not JsonObject section)
                throw new InvalidDataException($"'{_filePath}' does not hold a JSON object in '{ProvidersName}.{provider.Name}'.");

            var shared = new JsonObject();
            var clientSections = new List<KeyValuePair<OAuthClientType, JsonObject>>();
            foreach (var field in section)
            {
                if (TryReadClientType(field.Key, out var clientType))
                {
                    if (field.Value is not JsonObject clientSection)
                    {
                        throw new InvalidDataException(
                            $"'{_filePath}' does not hold a JSON object in '{ProvidersName}.{provider.Name}.{clientType}'.");
                    }

                    clientSections.Add(new KeyValuePair<OAuthClientType, JsonObject>(clientType, clientSection));
                }
                else if (provider.HasSharedField(field.Key))
                {
                    shared[field.Key] = field.Value?.DeepClone();
                }
                else
                {
                    throw new InvalidDataException(
                        $"'{_filePath}' has an unknown field '{ProvidersName}.{provider.Name}.{field.Key}'. " +
                        $"{provider.DescribeContents()} Client credentials belong in a client type section.");
                }
            }

            foreach (var clientSection in clientSections)
                _clients[clientSection.Key].Add(ReadClient(provider, clientSection.Key, shared, clientSection.Value));
        }

        private OAuthClientEntry ReadClient(Provider provider, OAuthClientType clientType, JsonObject shared, JsonObject section)
        {
            var merged = new JsonObject();
            foreach (var field in shared)
                merged[field.Key] = field.Value?.DeepClone();

            bool hasClientId = false;
            foreach (var field in section)
            {
                hasClientId |= string.Equals(field.Key, ClientIdName, StringComparison.OrdinalIgnoreCase);
                // A secret shipped inside an application package is public, so an App client is a public client (ADR-006).
                if ((clientType == OAuthClientType.Ios || clientType == OAuthClientType.Android)
                    && string.Equals(field.Key, ClientSecretName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"'{_filePath}' has a {ClientSecretName} in '{ProvidersName}.{provider.Name}.{clientType}'. " +
                        "An application cannot keep a secret, so remove it.");
                }
                merged[field.Key] = field.Value?.DeepClone();
            }

            if (!hasClientId)
            {
                throw new InvalidDataException(
                    $"'{_filePath}' has no {ClientIdName} in '{ProvidersName}.{provider.Name}.{clientType}'.");
            }

            var options = (OAuth2Options?)JsonSerializer.Deserialize(merged.ToJsonString(), provider.OptionsType, s_readOptions);
            if (options is null)
                throw new InvalidDataException($"'{_filePath}' could not read '{ProvidersName}.{provider.Name}.{clientType}'.");

            return new OAuthClientEntry(provider.Name, options);
        }

        private OAuthAppRelay ReadAppRelay(JsonNode? node)
        {
            if (node is not JsonObject section)
                throw new InvalidDataException($"'{_filePath}' does not hold a JSON object in '{AppRelayName}'.");

            string? backendUrl = null;
            string? redirectUri = null;
            foreach (var field in section)
            {
                if (string.Equals(field.Key, "BackendUrl", StringComparison.OrdinalIgnoreCase))
                    backendUrl = field.Value?.GetValue<string>();
                else if (string.Equals(field.Key, "RedirectUri", StringComparison.OrdinalIgnoreCase))
                    redirectUri = field.Value?.GetValue<string>();
                else
                    throw new InvalidDataException($"'{_filePath}' has an unknown field '{AppRelayName}.{field.Key}'. The fields are BackendUrl and RedirectUri.");
            }

            if (!Uri.TryCreate(backendUrl, UriKind.Absolute, out var backend) || backend.Scheme != Uri.UriSchemeHttps)
                throw new InvalidDataException($"'{_filePath}' needs an absolute https URL in '{AppRelayName}.BackendUrl'.");
            if (string.IsNullOrWhiteSpace(redirectUri))
                throw new InvalidDataException($"'{_filePath}' needs a RedirectUri in '{AppRelayName}'.");

            return new OAuthAppRelay(backend, redirectUri);
        }

        /// <summary>
        /// One supported provider: the options type its settings become, and the fields its clients may share.
        /// </summary>
        private sealed class Provider
        {
            private readonly string[] _sharedFields;

            public Provider(string name, Type optionsType, params string[] sharedFields)
            {
                Name = name;
                OptionsType = optionsType;
                _sharedFields = sharedFields;
            }

            public string Name { get; }

            public Type OptionsType { get; }

            public bool HasSharedField(string name)
            {
                return _sharedFields.Any(field => string.Equals(field, name, StringComparison.OrdinalIgnoreCase));
            }

            public string DescribeContents()
            {
                string clientTypes = string.Join(", ", Enum.GetNames(typeof(OAuthClientType)));
                return _sharedFields.Length == 0
                    ? $"'{ProvidersName}.{Name}' holds only the client type sections {clientTypes}."
                    : $"'{ProvidersName}.{Name}' holds only the client type sections {clientTypes} and the shared fields " +
                      $"{string.Join(", ", _sharedFields)}.";
            }
        }
    }
}
