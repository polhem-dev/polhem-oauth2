using System.ComponentModel;
using System.Net;
using System.Text;
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
        // The S256 challenge of the verifier in RFC 7636, appendix B.
        private const string ValidChallenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";
        private const string UserJson = """{"sub":"user-1","name":"User","email":"user@example.com"}""";

        [Theory]
        [DisplayName("AddOAuth2AppRelay rejects missing or invalid application redirect URIs and a code lifetime that is not positive or is longer than 10 minutes")]
        [InlineData(null, 60)]
        [InlineData("http://app.example.com/signin", 60)]
        [InlineData("com.example.app:/signin#fragment", 60)]
        [InlineData("javascript:alert(1)", 60)]
        [InlineData("data:text/html,x", 60)]
        [InlineData("file:///tmp/signin", 60)]
        [InlineData("", 60)]
        [InlineData("signin", 60)]
        [InlineData("/signin", 60)]
        [InlineData(AppRedirectUri, 0)]
        [InlineData(AppRedirectUri, -1)]
        [InlineData(AppRedirectUri, 601)]
        public void AddOAuth2AppRelay_InvalidOptions_ThrowsArgumentException(string? appRedirectUri, int lifetimeSeconds)
        {
            var services = new ServiceCollection();

            var exception = Assert.Throws<ArgumentException>(() => services.AddOAuth2AppRelay(options =>
            {
                if (appRedirectUri is not null)
                    options.AppRedirectUris.Add(appRedirectUri);
                options.CodeLifetime = TimeSpan.FromSeconds(lifetimeSeconds);
            }));

            Assert.Equal("configure", exception.ParamName);
        }

        [Theory]
        [DisplayName("AddOAuth2AppRelay accepts the application redirect URIs that OAuth2Options.IsAppRedirectUri accepts")]
        [InlineData("https://app.example.com/signin")]
        [InlineData("com.example.app:/signin?source=app")]
        [InlineData("msauth://com.example.app/2jmj7l5rSw0yVb%2FvlWAYkK%2FYBwk%3D")]
        [InlineData("line3rdp.com.example.app://auth")]
        [InlineData("fb1234567890://authorize")]
        public void AddOAuth2AppRelay_AppRedirectUri_IsAccepted(string appRedirectUri)
        {
            Assert.True(OAuth2Options.IsAppRedirectUri(appRedirectUri));

            new ServiceCollection().AddOAuth2AppRelay(options => options.AppRedirectUris.Add(appRedirectUri));
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
            Assert.Throws<InvalidOperationException>(() => manager.TryRedirectToAppAuthorization(CreateContext(), "Google", AppRedirectUri, CreateChallenge().Challenge));
            Assert.Throws<InvalidOperationException>(() => manager.TryRedirectToAppAuthorization(CreateContext(), "Unknown", "not registered", "not a challenge"));
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

        [Theory]
        [DisplayName("TryRedirectToAppAuthorization returns false for a value of the request that is not valid, and leaves the response unchanged")]
        [InlineData("Unknown", AppRedirectUri, ValidChallenge)]
        [InlineData("google", AppRedirectUri, ValidChallenge)]
        [InlineData("", AppRedirectUri, ValidChallenge)]
        [InlineData(null, AppRedirectUri, ValidChallenge)]
        [InlineData("Google", OtherAppRedirectUri, ValidChallenge)]
        [InlineData("Google", "COM.EXAMPLE.APP:/signin", ValidChallenge)]
        [InlineData("Google", AppRedirectUri + "/", ValidChallenge)]
        [InlineData("Google", "", ValidChallenge)]
        [InlineData("Google", null, ValidChallenge)]
        [InlineData("Google", AppRedirectUri, "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-c")]
        [InlineData("Google", AppRedirectUri, "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cMM")]
        [InlineData("Google", AppRedirectUri, "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw+cM")]
        [InlineData("Google", AppRedirectUri, "")]
        [InlineData("Google", AppRedirectUri, null)]
        public void TryRedirectToAppAuthorization_InvalidRequestValue_ReturnsFalse(string? clientName, string? appRedirectUri, string? codeChallenge)
        {
            var (manager, _) = CreateManager(new StubHttpMessageHandler());
            var context = CreateContext();

            bool started = manager.TryRedirectToAppAuthorization(context, clientName, appRedirectUri, codeChallenge);

            Assert.False(started);
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            Assert.Empty(context.Response.Headers.Location.ToString());
            Assert.Empty(context.Response.GetTypedHeaders().SetCookie);
        }

        [Fact]
        [DisplayName("TryRedirectToAppAuthorization starts the same relayed sign-in as RedirectToAppAuthorization")]
        public async Task TryRedirectToAppAuthorization_ValidRequest_StartsRelayedSignIn()
        {
            var (manager, _) = CreateManager(SuccessfulProvider());
            var (verifier, challenge) = CreateChallenge();
            var context = CreateContext();

            Assert.True(manager.TryRedirectToAppAuthorization(context, "Google", AppRedirectUri, challenge));

            string location = context.Response.Headers.Location.ToString();
            Assert.Equal(RedirectUri, LoopbackTestHttp.GetQueryValue(location, "redirect_uri"));
            var cookie = Assert.Single(context.Response.GetTypedHeaders().SetCookie);
            var callback = await FinishRelayedSignInAsync(
                manager, new RelayStart(LoopbackTestHttp.GetQueryValue(location, "state")!, $"{cookie.Name}={cookie.Value}"), "code=abc");
            string code = LoopbackTestHttp.GetQueryValue(callback.Location, "code")!;
            Assert.Equal("user-1", (await manager.RedeemAppCodeAsync("Google", code, verifier))?.UserId);
        }

        [Fact]
        [DisplayName("CreateAppAuthorizationUrl and TryCreateAppAuthorizationUrl return the URL and set the cookie without redirecting the response")]
        public void CreateAppAuthorizationUrl_ValidRequest_ReturnsUrlWithoutRedirect()
        {
            var (manager, _) = CreateManager(new StubHttpMessageHandler());
            var context = CreateContext();
            var tried = CreateContext();

            string url = manager.CreateAppAuthorizationUrl(context, "Google", AppRedirectUri, ValidChallenge);
            bool started = manager.TryCreateAppAuthorizationUrl(tried, "Google", AppRedirectUri, ValidChallenge, out string? triedUrl);

            Assert.True(started);
            foreach (var (response, location) in new[] { (context.Response, url), (tried.Response, triedUrl!) })
            {
                Assert.Equal(RedirectUri, LoopbackTestHttp.GetQueryValue(location, "redirect_uri"));
                Assert.Empty(response.Headers.Location.ToString());
                Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
                Assert.Single(response.GetTypedHeaders().SetCookie);
            }
            Assert.False(manager.TryCreateAppAuthorizationUrl(CreateContext(), "Unknown", AppRedirectUri, ValidChallenge, out string? none));
            Assert.Null(none);
            Assert.Throws<ArgumentException>(() => manager.CreateAppAuthorizationUrl(CreateContext(), "Google", OtherAppRedirectUri, ValidChallenge));
        }

        [Fact]
        [DisplayName("CreateAppRedirectUrlAsync returns the URL of the application with the code, or null for a web sign-in, without redirecting")]
        public async Task CreateAppRedirectUrlAsync_ReturnsUrlWithoutRedirect()
        {
            var (manager, _) = CreateManager(SuccessfulProvider());
            var (verifier, challenge) = CreateChallenge();
            var start = StartRelayedSignIn(manager, AppRedirectUri, challenge);
            var context = CreateContext("?code=abc&state=" + start.State, start.Cookie);
            var result = await manager.CompleteAuthorizationAsync(context);

            string? url = await manager.CreateAppRedirectUrlAsync(context, result);

            Assert.NotNull(url);
            Assert.StartsWith(AppRedirectUri + "?code=", url, StringComparison.Ordinal);
            Assert.Empty(context.Response.Headers.Location.ToString());
            Assert.Equal("user-1", (await manager.RedeemAppCodeAsync("Google", LoopbackTestHttp.GetQueryValue(url, "code")!, verifier))?.UserId);
            Assert.Null(await manager.CreateAppRedirectUrlAsync(CreateContext(), result));
        }

        [Fact]
        [DisplayName("TryRedirectToAppAuthorization rejects a null HTTP context")]
        public void TryRedirectToAppAuthorization_NullContext_ThrowsArgumentNullException()
        {
            var (manager, _) = CreateManager(new StubHttpMessageHandler());

            Assert.Throws<ArgumentNullException>(() => manager.TryRedirectToAppAuthorization(null!, "Google", AppRedirectUri, ValidChallenge));
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
            var clock = new AspNetCoreTestClock();
            var (manager, _) = CreateManager(SuccessfulProvider(), cache: new DictionaryCache(), clock: clock);
            var (verifier, challenge) = CreateChallenge();
            var callback = await CompleteRelayedSignInAsync(manager, AppRedirectUri, challenge, "code=abc");

            // The lifetime of one minute, and the minute by which the clock of another server may run ahead.
            clock.Advance(TimeSpan.FromMinutes(2));

            Assert.Null(await manager.RedeemAppCodeAsync("Google", LoopbackTestHttp.GetQueryValue(callback.Location, "code")!, verifier));
        }

        [Fact]
        [DisplayName("RedeemAppCodeAsync tolerates a server whose clock runs up to a minute ahead of the server that issued the code")]
        public async Task RedeemAppCodeAsync_ClockAheadWithinSkew_ReturnsUser()
        {
            var clock = new AspNetCoreTestClock();
            var (manager, _) = CreateManager(SuccessfulProvider(), cache: new DictionaryCache(), clock: clock);
            var (verifier, challenge) = CreateChallenge();
            var callback = await CompleteRelayedSignInAsync(manager, AppRedirectUri, challenge, "code=abc");

            clock.Advance(TimeSpan.FromMinutes(2) - TimeSpan.FromMilliseconds(1));

            var user = await manager.RedeemAppCodeAsync("Google", LoopbackTestHttp.GetQueryValue(callback.Location, "code")!, verifier);
            Assert.Equal("user-1", user?.UserId);
        }

        [Fact]
        [DisplayName("The cache ends the lifetime of a code at the code lifetime, without the tolerance for another clock")]
        public async Task RelayedSignIn_CacheEntry_ExpiresAtCodeLifetime()
        {
            var cache = new DictionaryCache();
            var (manager, _) = CreateManager(SuccessfulProvider(), options => options.CodeLifetime = TimeSpan.FromSeconds(90), cache);

            await CompleteRelayedSignInAsync(manager, AppRedirectUri, CreateChallenge().Challenge, "code=abc");

            Assert.Equal(TimeSpan.FromSeconds(90), Assert.Single(cache.Options).AbsoluteExpirationRelativeToNow);
        }

        [Fact]
        [DisplayName("AddOAuth2AppRelay accepts a code lifetime of 10 minutes")]
        public void AddOAuth2AppRelay_LongestCodeLifetime_IsAccepted()
        {
            var services = new ServiceCollection().AddOAuth2AppRelay(options =>
            {
                options.AppRedirectUris.Add(AppRedirectUri);
                options.CodeLifetime = TimeSpan.FromMinutes(10);
            });

            Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IDistributedCache));
        }

        [Theory]
        [DisplayName("RedeemAppCodeAsync returns null for a code that is not 43 base64url characters, without asking the cache")]
        [InlineData("")]
        [InlineData("not base64url!")]
        [InlineData("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-c")]
        [InlineData("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cMM")]
        [InlineData("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw+cM")]
        public async Task RedeemAppCodeAsync_MalformedCode_ReturnsNullWithoutCacheRead(string code)
        {
            var cache = new DictionaryCache();
            var (manager, _) = CreateManager(new StubHttpMessageHandler(), cache: cache);

            Assert.Null(await manager.RedeemAppCodeAsync("Google", code, Pkce.GenerateCodeVerifier()));
            Assert.Equal(0, cache.Reads);
        }

        [Theory]
        [DisplayName("RedeemAppCodeAsync returns null for a verifier that RFC 7636 does not allow, and does not use up the code")]
        [InlineData("short")]
        [InlineData("has a space in the verifier, which RFC 7636 does not allow at all")]
        [InlineData("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjX+")]
        [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
        public async Task RedeemAppCodeAsync_MalformedVerifier_ReturnsNullAndKeepsCode(string malformedVerifier)
        {
            var (manager, _) = CreateManager(SuccessfulProvider());
            var (verifier, challenge) = CreateChallenge();
            var callback = await CompleteRelayedSignInAsync(manager, AppRedirectUri, challenge, "code=abc");
            string code = LoopbackTestHttp.GetQueryValue(callback.Location, "code")!;

            Assert.Null(await manager.RedeemAppCodeAsync("Google", code, malformedVerifier));

            Assert.Equal("user-1", (await manager.RedeemAppCodeAsync("Google", code, verifier))?.UserId);
        }

        [Fact]
        [DisplayName("RedeemAppCodeAsync accepts a verifier of 128 characters with a period and a tilde, as RFC 7636 allows")]
        public async Task RedeemAppCodeAsync_LongestVerifierWithUnreservedCharacters_ReturnsUser()
        {
            var (manager, _) = CreateManager(SuccessfulProvider());
            string verifier = new string('a', 126) + ".~";
            var callback = await CompleteRelayedSignInAsync(manager, AppRedirectUri, Pkce.GenerateCodeChallenge(verifier), "code=abc");

            var user = await manager.RedeemAppCodeAsync("Google", LoopbackTestHttp.GetQueryValue(callback.Location, "code")!, verifier);

            Assert.Equal("user-1", user?.UserId);
        }

        [Fact]
        [DisplayName("The code lifetime is one minute by default, as the documentation of CodeLifetime says")]
        public void CodeLifetime_Default_IsOneMinute()
        {
            Assert.Equal(TimeSpan.FromMinutes(1), new OAuth2AppRelayOptions().CodeLifetime);
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
        [DisplayName("The cache holds the user information protected, so reading the cache does not reveal it")]
        public async Task RelayedSignIn_CacheValue_DoesNotRevealUserInformation()
        {
            var cache = new DictionaryCache();
            var (manager, _) = CreateManager(SuccessfulProvider(), cache: cache);
            var (verifier, challenge) = CreateChallenge();

            var callback = await CompleteRelayedSignInAsync(manager, AppRedirectUri, challenge, "code=abc");

            string value = Encoding.UTF8.GetString(Assert.Single(cache.Values));
            Assert.DoesNotContain("user@example.com", value, StringComparison.Ordinal);
            Assert.DoesNotContain("user-1", value, StringComparison.Ordinal);
            Assert.DoesNotContain(challenge, value, StringComparison.Ordinal);
            var user = await manager.RedeemAppCodeAsync("Google", LoopbackTestHttp.GetQueryValue(callback.Location, "code")!, verifier);
            Assert.Equal("user@example.com", user?.Email);
        }

        [Fact]
        [DisplayName("RedeemAppCodeAsync does not redeem an entry that someone wrote to the cache without the keys, and removes it")]
        public async Task RedeemAppCodeAsync_ForgedEntry_ReturnsNull()
        {
            var cache = new DictionaryCache();
            var (manager, _) = CreateManager(SuccessfulProvider(), cache: cache);
            var (verifier, challenge) = CreateChallenge();
            var callback = await CompleteRelayedSignInAsync(manager, AppRedirectUri, challenge, "code=abc");
            string code = LoopbackTestHttp.GetQueryValue(callback.Location, "code")!;
            long expiresAt = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds();
            string forged = $$"""{"client":"Google","challenge":"{{challenge}}","expiresAt":{{expiresAt}},"userId":"someone-else","raw":"{}"}""";
            cache.Set(Assert.Single(cache.Keys), Encoding.UTF8.GetBytes(forged), new DistributedCacheEntryOptions());

            Assert.Null(await manager.RedeemAppCodeAsync("Google", code, verifier));
            Assert.Empty(cache.Keys);
        }

        [Theory]
        [DisplayName("RedirectToAppAsync throws when the sign-in names an application redirect URI that is no longer registered, and does not redirect to it")]
        [InlineData("code=abc")]
        [InlineData("error=access_denied")]
        public async Task RedirectToAppAsync_AppRedirectUriNoLongerRegistered_ThrowsInvalidOperationException(string parameters)
        {
            var dataProtection = new EphemeralDataProtectionProvider();
            var cache = new DictionaryCache();
            var (before, _) = CreateManager(new StubHttpMessageHandler(), dataProtection: dataProtection);
            var (after, _) = CreateManager(
                SuccessfulProvider(),
                options =>
                {
                    options.AppRedirectUris.Clear();
                    options.AppRedirectUris.Add(OtherAppRedirectUri);
                },
                cache,
                dataProtection);
            var start = StartRelayedSignIn(before, AppRedirectUri, CreateChallenge().Challenge);
            var context = CreateContext("?" + parameters + "&state=" + start.State, start.Cookie);
            var result = await after.CompleteAuthorizationAsync(context);

            await Assert.ThrowsAsync<InvalidOperationException>(() => after.RedirectToAppAsync(context, result));

            Assert.Empty(context.Response.Headers.Location.ToString());
            Assert.Empty(cache.Keys);
        }

        [Fact]
        [DisplayName("RedirectToAppAsync stops storing the code when the request is aborted, without a token from the caller")]
        public async Task RedirectToAppAsync_RequestAborted_ThrowsOperationCanceledException()
        {
            var cache = new DictionaryCache();
            var (manager, _) = CreateManager(SuccessfulProvider(), cache: cache);
            var start = StartRelayedSignIn(manager, AppRedirectUri, CreateChallenge().Challenge);
            var context = CreateContext("?code=abc&state=" + start.State, start.Cookie);
            var result = await manager.CompleteAuthorizationAsync(context);
            using var aborted = new CancellationTokenSource();
            context.RequestAborted = aborted.Token;
            await aborted.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.RedirectToAppAsync(context, result));

            Assert.Empty(cache.Keys);
            Assert.Empty(context.Response.Headers.Location.ToString());
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
            StubHttpMessageHandler handler, Action<OAuth2AppRelayOptions>? configure = null, IDistributedCache? cache = null,
            IDataProtectionProvider? dataProtection = null, TimeProvider? clock = null)
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
            services.AddSingleton(dataProtection ?? new EphemeralDataProtectionProvider());
            if (clock is not null)
                services.AddSingleton(clock);
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
        /// A distributed cache that ignores expiration, so that the relay's own expiry check is tested, that exposes its
        /// entries, and that observes the cancellation token as a cache on the network does.
        /// </summary>
        private sealed class DictionaryCache : IDistributedCache
        {
            private readonly Dictionary<string, byte[]> _entries = new(StringComparer.Ordinal);

            public IReadOnlyCollection<string> Keys => _entries.Keys;

            public IReadOnlyCollection<byte[]> Values => _entries.Values;

            public List<DistributedCacheEntryOptions> Options { get; } = [];

            public int Reads { get; private set; }

            public byte[]? Get(string key)
            {
                Reads++;
                return _entries.TryGetValue(key, out var value) ? value : null;
            }

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

            public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
            {
                _entries[key] = value;
                Options.Add(options);
            }

            public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
            {
                token.ThrowIfCancellationRequested();
                Set(key, value, options);
                return Task.CompletedTask;
            }
        }
    }
}
