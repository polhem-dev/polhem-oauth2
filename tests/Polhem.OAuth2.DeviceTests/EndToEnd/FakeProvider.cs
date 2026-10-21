using System.Reflection;
using Polhem.OAuth2.FakeProvider;

namespace Polhem.OAuth2.DeviceTests.EndToEnd;

/// <summary>
/// Where the fake provider runs, as passed to the build, and the options that sign in to it.
/// </summary>
internal static class FakeProvider
{
    public static Uri? Origin { get; } = typeof(FakeProvider).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .Where(attribute => attribute.Key == "FakeProviderUrl" && !string.IsNullOrEmpty(attribute.Value))
        .Select(attribute => new Uri(attribute.Value!))
        .FirstOrDefault();

    public static Auth0OAuth2Options CreateOptions(string clientId, string redirectUri)
    {
        var origin = Origin ?? throw new InvalidOperationException("The fake provider is not configured.");
        return new Auth0OAuth2Options { Domain = origin.Authority, ClientId = clientId, RedirectUri = redirectUri };
    }

    /// <summary>
    /// Reads the options of the OpenID Connect mode from its discovery document, which names no user information
    /// endpoint, so the user comes from the ID token.
    /// </summary>
    public static async Task<OidcOAuth2Options> CreateOidcOptionsAsync(string clientId, string redirectUri)
    {
        var origin = Origin ?? throw new InvalidOperationException("The fake provider is not configured.");
        var options = await OidcOAuth2Options.FromDiscoveryAsync(origin.GetLeftPart(UriPartial.Authority) + FakeProviderValues.OidcPath);
        options.ProviderName = "Fake OpenID Connect";
        options.ClientId = clientId;
        options.RedirectUri = redirectUri;
        return options;
    }

    public static Uri GetUri(string pathAndQuery)
    {
        var origin = Origin ?? throw new InvalidOperationException("The fake provider is not configured.");
        return new Uri(origin, pathAndQuery);
    }

    public static bool IsKnownUser(UserInfo? user)
    {
        return user is not null
            && user.UserId == FakeProviderValues.UserId
            && user.UserName == FakeProviderValues.UserName
            && user.Email == FakeProviderValues.Email;
    }
}
