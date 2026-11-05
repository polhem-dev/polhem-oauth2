using System.ComponentModel;
using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Polhem.OAuth2.AspNetCore;

namespace Polhem.OAuth2.UnitTests
{
    public class AspNetCoreLoopbackRelayTests
    {
        private const string UserJson = """{"sub":"user-1","name":"User","email":"user@example.com"}""";

        [Fact]
        [DisplayName("The application redirect URI of SignInAsync is one that a relay registered with http://127.0.0.1/callback accepts")]
        public async Task SignInAsync_RedirectUri_IsRegisteredWithRelay()
        {
            var services = new ServiceCollection();
            services.AddOAuth2AppRelay(options => options.AppRedirectUris.Add("http://127.0.0.1/callback"));
            var settings = services.BuildServiceProvider().GetRequiredService<AppRelaySettings>();
            var browser = new FakeRelayBrowser("{redirect}&code=abc");
            var client = new LoopbackRelayClient("http://127.0.0.1:0/callback") { OpenBrowser = browser.Open };

            await client.SignInAsync(FakeRelayBrowser.CreateSignInUrl).WithTimeout();
            await browser.Completed.WithTimeout();

            Assert.True(settings.IsRegistered(browser.RedirectUri!));
        }

        [Fact]
        [DisplayName("A desktop application signs in through the ASP.NET Core relay and redeems the code for the user")]
        public async Task SignInAsync_ThroughRelay_RedeemsUser()
        {
            var manager = CreateManager();
            string? relayedTo = null;
            Task followed = Task.CompletedTask;
            var client = new LoopbackRelayClient("http://127.0.0.1:0/callback")
            {
                OpenBrowser = async url =>
                {
                    // The browser opens the sign-in URL of the back end, which starts the relayed sign-in, and the provider
                    // returns to the back end, which redirects to the application.
                    var start = AspNetCoreTestContext.Create();
                    Assert.True(manager.TryRedirectToAppAuthorization(start, "Google",
                        LoopbackTestHttp.GetQueryValue(url.AbsoluteUri, "redirect_uri"),
                        LoopbackTestHttp.GetQueryValue(url.AbsoluteUri, "code_challenge")));
                    string state = LoopbackTestHttp.GetQueryValue(start.Response.Headers.Location.ToString(), "state")!;
                    var cookie = Assert.Single(start.Response.GetTypedHeaders().SetCookie);
                    var callback = AspNetCoreTestContext.Create("?code=provider-code&state=" + state, $"{cookie.Name}={cookie.Value}");
                    var result = await manager.CompleteAuthorizationAsync(callback);
                    Assert.True(await manager.RedirectToAppAsync(callback, result));
                    relayedTo = callback.Response.Headers.Location.ToString();

                    var appRedirect = new Uri(relayedTo);
                    followed = LoopbackTestHttp.GetAsync(appRedirect, appRedirect.PathAndQuery);
                }
            };

            var signIn = await client.SignInAsync(FakeRelayBrowser.CreateSignInUrl).WithTimeout();
            await followed.WithTimeout();

            Assert.True(signIn.IsSuccess);
            Assert.Matches("^http://127\\.0\\.0\\.1:[0-9]+/callback\\?state=[A-Za-z0-9_-]{43}&code=", relayedTo);
            var user = await manager.RedeemAppCodeAsync("Google", signIn.Code, signIn.CodeVerifier);
            Assert.Equal("user-1", user?.UserId);
        }

        private static OAuth2Manager CreateManager()
        {
            var handler = new StubHttpMessageHandler()
                .Respond(HttpStatusCode.OK, """{"access_token":"provider-access-token"}""")
                .Respond(HttpStatusCode.OK, UserJson);
            var services = new ServiceCollection();
            services.AddOAuth2Client("Google", new GoogleOAuth2Options { ClientId = "client-id", RedirectUri = FakeRelayBrowser.BackEnd + "auth/callback" }, handler.CreateClient());
            services.AddOAuth2AppRelay(options => options.AppRedirectUris.Add("http://127.0.0.1/callback"));
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            return services.BuildServiceProvider().GetRequiredService<OAuth2Manager>();
        }
    }
}
