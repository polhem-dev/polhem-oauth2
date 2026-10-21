using System.ComponentModel;
using System.Globalization;

namespace Polhem.OAuth2.UnitTests
{
    // The documents state some default values, so that readers do not have to look them up in the source. These tests fail
    // when a default changes and a document in either language still states the old value.
    public class DocumentedDefaultsTests
    {
        [Fact]
        [DisplayName("The documents in both languages state the defaults that the options and clients have")]
        public void Documents_StatedDefaults_MatchCode()
        {
            var loopback = new LoopbackOAuth2Client(new GoogleOAuth2Options { ClientId = "id", RedirectUri = "http://127.0.0.1:0/callback" });
            string timeoutMinutes = loopback.Timeout.TotalMinutes.ToString(CultureInfo.InvariantCulture);
            string cookieMinutes = PendingAuthorizationCookie.Lifetime.TotalMinutes.ToString(CultureInfo.InvariantCulture);
            string usePkce = new GoogleOAuth2Options().UsePkce ? "true" : "false";
            string oktaServer = new OktaOAuth2Options().AuthorizationServerId;
            string entraTenant = new AzureOAuth2Options().Tenant;

            Assert.Contains($"(`Timeout`, {timeoutMinutes} minutes by default)", ReadRepositoryFile("docs/en/guides/desktop.md"), StringComparison.Ordinal);
            Assert.Contains($"（`Timeout`，預設 {timeoutMinutes} 分鐘）", ReadRepositoryFile("docs/zh-TW/guides/desktop.md"), StringComparison.Ordinal);
            Assert.Contains($"A sign-in must complete within {cookieMinutes} minutes.", ReadRepositoryFile("docs/en/guides/web-sign-in-state.md"), StringComparison.Ordinal);
            Assert.Contains($"登入必須在 {cookieMinutes} 分鐘內完成。", ReadRepositoryFile("docs/zh-TW/guides/web-sign-in-state.md"), StringComparison.Ordinal);
            Assert.Contains($"`UsePkce` is `{usePkce}` by default.", ReadRepositoryFile("docs/en/guides/options.md"), StringComparison.Ordinal);
            Assert.Contains($"`UsePkce` 預設為 `{usePkce}`。", ReadRepositoryFile("docs/zh-TW/guides/options.md"), StringComparison.Ordinal);
            Assert.Contains($"Okta uses the `{oktaServer}` authorization server", ReadRepositoryFile("docs/en/providers/okta.md"), StringComparison.Ordinal);
            Assert.Contains($"Okta 預設使用 `{oktaServer}` 授權伺服器", ReadRepositoryFile("docs/zh-TW/providers/okta.md"), StringComparison.Ordinal);
            Assert.Contains($"Microsoft Entra ID uses the `{entraTenant}` tenant.", ReadRepositoryFile("docs/en/providers/microsoft-entra-id.md"), StringComparison.Ordinal);
            Assert.Contains($"Microsoft Entra ID 預設使用 `{entraTenant}` tenant。", ReadRepositoryFile("docs/zh-TW/providers/microsoft-entra-id.md"), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("The provider documents in both languages state the default scopes that the options have")]
        public void Documents_StatedScopes_MatchOptions()
        {
            string gitHub = string.Join(" ", new GitHubOAuth2Options().Scopes);
            string apple = string.Join(" ", new AppleOAuth2Options().Scopes);
            string oidc = string.Join(" ", new OidcOAuth2Options().Scopes);

            Assert.Contains($"The options ask for the scopes `{gitHub}`.", ReadRepositoryFile("docs/en/providers/github.md"), StringComparison.Ordinal);
            Assert.Contains($"options 要求的 scope 為 `{gitHub}`。", ReadRepositoryFile("docs/zh-TW/providers/github.md"), StringComparison.Ordinal);
            Assert.Contains($"The default scopes `{apple}`", ReadRepositoryFile("docs/en/providers/apple.md"), StringComparison.Ordinal);
            Assert.Contains($"預設的 scope `{apple}`", ReadRepositoryFile("docs/zh-TW/providers/apple.md"), StringComparison.Ordinal);
            Assert.Contains($"The default scopes are `{oidc}`", ReadRepositoryFile("docs/en/providers/openid-connect.md"), StringComparison.Ordinal);
            Assert.Contains($"預設 scope 為 `{oidc}`", ReadRepositoryFile("docs/zh-TW/providers/openid-connect.md"), StringComparison.Ordinal);
        }

        [Theory]
        [DisplayName("The READMEs in both languages state the target frameworks of each package")]
        [InlineData("Polhem.OAuth2")]
        [InlineData("Polhem.OAuth2.AspNetCore")]
        [InlineData("Polhem.OAuth2.AspNet")]
        public void Readmes_StatedTargetFrameworks_MatchProjects(string package)
        {
            var project = System.Xml.Linq.XDocument.Parse(ReadRepositoryFile($"src/{package}/{package}.csproj"));
            string? names = project.Descendants()
                .FirstOrDefault(element => element.Name.LocalName is "TargetFrameworks" or "TargetFramework")?.Value;
            Assert.False(string.IsNullOrEmpty(names), $"{package}.csproj names no target framework.");
            string[] frameworks = names!.Split(';');
            string link = $"[{package}](https://www.nuget.org/packages/{package})";

            Assert.Contains($"{link} | {string.Join(", ", frameworks)} |", ReadRepositoryFile("README.md"), StringComparison.Ordinal);
            Assert.Contains($"{link} | {string.Join("、", frameworks)} |", ReadRepositoryFile("README.zh-TW.md"), StringComparison.Ordinal);
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
