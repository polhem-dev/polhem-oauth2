using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Polhem.OAuth2.AspNetCore;

namespace Polhem.OAuth2.UnitTests
{
    public class AspNetCoreAppRelayLoopbackTests
    {
        private const string State = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";

        [Theory]
        [DisplayName("AddOAuth2AppRelay accepts a loopback redirect URI of 127.0.0.1 or [::1], with or without a port")]
        [InlineData("http://127.0.0.1/callback")]
        [InlineData("http://[::1]/callback")]
        [InlineData("http://127.0.0.1:53682/callback")]
        [InlineData("http://127.0.0.1/oauth2/callback")]
        public void AddOAuth2AppRelay_LoopbackUri_IsAccepted(string appRedirectUri)
        {
            var settings = CreateSettings(appRedirectUri);

            Assert.True(settings.IsRegistered(appRedirectUri));
        }

        [Theory]
        [DisplayName("AddOAuth2AppRelay rejects a loopback redirect URI with localhost, another address, no path, a query, user information, port 0 or a form Uri does not write")]
        [InlineData("http://localhost/callback")]
        [InlineData("http://127.0.0.2/callback")]
        [InlineData("http://192.168.0.1/callback")]
        [InlineData("http://127.0.0.1")]
        [InlineData("http://127.0.0.1/")]
        [InlineData("http://127.0.0.1/callback?source=desktop")]
        [InlineData("http://127.0.0.1/callback?state=" + State)]
        [InlineData("http://user@127.0.0.1/callback")]
        [InlineData("http://127.0.0.1:0/callback")]
        [InlineData("http://127.0.0.1/a/../callback")]
        [InlineData("http://[0:0:0:0:0:0:0:1]/callback")]
        [InlineData("http://2130706433/callback")]
        public void AddOAuth2AppRelay_InvalidLoopbackUri_ThrowsArgumentException(string appRedirectUri)
        {
            var exception = Assert.Throws<ArgumentException>(() => CreateSettings(appRedirectUri));

            Assert.Equal("configure", exception.ParamName);
        }

        [Theory]
        [DisplayName("A loopback redirect URI registered without a port matches the same address and path on any port, with or without the state of the application")]
        [InlineData("http://127.0.0.1:53682/callback", true)]
        [InlineData("http://127.0.0.1:1/callback", true)]
        [InlineData("http://127.0.0.1:65535/callback?state=" + State, true)]
        [InlineData("http://127.0.0.1/callback", true)]
        [InlineData("http://[::1]:53682/callback", false)]
        [InlineData("http://127.0.0.1:53682/other", false)]
        [InlineData("http://127.0.0.1:53682/Callback", false)]
        [InlineData("http://127.0.0.1:53682/callback/", false)]
        [InlineData("http://localhost:53682/callback", false)]
        [InlineData("http://127.0.0.2:53682/callback", false)]
        [InlineData("http://127.0.0.1:0/callback", false)]
        [InlineData("https://127.0.0.1:53682/callback", false)]
        [InlineData("http://127.0.0.1:53682/callback#fragment", false)]
        [InlineData("http://127.0.0.1:53682/callback?state=short", false)]
        [InlineData("http://127.0.0.1:53682/callback?state=" + State + "+", false)]
        [InlineData("http://127.0.0.1:53682/callback?state=" + State + "&code=forged", false)]
        [InlineData("http://127.0.0.1:53682/callback?code=forged", false)]
        [InlineData("http://127.0.0.1:053682/callback", false)]
        [InlineData("http://user@127.0.0.1:53682/callback", false)]
        public void IsRegistered_LoopbackWithoutPort_MatchesAnyPort(string requested, bool expected)
        {
            var settings = CreateSettings("http://127.0.0.1/callback");

            Assert.Equal(expected, settings.IsRegistered(requested));
        }

        [Theory]
        [DisplayName("A loopback redirect URI registered with a port matches only that port, with or without the state of the application")]
        [InlineData("http://127.0.0.1:53682/callback", true)]
        [InlineData("http://127.0.0.1:53682/callback?state=" + State, true)]
        [InlineData("http://127.0.0.1:53683/callback", false)]
        [InlineData("http://127.0.0.1/callback", false)]
        public void IsRegistered_LoopbackWithPort_MatchesOnlyThatPort(string requested, bool expected)
        {
            var settings = CreateSettings("http://127.0.0.1:53682/callback");

            Assert.Equal(expected, settings.IsRegistered(requested));
        }

        [Fact]
        [DisplayName("A state query does not widen a custom scheme registration, which still matches exactly")]
        public void IsRegistered_CustomSchemeWithState_IsRejected()
        {
            var settings = CreateSettings("com.example.app:/signin");

            Assert.False(settings.IsRegistered("com.example.app:/signin?state=" + State));
        }

        private static AppRelaySettings CreateSettings(string appRedirectUri)
        {
            var services = new ServiceCollection();
            services.AddOAuth2AppRelay(options => options.AppRedirectUris.Add(appRedirectUri));
            return services.BuildServiceProvider().GetRequiredService<AppRelaySettings>();
        }
    }
}
