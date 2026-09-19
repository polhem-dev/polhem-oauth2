using System.ComponentModel;

namespace Polhem.OAuth2.DeviceTests;

public class DefaultBrowserTests
{
    [MobileFact]
    [DisplayName("Without OpenBrowser, SignInAsync fails on iOS and Android with the documented exception")]
    public async Task SignInAsync_DefaultOpenBrowser_ThrowsDocumentedException()
    {
        var options = new GoogleOAuth2Options { ClientId = "client-id", RedirectUri = "http://127.0.0.1:0/callback" };
        var client = new LoopbackOAuth2Client(options);

        var exception = await Record.ExceptionAsync(() => client.SignInAsync());

        if (OperatingSystem.IsAndroid())
            Assert.IsType<Win32Exception>(exception);
        else
            Assert.IsType<PlatformNotSupportedException>(exception);
    }
}
