namespace Polhem.OAuth2.DeviceTests.EndToEnd;

/// <summary>
/// A fact that signs in to the fake provider with <c>WebAuthenticator</c>, on Android, iOS and Mac Catalyst. It is skipped
/// when the build has no fake provider or on Windows, where <c>WebAuthenticator</c> does not work (ADR-006).
/// </summary>
public sealed class AppSignInFactAttribute : FactAttribute
{
    public AppSignInFactAttribute()
    {
        if (FakeProvider.Origin is null)
            Skip = "No fake provider was passed to the build.";
        else if (OperatingSystem.IsWindows())
            Skip = "WebAuthenticator does not work on Windows.";
    }
}
