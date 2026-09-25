using System.ComponentModel;
using System.Globalization;

namespace Polhem.OAuth2.UnitTests
{
    // The README states some default values, because readers on NuGet cannot follow a link to the source. These tests fail
    // when a default changes and a README still states the old value.
    public class ReadmeDefaultsTests
    {
        [Fact]
        [DisplayName("The README in both languages states the defaults that the options and clients have")]
        public void Readme_StatedDefaults_MatchCode()
        {
            string english = ReadRepositoryFile("README.md");
            string chinese = ReadRepositoryFile("README.zh-TW.md");
            var loopback = new LoopbackOAuth2Client(new GoogleOAuth2Options { ClientId = "id", RedirectUri = "http://127.0.0.1:0/callback" });
            string timeoutMinutes = loopback.Timeout.TotalMinutes.ToString(CultureInfo.InvariantCulture);
            string cookieMinutes = PendingAuthorizationCookie.Lifetime.TotalMinutes.ToString(CultureInfo.InvariantCulture);
            string usePkce = new GoogleOAuth2Options().UsePkce ? "true" : "false";
            string oktaServer = new OktaOAuth2Options().AuthorizationServerId;
            string entraTenant = new AzureOAuth2Options().Tenant;

            Assert.Contains($"(`Timeout`, {timeoutMinutes} minutes by default)", english, StringComparison.Ordinal);
            Assert.Contains($"（`Timeout`，預設 {timeoutMinutes} 分鐘）", chinese, StringComparison.Ordinal);
            Assert.Contains($"A sign-in must complete within {cookieMinutes} minutes.", english, StringComparison.Ordinal);
            Assert.Contains($"登入必須在 {cookieMinutes} 分鐘內完成。", chinese, StringComparison.Ordinal);
            Assert.Contains($"`UsePkce` is `{usePkce}` by default.", english, StringComparison.Ordinal);
            Assert.Contains($"`UsePkce` 預設為 `{usePkce}`。", chinese, StringComparison.Ordinal);
            Assert.Contains($"Okta uses the `{oktaServer}` authorization server", english, StringComparison.Ordinal);
            Assert.Contains($"Okta 預設使用 `{oktaServer}` 授權伺服器", chinese, StringComparison.Ordinal);
            Assert.Contains($"Microsoft Entra ID uses the `{entraTenant}` tenant.", english, StringComparison.Ordinal);
            Assert.Contains($"Microsoft Entra ID 預設使用 `{entraTenant}` tenant。", chinese, StringComparison.Ordinal);
        }

        private static string ReadRepositoryFile(string name)
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Polhem.OAuth2.slnx")))
                    return File.ReadAllText(Path.Combine(directory.FullName, name));
            }
            throw new InvalidOperationException("The repository root was not found above the test output folder.");
        }
    }
}
