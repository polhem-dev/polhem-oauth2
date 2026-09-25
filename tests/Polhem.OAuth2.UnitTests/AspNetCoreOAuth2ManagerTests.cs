using System.ComponentModel;
using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Polhem.OAuth2.AspNetCore;

namespace Polhem.OAuth2.UnitTests
{
    public class AspNetCoreOAuth2ManagerTests
    {
        private const string RedirectUri = "https://app.example.com/auth/callback";

        [Fact]
        [DisplayName("AddOAuth2Client rejects a second client under the same name")]
        public void AddOAuth2Client_DuplicateName_ThrowsInvalidOperationException()
        {
            var services = new ServiceCollection();
            services.AddOAuth2Client("Google", CreateOptions("Google"));

            Assert.Throws<InvalidOperationException>(() => services.AddOAuth2Client("Google", CreateOptions("Google")));
        }

        [Theory]
        [DisplayName("AddOAuth2Client rejects an empty client name")]
        [InlineData("")]
        [InlineData("   ")]
        public void AddOAuth2Client_EmptyName_ThrowsArgumentException(string clientName)
        {
            Assert.Throws<ArgumentException>(() => new ServiceCollection().AddOAuth2Client(clientName, CreateOptions("Google")));
        }

        [Fact]
        [DisplayName("AddOAuth2Client reports invalid options when it is called")]
        public void AddOAuth2Client_InvalidOptions_ThrowsArgumentException()
        {
            var options = CreateOptions("Google");
            options.ClientId = string.Empty;

            Assert.Throws<ArgumentException>(() => new ServiceCollection().AddOAuth2Client("Google", options));
        }

        [Fact]
        [DisplayName("AddOAuth2ClientWithHttpClientFactory resolves the HTTP client from the service provider for each request to the provider")]
        public async Task AddOAuth2ClientWithHttpClientFactory_ResolvesClientPerRequest()
        {
            var handler = new StubHttpMessageHandler()
                .Respond(HttpStatusCode.OK, """{"access_token":"access"}""")
                .Respond(HttpStatusCode.OK, """{"sub":"1"}""");
            int resolved = 0;
            var services = new ServiceCollection();
            services.AddSingleton(handler);
            services.AddOAuth2ClientWithHttpClientFactory("Google", CreateOptions("Google"), provider =>
            {
                resolved++;
                return provider.GetRequiredService<StubHttpMessageHandler>().CreateClient();
            });
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            var manager = services.BuildServiceProvider().GetRequiredService<OAuth2Manager>();
            var signIn = StartSignIn(manager, "Google");

            var result = await manager.CompleteAuthorizationAsync(AspNetCoreTestContext.Create($"?code=abc&state={signIn.State}", signIn.Cookie));

            Assert.True(result.IsSuccess);
            Assert.Equal(2, resolved);
            Assert.Equal(2, handler.Requests.Count);
        }

        [Fact]
        [DisplayName("AddOAuth2ClientWithHttpClientFactory reports invalid options, a null factory and a duplicate name when it is called")]
        public void AddOAuth2ClientWithHttpClientFactory_InvalidArguments_Throw()
        {
            var services = new ServiceCollection();
            static HttpClient Factory(IServiceProvider provider) => new StubHttpMessageHandler().CreateClient();

            Assert.Throws<ArgumentException>(() => services.AddOAuth2ClientWithHttpClientFactory("Google", new GoogleOAuth2Options(), Factory));
            Assert.Throws<ArgumentNullException>(() => services.AddOAuth2ClientWithHttpClientFactory("Google", CreateOptions("Google"), null!));
            services.AddOAuth2Client("Google", CreateOptions("Google"));
            Assert.Throws<InvalidOperationException>(() => services.AddOAuth2ClientWithHttpClientFactory("Google", CreateOptions("Google"), Factory));
        }

        [Fact]
        [DisplayName("A test double derived through the protected constructor overrides what it needs and has no clients or relay otherwise")]
        public async Task ProtectedConstructor_TestDouble_OverridesMembersAndKeepsSafeDefaults()
        {
            var expected = AuthorizationResult.Failure(new OAuth2Exception("From the test double."));
            OAuth2Manager manager = new FakeManager(expected);
            var context = AspNetCoreTestContext.Create();

            Assert.Same(expected, await manager.CompleteAuthorizationAsync(context));
            Assert.Null(manager.GetClient("Google"));
            Assert.Throws<InvalidOperationException>(() => manager.CreateAuthorizationUrl(context, "Google"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => manager.RedeemAppCodeAsync("Google", "code", "verifier"));
        }

        [Fact]
        [DisplayName("GetClient returns the client registered under a name, and null for an unknown name")]
        public void GetClient_RegisteredAndUnknownNames_ReturnsClientOrNull()
        {
            var manager = CreateManager(new StubHttpMessageHandler());

            Assert.Equal("Google", manager.GetClient("Google")?.ProviderName);
            Assert.Null(manager.GetClient("Unknown"));
        }

        [Fact]
        [DisplayName("CreateAuthorizationUrl keeps the sign-in in a protected __Host- cookie that is Secure, HTTP-only and SameSite=Lax")]
        public void CreateAuthorizationUrl_RegisteredClient_SetsProtectedCookie()
        {
            var manager = CreateManager(new StubHttpMessageHandler());
            var context = AspNetCoreTestContext.Create();

            string url = manager.CreateAuthorizationUrl(context, "Google");

            var cookie = Assert.Single(context.Response.GetTypedHeaders().SetCookie);
            string state = LoopbackTestHttp.GetQueryValue(url, "state")!;
            Assert.Equal("__Host-oauth2." + state, cookie.Name.Value);
            Assert.True(cookie.Secure);
            Assert.True(cookie.HttpOnly);
            Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Lax, cookie.SameSite);
            Assert.Equal("/", cookie.Path.Value);
            Assert.Null(cookie.Domain.Value);
            Assert.Equal(TimeSpan.FromMinutes(10), cookie.MaxAge);
            Assert.DoesNotContain(state, cookie.Value.Value, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("CreateAuthorizationUrl rejects a client name that is not registered")]
        public void CreateAuthorizationUrl_UnregisteredClient_ThrowsInvalidOperationException()
        {
            var manager = CreateManager(new StubHttpMessageHandler());

            Assert.Throws<InvalidOperationException>(() => manager.CreateAuthorizationUrl(AspNetCoreTestContext.Create(), "Unknown"));
        }

        [Fact]
        [DisplayName("RedirectToAuthorization redirects the response to the authorization URL")]
        public void RedirectToAuthorization_RegisteredClient_RedirectsToAuthorizationUrl()
        {
            var manager = CreateManager(new StubHttpMessageHandler());
            var context = AspNetCoreTestContext.Create();

            manager.RedirectToAuthorization(context, "Google");

            Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
            Assert.StartsWith("https://accounts.google.com/", context.Response.Headers.Location.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("CompleteAuthorizationAsync exchanges the code with the kept values and removes the sign-in cookie")]
        public async Task CompleteAuthorizationAsync_MatchingCookie_ReturnsSuccessAndRemovesCookie()
        {
            var handler = new StubHttpMessageHandler()
                .Respond(HttpStatusCode.OK, """{"access_token":"access"}""")
                .Respond(HttpStatusCode.OK, """{"sub":"1","name":"Ada"}""");
            var manager = CreateManager(handler);
            var signIn = StartSignIn(manager, "Google");
            var callback = AspNetCoreTestContext.Create($"?code=abc&state={signIn.State}", signIn.Cookie);

            var result = await manager.CompleteAuthorizationAsync(callback);

            Assert.True(result.IsSuccess);
            Assert.Equal("1", result.UserInfo.UserId);
            var tokenRequest = handler.Requests[0];
            Assert.Equal(RedirectUri, tokenRequest.FormValue("redirect_uri"));
            Assert.Equal(Pkce.GenerateCodeChallenge(tokenRequest.FormValue("code_verifier")!), LoopbackTestHttp.GetQueryValue(signIn.Url, "code_challenge"));
            var removal = Assert.Single(callback.Response.GetTypedHeaders().SetCookie);
            Assert.Equal(signIn.CookieName, removal.Name.Value);
            Assert.True(removal.Expires < DateTimeOffset.UtcNow);
            Assert.Null(removal.MaxAge);
            // A browser applies the removal of a __Host- cookie only with the attributes it was set with.
            Assert.True(removal.Secure);
            Assert.True(removal.HttpOnly);
            Assert.Equal("/", removal.Path.Value);
            Assert.Null(removal.Domain.Value);
        }

        [Fact]
        [DisplayName("CompleteAuthorizationAsync removes the sign-in cookie before it sends the token request")]
        public async Task CompleteAuthorizationAsync_TokenRequest_IsSentAfterCookieRemoval()
        {
            DefaultHttpContext? callback = null;
            string?[] removedWhenRequested = [];
            var handler = new StubHttpMessageHandler()
                .Respond(
                    HttpStatusCode.OK,
                    """{"access_token":"access"}""",
                    () => removedWhenRequested = callback!.Response.GetTypedHeaders().SetCookie.Select(cookie => cookie.Name.Value).ToArray())
                .Respond(HttpStatusCode.OK, """{"sub":"1","name":"Ada"}""");
            var manager = CreateManager(handler);
            var signIn = StartSignIn(manager, "Google");
            callback = AspNetCoreTestContext.Create($"?code=abc&state={signIn.State}", signIn.Cookie);

            var result = await manager.CompleteAuthorizationAsync(callback);

            Assert.True(result.IsSuccess);
            Assert.Equal(signIn.CookieName, Assert.Single(removedWhenRequested));
        }

        [Theory]
        [DisplayName("CompleteAuthorizationAsync removes the sign-in cookie when the sign-in fails, after an error redirect or an error from the token endpoint")]
        [InlineData("error=access_denied")]
        [InlineData("code=abc")]
        public async Task CompleteAuthorizationAsync_FailedSignIn_StillRemovesCookie(string parameters)
        {
            var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""");
            var manager = CreateManager(handler);
            var signIn = StartSignIn(manager, "Google");
            var callback = AspNetCoreTestContext.Create($"?{parameters}&state={signIn.State}", signIn.Cookie);

            var result = await manager.CompleteAuthorizationAsync(callback);

            Assert.IsType<OAuth2Exception>(result.Exception);
            var removal = Assert.Single(callback.Response.GetTypedHeaders().SetCookie);
            Assert.Equal(signIn.CookieName, removal.Name.Value);
            Assert.True(removal.Expires < DateTimeOffset.UtcNow);
        }

        [Fact]
        [DisplayName("Two sign-ins started in the same browser do not affect each other")]
        public async Task CompleteAuthorizationAsync_TwoPendingSignIns_CompletesTheOneThatReturns()
        {
            var handler = new StubHttpMessageHandler()
                .Respond(HttpStatusCode.OK, """{"access_token":"access"}""")
                .Respond(HttpStatusCode.OK, """{"sub":"1"}""");
            var manager = CreateManager(handler, "Google", "Line");
            var first = StartSignIn(manager, "Google");
            var second = StartSignIn(manager, "Line");

            var result = await manager.CompleteAuthorizationAsync(AspNetCoreTestContext.Create($"?code=abc&state={first.State}", first.Cookie, second.Cookie));

            Assert.NotEqual(first.CookieName, second.CookieName);
            Assert.True(result.IsSuccess);
            Assert.Equal("Google", result.ProviderName);
        }

        [Theory]
        [DisplayName("CompleteAuthorizationAsync turns a missing, blank, repeated or malformed state into a failed result")]
        [InlineData("?code=abc")]
        [InlineData("?code=abc&state=")]
        [InlineData("?code=abc&state=%20")]
        [InlineData("?code=abc&state=a&state=b")]
        [InlineData("?code=abc&state=a%3Bb")]
        public async Task CompleteAuthorizationAsync_InvalidState_ReturnsFailedResult(string queryString)
        {
            var handler = new StubHttpMessageHandler();
            var manager = CreateManager(handler);

            var result = await manager.CompleteAuthorizationAsync(AspNetCoreTestContext.Create(queryString));

            Assert.IsType<OAuth2Exception>(result.Exception);
            Assert.Empty(handler.Requests);
        }

        [Theory]
        [DisplayName("CompleteAuthorizationAsync turns a sign-in that started more than 10 minutes ago into a failed result, without a request to the provider")]
        [InlineData(600, true)]
        [InlineData(601, false)]
        public async Task CompleteAuthorizationAsync_CookieAge_DecidesResult(int seconds, bool succeeds)
        {
            var clock = new AspNetCoreTestClock();
            var handler = new StubHttpMessageHandler()
                .Respond(HttpStatusCode.OK, """{"access_token":"access"}""")
                .Respond(HttpStatusCode.OK, """{"sub":"1"}""");
            var manager = CreateManager(handler, new EphemeralDataProtectionProvider(), clock);
            var signIn = StartSignIn(manager, "Google");

            clock.Advance(TimeSpan.FromSeconds(seconds));
            var result = await manager.CompleteAuthorizationAsync(AspNetCoreTestContext.Create($"?code=abc&state={signIn.State}", signIn.Cookie));

            Assert.Equal(succeeds, result.IsSuccess);
            if (!succeeds)
            {
                Assert.IsType<OAuth2Exception>(result.Exception);
                Assert.Empty(handler.Requests);
            }
        }

        [Fact]
        [DisplayName("CompleteAuthorizationAsync turns a state without a sign-in cookie into a failed result")]
        public async Task CompleteAuthorizationAsync_NoCookie_ReturnsFailedResult()
        {
            var handler = new StubHttpMessageHandler();
            var manager = CreateManager(handler);
            var signIn = StartSignIn(manager, "Google");

            var result = await manager.CompleteAuthorizationAsync(AspNetCoreTestContext.Create($"?code=abc&state={signIn.State}"));

            Assert.IsType<OAuth2Exception>(result.Exception);
            Assert.Empty(handler.Requests);
        }

        [Theory]
        [DisplayName("CompleteAuthorizationAsync turns a cookie that cannot be decrypted into a failed result")]
        [InlineData("tampered")]
        [InlineData("!!!")]
        public async Task CompleteAuthorizationAsync_UndecryptableCookie_ReturnsFailedResultWithCryptographicException(string value)
        {
            var handler = new StubHttpMessageHandler();
            var manager = CreateManager(handler);
            var signIn = StartSignIn(manager, "Google");

            var result = await manager.CompleteAuthorizationAsync(AspNetCoreTestContext.Create($"?code=abc&state={signIn.State}", $"{signIn.CookieName}={value}"));

            Assert.IsAssignableFrom<CryptographicException>(result.Exception);
            Assert.Empty(handler.Requests);
        }

        [Fact]
        [DisplayName("CompleteAuthorizationAsync rejects the cookie of another sign-in placed under the name of this state")]
        public async Task CompleteAuthorizationAsync_CookieOfAnotherSignIn_ReturnsFailedResult()
        {
            var handler = new StubHttpMessageHandler();
            var manager = CreateManager(handler);
            var first = StartSignIn(manager, "Google");
            var second = StartSignIn(manager, "Google");

            var result = await manager.CompleteAuthorizationAsync(AspNetCoreTestContext.Create($"?code=abc&state={first.State}", $"{first.CookieName}={second.CookieValue}"));

            Assert.IsType<OAuth2Exception>(result.Exception);
            Assert.Empty(handler.Requests);
        }

        [Fact]
        [DisplayName("CompleteAuthorizationAsync turns an error redirect into a failed result with the error code")]
        public async Task CompleteAuthorizationAsync_ErrorRedirect_ReturnsFailedResultWithErrorCode()
        {
            var handler = new StubHttpMessageHandler();
            var manager = CreateManager(handler);
            var signIn = StartSignIn(manager, "Google");

            var result = await manager.CompleteAuthorizationAsync(
                AspNetCoreTestContext.Create($"?error=access_denied&error_description=Denied&state={signIn.State}", signIn.Cookie));

            Assert.Equal("access_denied", Assert.IsType<OAuth2Exception>(result.Exception).Error);
            Assert.Empty(handler.Requests);
        }

        [Fact]
        [DisplayName("CompleteAuthorizationAsync throws when the sign-in names a client that is no longer registered")]
        public async Task CompleteAuthorizationAsync_UnregisteredClientInCookie_ThrowsInvalidOperationException()
        {
            var dataProtection = new EphemeralDataProtectionProvider();
            var starter = CreateManager(new StubHttpMessageHandler(), dataProtection, "Google", "Line");
            var completer = CreateManager(new StubHttpMessageHandler(), dataProtection, "Google");
            var signIn = StartSignIn(starter, "Line");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => completer.CompleteAuthorizationAsync(AspNetCoreTestContext.Create($"?code=abc&state={signIn.State}", signIn.Cookie)));
        }

        [Fact]
        [DisplayName("CompleteAuthorizationAsync stops the requests to the provider when the request is aborted")]
        public async Task CompleteAuthorizationAsync_RequestAborted_ThrowsOperationCanceledException()
        {
            var handler = new StubHttpMessageHandler().Hang();
            var manager = CreateManager(handler);
            var signIn = StartSignIn(manager, "Google");
            using var aborted = new CancellationTokenSource();
            var callback = AspNetCoreTestContext.Create($"?code=abc&state={signIn.State}", signIn.Cookie);
            callback.RequestAborted = aborted.Token;

            var completion = manager.CompleteAuthorizationAsync(callback);
            await handler.Hanging.WithTimeout();
            await aborted.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => completion.WithTimeout());
        }

        [Fact]
        [DisplayName("The manager rejects a null HTTP context")]
        public async Task Methods_NullContext_ThrowArgumentNullException()
        {
            var manager = CreateManager(new StubHttpMessageHandler());

            Assert.Throws<ArgumentNullException>(() => manager.CreateAuthorizationUrl(null!, "Google"));
            await Assert.ThrowsAsync<ArgumentNullException>(() => manager.CompleteAuthorizationAsync(null!));
        }

        private static OAuth2Manager CreateManager(StubHttpMessageHandler handler, params string[] clientNames)
        {
            return CreateManager(handler, new EphemeralDataProtectionProvider(), clientNames);
        }

        private static OAuth2Manager CreateManager(StubHttpMessageHandler handler, IDataProtectionProvider dataProtection, params string[] clientNames)
        {
            return CreateManager(handler, dataProtection, clock: null, clientNames);
        }

        private static OAuth2Manager CreateManager(StubHttpMessageHandler handler, IDataProtectionProvider dataProtection, TimeProvider? clock, params string[] clientNames)
        {
            var services = new ServiceCollection();
            foreach (string clientName in clientNames.Length == 0 ? new[] { "Google" } : clientNames)
                services.AddOAuth2Client(clientName, CreateOptions(clientName), handler.CreateClient());

            // Registered last, so the manager uses keys in memory instead of the key ring that AddDataProtection keeps on disk.
            services.AddSingleton(dataProtection);
            if (clock is not null)
                services.AddSingleton(clock);
            return services.BuildServiceProvider().GetRequiredService<OAuth2Manager>();
        }

        private static OAuth2Options CreateOptions(string clientName)
        {
            OAuth2Options options = clientName == "Line" ? new LineOAuth2Options() : new GoogleOAuth2Options();
            options.ClientId = "client-id";
            options.RedirectUri = RedirectUri;
            return options;
        }

        private static SignIn StartSignIn(OAuth2Manager manager, string clientName)
        {
            var context = AspNetCoreTestContext.Create();
            string url = manager.CreateAuthorizationUrl(context, clientName);
            var cookie = Assert.Single(context.Response.GetTypedHeaders().SetCookie);
            return new SignIn(url, LoopbackTestHttp.GetQueryValue(url, "state")!, cookie.Name.Value!, cookie.Value.Value!);
        }

        private sealed record SignIn(string Url, string State, string CookieName, string CookieValue)
        {
            public string Cookie => $"{CookieName}={CookieValue}";
        }

        private sealed class FakeManager : OAuth2Manager
        {
            private readonly AuthorizationResult _result;

            public FakeManager(AuthorizationResult result)
            {
                _result = result;
            }

            public override Task<AuthorizationResult> CompleteAuthorizationAsync(HttpContext context, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_result);
            }
        }
    }
}
