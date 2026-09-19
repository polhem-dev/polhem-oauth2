using Android.App;
using Android.Content;
using Android.Content.PM;

namespace Polhem.OAuth2.DeviceTests;

[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], DataScheme = "dev.polhem.oauth2.devicetests")]
public class CallbackActivity : WebAuthenticatorCallbackActivity
{
}
