using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Web;
using Polhem.OAuth2.AspNet;

namespace Polhem.OAuth2.UnitTests
{
    // The System.Web manager keeps its clients in a static registry, so every test registers a client name of its own.
    // MachineKey.Protect reads the test keys from App.config.
    public class AspNetOAuth2ManagerTests
    {
        private const string RedirectUri = "https://app.example.com/auth/callback";

        [Fact]
        [DisplayName("RegisterClient rejects a second client under the same name")]
        public void RegisterClient_DuplicateName_ThrowsInvalidOperationException()
        {
            string clientName = RegisterClient(new StubHttpMessageHandler());

            Assert.Throws<InvalidOperationException>(() => OAuth2Manager.RegisterClient(clientName, CreateOptions()));
        }

        [Theory]
        [DisplayName("RegisterClient rejects an empty client name")]
        [InlineData("")]
        [InlineData("   ")]
        public void RegisterClient_EmptyName_ThrowsArgumentException(string clientName)
        {
            Assert.Throws<ArgumentException>(() => OAuth2Manager.RegisterClient(clientName, CreateOptions()));
        }

        [Fact]
        [DisplayName("RegisterClient rejects invalid options without taking the name")]
        public void RegisterClient_InvalidOptions_ThrowsArgumentExceptionAndLeavesNameFree()
        {
            string clientName = NewClientName();
            var options = CreateOptions();
            options.ClientId = string.Empty;

            Assert.Throws<ArgumentException>(() => OAuth2Manager.RegisterClient(clientName, options));
            OAuth2Manager.RegisterClient(clientName, CreateOptions());
            Assert.NotNull(OAuth2Manager.GetClient(clientName));
        }

        [Fact]
        [DisplayName("GetClient returns the client registered under a name, and null for an unknown name")]
        public void GetClient_RegisteredAndUnknownNames_ReturnsClientOrNull()
        {
            string clientName = RegisterClient(new StubHttpMessageHandler());

            Assert.Equal("Google", OAuth2Manager.GetClient(clientName)?.ProviderName);
            Assert.Null(OAuth2Manager.GetClient(NewClientName()));
        }

        [Fact]
        [DisplayName("CreateAuthorizationUrl keeps the sign-in in a protected __Host- cookie that is Secure, HTTP-only and SameSite=Lax")]
        public void CreateAuthorizationUrl_RegisteredClient_SetsProtectedCookie()
        {
            string clientName = RegisterClient(new StubHttpMessageHandler());
            var context = new FakeHttpContext();

            string url = OAuth2Manager.CreateAuthorizationUrl(context, clientName);

            var cookie = Assert.Single(GetCookies(context.Response.Cookies));
            string state = LoopbackTestHttp.GetQueryValue(url, "state")!;
            Assert.Equal("__Host-oauth2." + state, cookie.Name);
            Assert.True(cookie.Secure);
            Assert.True(cookie.HttpOnly);
            Assert.Equal(SameSiteMode.Lax, cookie.SameSite);
            Assert.Equal("/", cookie.Path);
            Assert.Null(cookie.Domain);
            Assert.InRange(cookie.Expires, DateTime.UtcNow.AddMinutes(9), DateTime.UtcNow.AddMinutes(11));
            Assert.DoesNotContain(state, cookie.Value, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("CreateAuthorizationUrl rejects a client name that is not registered")]
        public void CreateAuthorizationUrl_UnregisteredClient_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() => OAuth2Manager.CreateAuthorizationUrl(new FakeHttpContext(), NewClientName()));
        }

        [Fact]
        [DisplayName("RedirectToAuthorization redirects the response without ending the thread")]
        public void RedirectToAuthorization_RegisteredClient_RedirectsToAuthorizationUrl()
        {
            string clientName = RegisterClient(new StubHttpMessageHandler());
            var context = new FakeHttpContext();

            OAuth2Manager.RedirectToAuthorization(context, clientName);

            Assert.Equal(302, context.Response.StatusCode);
            Assert.StartsWith("https://accounts.google.com/", context.Response.RedirectLocation, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("CompleteAuthorizationAsync exchanges the code with the kept values and removes the sign-in cookie")]
        public async Task CompleteAuthorizationAsync_MatchingCookie_ReturnsSuccessAndRemovesCookie()
        {
            var handler = new StubHttpMessageHandler()
                .Respond(HttpStatusCode.OK, """{"access_token":"access"}""")
                .Respond(HttpStatusCode.OK, """{"sub":"1","name":"Ada"}""");
            string clientName = RegisterClient(handler);
            var signIn = StartSignIn(clientName);
            var callback = new FakeHttpContext($"code=abc&state={signIn.State}", signIn.Cookie);

            var result = await OAuth2Manager.CompleteAuthorizationAsync(callback);

            Assert.True(result.IsSuccess);
            Assert.Equal("1", result.UserInfo.UserId);
            var tokenRequest = handler.Requests[0];
            Assert.Equal(RedirectUri, tokenRequest.FormValue("redirect_uri"));
            Assert.Equal(Pkce.GenerateCodeChallenge(tokenRequest.FormValue("code_verifier")!), LoopbackTestHttp.GetQueryValue(signIn.Url, "code_challenge"));
            var removal = Assert.Single(GetCookies(callback.Response.Cookies));
            Assert.Equal(signIn.Cookie.Name, removal.Name);
            Assert.True(removal.Expires < DateTime.UtcNow);
        }

        [Theory]
        [DisplayName("CompleteAuthorizationAsync turns a missing, blank, repeated or malformed state into a failed result")]
        [InlineData("code=abc")]
        [InlineData("code=abc&state=")]
        [InlineData("code=abc&state=%20")]
        [InlineData("code=abc&state=a&state=b")]
        [InlineData("code=abc&state=a%3Bb")]
        public async Task CompleteAuthorizationAsync_InvalidState_ReturnsFailedResult(string queryString)
        {
            var result = await OAuth2Manager.CompleteAuthorizationAsync(new FakeHttpContext(queryString));

            Assert.IsType<OAuth2Exception>(result.Exception);
        }

        [Fact]
        [DisplayName("CompleteAuthorizationAsync turns a state without a sign-in cookie into a failed result")]
        public async Task CompleteAuthorizationAsync_NoCookie_ReturnsFailedResult()
        {
            var handler = new StubHttpMessageHandler();
            var signIn = StartSignIn(RegisterClient(handler));

            var result = await OAuth2Manager.CompleteAuthorizationAsync(new FakeHttpContext($"code=abc&state={signIn.State}"));

            Assert.IsType<OAuth2Exception>(result.Exception);
            Assert.Empty(handler.Requests);
        }

        [Theory]
        [DisplayName("CompleteAuthorizationAsync turns a cookie that cannot be decrypted into a failed result")]
        [InlineData("tampered")]
        [InlineData("AAAAAAAAAAAA0")]
        public async Task CompleteAuthorizationAsync_UndecryptableCookie_ReturnsFailedResultWithCryptographicException(string value)
        {
            var handler = new StubHttpMessageHandler();
            var signIn = StartSignIn(RegisterClient(handler));

            var result = await OAuth2Manager.CompleteAuthorizationAsync(
                new FakeHttpContext($"code=abc&state={signIn.State}", new HttpCookie(signIn.Cookie.Name, value)));

            Assert.IsAssignableFrom<CryptographicException>(result.Exception);
            Assert.Empty(handler.Requests);
        }

        [Fact]
        [DisplayName("CompleteAuthorizationAsync turns an error redirect into a failed result with the error code")]
        public async Task CompleteAuthorizationAsync_ErrorRedirect_ReturnsFailedResultWithErrorCode()
        {
            var handler = new StubHttpMessageHandler();
            var signIn = StartSignIn(RegisterClient(handler));

            var result = await OAuth2Manager.CompleteAuthorizationAsync(
                new FakeHttpContext($"error=access_denied&error_description=Denied&state={signIn.State}", signIn.Cookie));

            Assert.Equal("access_denied", Assert.IsType<OAuth2Exception>(result.Exception).Error);
            Assert.Empty(handler.Requests);
        }

        [Fact]
        [DisplayName("The overloads without an HTTP context throw when there is no current request")]
        public async Task CurrentContextOverloads_NoCurrentRequest_ThrowInvalidOperationException()
        {
            string clientName = RegisterClient(new StubHttpMessageHandler());

            Assert.Throws<InvalidOperationException>(() => OAuth2Manager.CreateAuthorizationUrl(clientName));
            await Assert.ThrowsAsync<InvalidOperationException>(() => OAuth2Manager.CompleteAuthorizationAsync());
        }

        [Fact]
        [DisplayName("The manager rejects a null HTTP context")]
        public async Task Methods_NullContext_ThrowArgumentNullException()
        {
            string clientName = RegisterClient(new StubHttpMessageHandler());

            Assert.Throws<ArgumentNullException>(() => OAuth2Manager.CreateAuthorizationUrl(null!, clientName));
            await Assert.ThrowsAsync<ArgumentNullException>(() => OAuth2Manager.CompleteAuthorizationAsync(null!));
        }

        private static string NewClientName()
        {
            return "Test-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        }

        private static string RegisterClient(StubHttpMessageHandler handler)
        {
            string clientName = NewClientName();
            OAuth2Manager.RegisterClient(clientName, CreateOptions(), handler.CreateClient());
            return clientName;
        }

        private static GoogleOAuth2Options CreateOptions()
        {
            return new GoogleOAuth2Options { ClientId = "client-id", RedirectUri = RedirectUri };
        }

        private static SignIn StartSignIn(string clientName)
        {
            var context = new FakeHttpContext();
            string url = OAuth2Manager.CreateAuthorizationUrl(context, clientName);
            var cookie = Assert.Single(GetCookies(context.Response.Cookies));
            return new SignIn(url, LoopbackTestHttp.GetQueryValue(url, "state")!, new HttpCookie(cookie.Name, cookie.Value));
        }

        private static List<HttpCookie> GetCookies(HttpCookieCollection cookies)
        {
            var list = new List<HttpCookie>();
            for (int i = 0; i < cookies.Count; i++)
                list.Add(cookies[i]);
            return list;
        }

        private sealed class SignIn
        {
            public SignIn(string url, string state, HttpCookie cookie)
            {
                Url = url;
                State = state;
                Cookie = cookie;
            }

            public string Url { get; }

            public string State { get; }

            public HttpCookie Cookie { get; }
        }
    }
}
