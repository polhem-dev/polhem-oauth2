namespace Polhem.OAuth2.FakeProvider
{
    /// <summary>
    /// What the fake provider remembers about an authorization code until the client exchanges it.
    /// </summary>
    internal sealed record FakeAuthorizationCode(string ClientId, string RedirectUri, string? CodeChallenge);
}
