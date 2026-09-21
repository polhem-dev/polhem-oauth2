using System.ComponentModel;
using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Polhem.OAuth2.AspNetCore;

namespace Polhem.OAuth2.UnitTests
{
    public class AspNetCoreAppRelayTests
    {
        private const string RedirectUri = "https://app.example.com/auth/callback";
        private const string AppRedirectUri = "com.example.app:/signin";
        private const string OtherAppRedirectUri = "com.example.other:/signin";
        private const string UserJson = """{"sub":"user-1","name":"User","email":"user@example.com"}""";

        [Theory]
        [DisplayName("AddOAuth2AppRelay rejects missing or invalid application redirect URIs and a code lifetime that is not positive")]
        [InlineData(null, 60)]
        [InlineData("http://app.example.com/signin", 60)]
        [InlineData("com.example.app:/signin#fragment", 60)]
        [InlineData("javascript:alert(1)", 60)]
        [InlineData("/signin", 60)]
        [InlineData(AppRedirectUri, 0)]
        public void AddOAuth2AppRelay_InvalidOptions_ThrowsArgumentException(string? appRedirectUri, int lifetimeSeconds)
        {
            var services = new ServiceCollection();

            Assert.Throws<ArgumentException>(() => services.AddOAuth2AppRelay(options =>
            {
                if (appRedirectUri is not null)
                    options.AppRedirectUris.Add(appRedirectUri);
                options.CodeLifetime = TimeSpan.FromSeconds(lifetimeSeconds);
            }));
        }

        [Fact]
        [DisplayName("AddOAuth2AppRelay rejects a second registration and a null configure action")]
        public void AddOAuth2AppRelay_SecondCallOrNullAction_Throws()
        {
            var services = new ServiceCollection();
            services.AddOAuth2AppRelay(options => options.AppRedirectUris.Add(AppRedirectUri));

            Assert.Throws<InvalidOperationException>(() => services.AddOAuth2AppRelay(options => options.AppRedirectUris.Add(AppRedirectUri)));
            Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddOAuth2AppRelay(null!));
        }

        [Fact]
        [DisplayName("AddOAuth2AppRelay registers the in-memory distributed cache, but keeps one that is already registered")]
        public void AddOAuth2AppRelay_DistributedCache_KeepsExistingRegistration()
        {
            var withoutCache = new ServiceCollection().AddOAuth2AppRelay(options => options.AppRedirectUris.Add(AppRedirectUri));
            var existing = new DictionaryCache();
            var withCache = new ServiceCollection().AddSingleton<IDistributedCache>(existing);
            withCache.AddOAuth2AppRelay(options => options.AppRedirectUris.Add(AppRedirectUri));

            Assert.IsType<MemoryDistributedCache>(withoutCache.BuildServiceProvider().GetRequiredService<IDistributedCache>());
            Assert.Same(existing, withCache.BuildServiceProvider().GetRequiredService<IDistributedCache>());
        }

        [Fact]
        [DisplayName("The relay members throw InvalidOperationException when the relay is not registered")]
        public async Task RelayMembers_RelayNotRegistered_ThrowInvalidOperationException()
        {
            var services = new ServiceCollection();
            services.AddOAuth2Client("Google", CreateOptions(), new StubHttpMessageHandler().CreateClient());
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            var manager = services.BuildServiceProvider().GetRequiredService<OAuth2Manager>();

            Assert.Throws<InvalidOperationException>(() => manager.RedirectToAppAuthorization(CreateContext(), "Google", AppRedirectUri, CreateChallenge().Challenge));
            await Assert.ThrowsAsync<InvalidOperationException>(() => manager.RedeemAppCodeAsync("Google", "code", Pkce.GenerateCodeVerifier()));
        }

        [Theory]
        [DisplayName("RedirectToAppAuthorization rejects an application redirect URI that is not registered")]
        [InlineData(OtherAppRedirectUri)]
        [InlineData("com.example.app:/signin/")]
        [InlineData("COM.EXAMPLE.APP:/signin")]
        public void RedirectToAppAuthorization_UnregisteredAppRedirectUri_ThrowsArgumentException(string appRedirectUri)
        {
            var (manager, _) = CreateManager(new StubHttpMessageHandler());

            Assert.Throws<ArgumentException>(() => manager.RedirectToAppAuthorization(CreateContext(), "Google", appRedirectUri, CreateChallenge().Challenge));
        }

        [Theory]
        [DisplayName("RedirectToAppAuthorization rejects a code challenge that is not 43 base64url characters")]
        [InlineData("")]
        [InlineData("abc")]
        [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa+")]
        [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
        public void RedirectToAppAuthorization_InvalidChallenge_ThrowsArgumentException(string challenge)
        {
            var (manager, _) = CreateManager(new StubHttpMessageHandler());

            Assert.Throws<ArgumentException>(() => manager.RedirectToAppAuthorization(CreateContext(), "Google", AppRedirectUri, challenge));
        }

        [Fact]
        [DisplayName("RedirectToAppAuthorization redirects to the provider with the web redirect URI and keeps the sign-in in a __Host- cookie")]
        public void RedirectToAppAuthorization_RegisteredApp_RedirectsWithWebRedirectUri()
        {
            var (manager, _) = CreateManager(new StubHttpMessageHandler());
            var context = CreateContext();

            manager.RedirectToAppAuthorization(context, "Google", AppRedirectUri, CreateChallenge().Challenge);

            string location = context.Response.Headers.Location.ToString();
            Assert.Equal(RedirectUri, LoopbackTestHttp.GetQueryValue(location, "redirect_uri"));
            var cookie = Assert.Single(context.Response.GetTypedHeaders().SetCookie);
            Assert.StartsWith("__Host-oauth2.", cookie.Name.Value, StringComparison.Ordinal);
            Assert.True(cookie.Secure);
            Assert.True(cookie.HttpOnly);
            Assert.DoesNotContain(AppRedirectUri, cookie.Value.Value, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A relayed sign-in sends the provider the PKCE values of the web client, not the code challenge of the application")]
        public async Task RelayedSignIn_ProviderRequests_UseWebClientPkce()
        {
            var handler = SuccessfulProvider();
            var (manager, _) = CreateManager(handler);
            var (appVerifier, appChallenge) = CreateChallenge();
            var start = CreateContext();

            manager.RedirectToAppAuthorization(start, "Google", AppRedirectUri, appChallenge);

            string location = start.Response.Headers.Location.ToString();
            string? providerChallenge = LoopbackTestHttp.GetQueryValue(location, "code_challenge");
            Assert.NotNull(providerChallenge);
            Assert.NotEqual(appChallenge, providerChallenge);
            Assert.DoesNotContain(appChallenge, location, StringComparison.Ordinal);

            var cookie = Assert.Single(start.Response.GetTypedHeaders().SetCookie);
            var callback = await FinishRelayedSignInAsync(
                manager, new RelayStart(LoopbackTestHttp.GetQueryValue(location, "state")!, $"{cookie.Name}={cookie.Value}"), "code=abc");

            Assert.True(callback.Relayed);
            string providerVerifier = handler.Requests[0].FormValue("code_verifier")!;
            Assert.NotEqual(appVerifier, providerVerifier);
            Assert.Equal(providerChallenge, Pkce.GenerateCodeChallenge(providerVerifier));
        }

        [Fact]
        [DisplayName("A relayed sign-in returns a code to the application, which redeems it once for the user information")]
        public async Task RelayedSignIn_Success_RedeemsCodeOnce()
        {
            var (manager, _) = CreateManager(SuccessfulProvider());
            var (verifier, challenge) = CreateChallenge();

            var callback = await CompleteRelayedSignInAsync(manager, AppRedirectUri, challenge, "code=abc");

            Assert.True(callback.Relayed);
            Assert.StartsWith(AppRedirectUri + "?code=", callback.Location, StringComparison.Ordinal);
            Assert.DoesNotContain("provider-access-token", callback.Location, StringComparison.Ordinal);
            string code = LoopbackTestHttp.GetQueryValue(callback.Location, "code")!;

            var user = await manager.RedeemAppCodeAsync("Google", code, verifier);

            Assert.Equal("user-1", user?.UserId);
            Assert.Equal("user@example.com", user?.Email);
            Assert.Equal(UserJson, user?.RawJson);
            Assert.Null(await manager.RedeemAppCodeAsync("Google", code, verifier));
        }

        [Fact]
        [DisplayName("RedeemAppCodeAsync rejects a wrong verifier and removes the code, so the right verifier fails afterwards")]
        public async Task RedeemAppCodeAsync_WrongVerifier_ConsumesCode()
        {
            var (manager, _) = CreateManager(SuccessfulProvider());
            var (verifier, challenge) = CreateChallenge();
            var callback = await CompleteRelayedSignInAsync(manager, AppRedirectUri, challenge, "code=abc");
            string code = LoopbackTestHttp.GetQueryValue(callback.Location, "code")!;

            Assert.Null(await manager.RedeemAppCodeAsync("Google", code, Pkce.GenerateCodeVerifier()));
            Assert.Null(await manager.RedeemAppCodeAsync("Google", code, verifier));
        }

        [Fact]
        [DisplayName("RedeemAppCodeAsync rejects a code redeemed under another client name")]
        public async Task RedeemAppCodeAsync_OtherClient_ReturnsNull()
        {
            var (manager, _) = CreateManager(SuccessfulProvider());
            var (verifier, challenge) = CreateChallenge();
            var callback = await CompleteRelayedSignInAsync(manager, AppRedirectUri, challenge, "code=abc");

            Assert.Null(await manager.RedeemAppCodeAsync("Line", LoopbackTestHttp.GetQueryValue(callback.Location, "code")!, verifier));
        }

        [Fact]
        [DisplayName("RedeemAppCodeAsync rejects an expired code even when the cache still returns it")]
        public async Task RedeemAppCodeAsync_Expired_ReturnsNull()
        {
            var cache = new DictionaryCache();
            var (manager, _) = CreateManager(SuccessfulProvider(), options => options.CodeLifetime = TimeSpan.FromMilliseconds(1), cache);
            var (verifier, challenge) = CreateChallenge();
            var callback = await CompleteRelayedSignInAsync(manager, AppRedirectUri, challenge, "code=abc");
            await Task.Delay(50);

            Assert.Null(await manager.RedeemAppCodeAsync("Google", LoopbackTestHttp.GetQueryValue(callback.Location, "code")!, verifier));
        }

        [Theory]
        [DisplayName("RedeemAppCodeAsync returns null for a malformed code or verifier")]
        [InlineData("", "verifier")]
        [InlineData("not base64url!", "verifier")]
        [InlineData("code", "short")]
        [InlineData("code", "has a space in the verifier, which RFC 7636 does not allow at all")]
        public async Task RedeemAppCodeAsync_MalformedInput_ReturnsNull(string code, string verifier)
        {
            var (manager, _) = CreateManager(new StubHttpMessageHandler());

            Assert.Null(await manager.RedeemAppCodeAsync("Google", code, verifier));
        }

        [Fact]
        [DisplayName("The cache holds a hash of the code, not the code itself")]
        public async Task RelayedSignIn_CacheKey_DoesNotContainCode()
        {
            var cache = new DictionaryCache();
            var (manager, _) = CreateManager(SuccessfulProvider(), cache: cache);
            var callback = await CompleteRelayedSignInAsync(manager, AppRedirectUri, CreateChallenge().Challenge, "code=abc");
            string code = LoopbackTestHttp.GetQueryValue(callback.Location, "code")!;

            string key = Assert.Single(cache.Keys);
            Assert.DoesNotContain(code, key, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A relayed sign-in that the provider refused returns its error code to the application, without a code")]
        public async Task RelayedSignIn_ProviderError_RedirectsWithError()
        {
            var cache = new DictionaryCache();
            var (manager, _) = CreateManager(new StubHttpMessageHandler(), cache: cache);

            var callback = await CompleteRelayedSignInAsync(manager, AppRedirectUri, CreateChallenge().Challenge, "error=access_denied&error_description=Denied");

            Assert.True(callback.Relayed);
            Assert.Equal(AppRedirectUri + "?error=access_denied", callback.Location);
            Assert.Empty(cache.Keys);
        }

        [Fact]
        [DisplayName("A relayed sign-in that failed for another reason returns a generic error without its details")]
        public async Task RelayedSignIn_OtherFailure_RedirectsWithGenericError()
        {
            var handler = new StubHttpMessageHandler().Fail(new HttpRequestException("secret detail"));
            var (manager, _) = CreateManager(handler);

            var callback = await CompleteRelayedSignInAsync(manager, AppRedirectUri, CreateChallenge().Challenge, "code=abc");

            Assert.Equal(AppRedirectUri + "?error=sign_in_failed", callback.Location);
        }

        [Fact]
        [DisplayName("RedirectToAppAsync appends the code with & when the application redirect URI already has a query")]
        public async Task RedirectToAppAsync_AppRedirectUriWithQuery_AppendsWithAmpersand()
        {
            const string withQuery = "com.example.app:/signin?from=relay";
            var (manager, _) = CreateManager(SuccessfulProvider(), options => options.AppRedirectUris.Add(withQuery));

            var callback = await CompleteRelayedSignInAsync(manager, withQuery, CreateChallenge().Challenge, "code=abc");

            Assert.StartsWith(withQuery + "&code=", callback.Location, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("RedirectToAppAsync returns false for a web sign-in and leaves the response unchanged")]
        public async Task RedirectToAppAsync_WebSignIn_ReturnsFalse()
        {
            var (manager, _) = CreateManager(SuccessfulProvider());
            var start = CreateContext();
            string url = manager.CreateAuthorizationUrl(start, "Google");
            var cookie = Assert.Single(start.Response.GetTypedHeaders().SetCookie);
            var context = CreateContext("?code=abc&state=" + LoopbackTestHttp.GetQueryValue(url, "state"), $"{cookie.Name}={cookie.Value}");

            var result = await manager.CompleteAuthorizationAsync(context);
            bool relayed = await manager.RedirectToAppAsync(context, result);

            Assert.True(result.IsSuccess);
            Assert.False(relayed);
            Assert.Equal(0, context.Response.Headers.Location.Count);
        }

        [Fact]
        [DisplayName("Relayed sign-ins in progress at the same time return to their own applications")]
        public async Task RelayedSignIns_InParallel_KeepTheirOwnApplications()
        {
            var handler = new StubHttpMessageHandler()
                .Respond(HttpStatusCode.OK, """{"access_token":"first"}""").Respond(HttpStatusCode.OK, UserJson)
                .Respond(HttpStatusCode.OK, """{"access_token":"second"}""").Respond(HttpStatusCode.OK, UserJson);
            var (manager, _) = CreateManager(handler, options => options.AppRedirectUris.Add(OtherAppRedirectUri));
            var first = StartRelayedSignIn(manager, AppRedirectUri, CreateChallenge().Challenge);
            var second = StartRelayedSignIn(manager, OtherAppRedirectUri, CreateChallenge().Challenge);

            var secondCallback = await FinishRelayedSignInAsync(manager, second, "code=abc");
            var firstCallback = await FinishRelayedSignInAsync(manager, first, "code=abc");

            Assert.StartsWith(OtherAppRedirectUri + "?code=", secondCallback.Location, StringComparison.Ordinal);
            Assert.StartsWith(AppRedirectUri + "?code=", firstCallback.Location, StringComparison.Ordinal);
        }

        private static StubHttpMessageHandler SuccessfulProvider()
        {
            return new StubHttpMessageHandler()
                .Respond(HttpStatusCode.OK, """{"access_token":"provider-access-token"}""")
                .Respond(HttpStatusCode.OK, UserJson);
        }

        private static (string Verifier, string Challenge) CreateChallenge()
        {
            string verifier = Pkce.GenerateCodeVerifier();
            return (verifier, Pkce.GenerateCodeChallenge(verifier));
        }

        private static async Task<RelayCallback> CompleteRelayedSignInAsync(OAuth2Manager manager, string appRedirectUri, string challenge, string parameters)
        {
            return await FinishRelayedSignInAsync(manager, StartRelayedSignIn(manager, appRedirectUri, challenge), parameters);
        }

        private static RelayStart StartRelayedSignIn(OAuth2Manager manager, string appRedirectUri, string challenge)
        {
            var context = CreateContext();
            manager.RedirectToAppAuthorization(context, "Google", appRedirectUri, challenge);
            var cookie = Assert.Single(context.Response.GetTypedHeaders().SetCookie);
            string state = LoopbackTestHttp.GetQueryValue(context.Response.Headers.Location.ToString(), "state")!;
            return new RelayStart(state, $"{cookie.Name}={cookie.Value}");
        }

        private static async Task<RelayCallback> FinishRelayedSignInAsync(OAuth2Manager manager, RelayStart start, string parameters)
        {
            var context = CreateContext("?" + parameters + "&state=" + start.State, start.Cookie);
            var result = await manager.CompleteAuthorizationAsync(context);
            bool relayed = await manager.RedirectToAppAsync(context, result);
            return new RelayCallback(relayed, context.Response.Headers.Location.ToString());
        }

        private static (OAuth2Manager Manager, IServiceProvider Services) CreateManager(
            StubHttpMessageHandler handler, Action<OAuth2AppRelayOptions>? configure = null, IDistributedCache? cache = null)
        {
            var services = new ServiceCollection();
            if (cache is not null)
                services.AddSingleton(cache);
            services.AddOAuth2Client("Google", CreateOptions(), handler.CreateClient());
            services.AddOAuth2AppRelay(options =>
            {
                options.AppRedirectUris.Add(AppRedirectUri);
                configure?.Invoke(options);
            });
            // Registered last, so the manager uses keys in memory instead of the key ring that AddDataProtection keeps on disk.
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            var provider = services.BuildServiceProvider();
            return (provider.GetRequiredService<OAuth2Manager>(), provider);
        }

        private static OAuth2Options CreateOptions()
        {
            return new GoogleOAuth2Options { ClientId = "client-id", RedirectUri = RedirectUri };
        }

        private static DefaultHttpContext CreateContext(string? queryString = null, string? cookie = null)
        {
            var context = new DefaultHttpContext();
            context.Request.Scheme = "https";
            context.Request.Host = new HostString("app.example.com");
            if (queryString is not null)
                context.Request.QueryString = new QueryString(queryString);
            if (cookie is not null)
                context.Request.Headers.Cookie = cookie;
            return context;
        }

        private sealed record RelayStart(string State, string Cookie);

        private sealed record RelayCallback(bool Relayed, string Location);

        /// <summary>
        /// A distributed cache that ignores expiration, so that the relay's own expiry check is tested, and that exposes its keys.
        /// </summary>
        private sealed class DictionaryCache : IDistributedCache
        {
            private readonly Dictionary<string, byte[]> _entries = new(StringComparer.Ordinal);

            public IReadOnlyCollection<string> Keys => _entries.Keys;

            public byte[]? Get(string key) => _entries.TryGetValue(key, out var value) ? value : null;

            public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));

            public void Refresh(string key)
            {
            }

            public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

            public void Remove(string key) => _entries.Remove(key);

            public Task RemoveAsync(string key, CancellationToken token = default)
            {
                Remove(key);
                return Task.CompletedTask;
            }

            public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => _entries[key] = value;

            public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
            {
                Set(key, value, options);
                return Task.CompletedTask;
            }
        }
    }
}
