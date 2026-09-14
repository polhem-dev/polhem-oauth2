using System.ComponentModel;

namespace Polhem.OAuth2.UnitTests
{
    public class SystemBrowserTests
    {
        [Theory]
        [DisplayName("IsWebUrl accepts only absolute http and https URLs")]
        [InlineData("https://accounts.google.com/o/oauth2/auth?client_id=x", true)]
        [InlineData("http://127.0.0.1:53682/callback", true)]
        [InlineData("file:///etc/hosts", false)]
        [InlineData("calc.exe", false)]
        [InlineData("/usr/bin/open", false)]
        [InlineData("mailto:user@example.com", false)]
        [InlineData("", false)]
        public void IsWebUrl_VariousStrings_AcceptsOnlyWebUrls(string url, bool expected)
        {
            Assert.Equal(expected, SystemBrowser.IsWebUrl(url));
        }

        [Fact]
        [DisplayName("Open refuses a URL that is not a web URL before starting a process")]
        public void Open_FileUrl_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() => SystemBrowser.Open("file:///polhem-oauth2-test/does-not-exist"));
        }
    }
}
