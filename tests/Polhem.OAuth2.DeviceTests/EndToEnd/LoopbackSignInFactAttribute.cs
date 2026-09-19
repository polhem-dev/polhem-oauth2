namespace Polhem.OAuth2.DeviceTests.EndToEnd;

/// <summary>
/// A fact that signs in to the fake provider through the default browser and a loopback redirect, on Windows. It is
/// skipped when the build has no fake provider or on other platforms.
/// </summary>
public sealed class LoopbackSignInFactAttribute : FactAttribute
{
    public LoopbackSignInFactAttribute()
    {
        if (FakeProvider.Origin is null)
            Skip = "No fake provider was passed to the build.";
        else if (!OperatingSystem.IsWindows())
            Skip = "Applications sign in with the loopback client on Windows only (ADR-006).";
    }
}
