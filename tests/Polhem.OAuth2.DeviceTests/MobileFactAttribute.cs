namespace Polhem.OAuth2.DeviceTests;

/// <summary>
/// A fact for iOS and Android, where no process can open a browser. Elsewhere it is reported as skipped.
/// </summary>
public sealed class MobileFactAttribute : FactAttribute
{
    public MobileFactAttribute()
    {
        // Mac Catalyst also reports itself as iOS.
        bool isMobile = OperatingSystem.IsAndroid() || (OperatingSystem.IsIOS() && !OperatingSystem.IsMacCatalyst());
        if (!isMobile)
            Skip = "Only iOS and Android cannot start a browser process.";
    }
}
