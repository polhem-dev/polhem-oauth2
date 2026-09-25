using Android.App;
using Android.Content;
using Android.Content.PM;
using Polhem.OAuth2.FakeProvider;

namespace Polhem.OAuth2.DeviceTests;

[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], DataScheme = FakeProviderValues.AppScheme)]
public class CallbackActivity : WebAuthenticatorCallbackActivity
{
}
