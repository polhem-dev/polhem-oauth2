using Android.App;
using Android.Content.PM;

namespace Polhem.OAuth2.DeviceTests;

// `dotnet test` starts the activity named <package>.MainActivity, so the generated name is replaced.
[Activity(Name = "dev.polhem.oauth2.devicetests.MainActivity", Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
}
