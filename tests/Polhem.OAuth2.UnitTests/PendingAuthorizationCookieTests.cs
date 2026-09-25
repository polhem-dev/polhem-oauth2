using System.ComponentModel;
using System.Text;

namespace Polhem.OAuth2.UnitTests
{
    public class PendingAuthorizationCookieTests
    {
        private static readonly DateTimeOffset s_issuedAt = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

        [Theory]
        [DisplayName("Deserialize reads back what Serialize wrote, with or without a code verifier")]
        [InlineData("verifier")]
        [InlineData(null)]
        public void Deserialize_SerializedPayload_ReturnsSameValues(string? codeVerifier)
        {
            var pending = new PendingAuthorization("state", codeVerifier, "https://app.example.com/callback");
            byte[] data = PendingAuthorizationCookie.Serialize("Google", pending, s_issuedAt);

            var signIn = PendingAuthorizationCookie.Deserialize(data, s_issuedAt.AddMinutes(5));

            Assert.Equal("Google", signIn.ClientName);
            Assert.Equal("state", signIn.Pending.State);
            Assert.Equal(codeVerifier, signIn.Pending.CodeVerifier);
            Assert.Equal("https://app.example.com/callback", signIn.Pending.RedirectUri);
        }

        [Theory]
        [DisplayName("Deserialize accepts a sign-in up to ten minutes old, or started up to a minute in the future")]
        [InlineData(600)]
        [InlineData(0)]
        [InlineData(-60)]
        public void Deserialize_WithinLifetime_ReturnsValues(int ageSeconds)
        {
            byte[] data = PendingAuthorizationCookie.Serialize("Google", new PendingAuthorization("state", null, "https://app.example.com/callback"), s_issuedAt);

            var signIn = PendingAuthorizationCookie.Deserialize(data, s_issuedAt.AddSeconds(ageSeconds));

            Assert.Equal("Google", signIn.ClientName);
        }

        [Theory]
        [DisplayName("Deserialize rejects a sign-in older than ten minutes, or started more than a minute in the future")]
        [InlineData(601)]
        [InlineData(-61)]
        public void Deserialize_OutsideLifetime_ThrowsOAuth2Exception(int ageSeconds)
        {
            byte[] data = PendingAuthorizationCookie.Serialize("Google", new PendingAuthorization("state", null, "https://app.example.com/callback"), s_issuedAt);

            Assert.Throws<OAuth2Exception>(() => PendingAuthorizationCookie.Deserialize(data, s_issuedAt.AddSeconds(ageSeconds)));
        }

        [Theory]
        [DisplayName("Deserialize rejects data that is not a pending sign-in")]
        [InlineData("not json")]
        [InlineData("[]")]
        [InlineData("""{"state":"s","redirectUri":"https://app.example.com/callback","issuedAt":1789387200}""")]
        [InlineData("""{"client":"Google","state":"","redirectUri":"https://app.example.com/callback","issuedAt":1789387200}""")]
        [InlineData("""{"client":"Google","state":"s","redirectUri":"https://app.example.com/callback","issuedAt":"1789387200"}""")]
        [InlineData("""{"client":"Google","state":"s","redirectUri":"https://app.example.com/callback","issuedAt":-1}""")]
        public void Deserialize_InvalidData_ThrowsOAuth2Exception(string json)
        {
            Assert.Throws<OAuth2Exception>(() => PendingAuthorizationCookie.Deserialize(Encoding.UTF8.GetBytes(json), s_issuedAt));
        }

        [Fact]
        [DisplayName("GetName prefixes a base64url state with __Host-")]
        public void GetName_Base64UrlState_ReturnsPrefixedName()
        {
            Assert.Equal("__Host-oauth2.abc-_XYZ09", PendingAuthorizationCookie.GetName("abc-_XYZ09"));
        }

        [Theory]
        [DisplayName("GetName returns null for a state that cannot be part of a cookie name")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("a b")]
        [InlineData("a;b")]
        [InlineData("a=b")]
        public void GetName_InvalidState_ReturnsNull(string? state)
        {
            Assert.Null(PendingAuthorizationCookie.GetName(state));
        }

        [Fact]
        [DisplayName("GetName returns null for a state longer than 256 characters")]
        public void GetName_LongState_ReturnsNull()
        {
            Assert.Null(PendingAuthorizationCookie.GetName(new string('a', 257)));
        }

        [Fact]
        [DisplayName("Deserialize reads back the application redirect URI and code challenge of a relayed sign-in")]
        public void Deserialize_RelayedSignIn_ReturnsAppValues()
        {
            var pending = new PendingAuthorization("state", "verifier", "https://app.example.com/callback");
            byte[] data = PendingAuthorizationCookie.Serialize("Google", pending, s_issuedAt, "com.example.app:/signin", "challenge");

            var signIn = PendingAuthorizationCookie.Deserialize(data, s_issuedAt);

            Assert.Equal("Google", signIn.ClientName);
            Assert.Equal("com.example.app:/signin", signIn.AppRedirectUri);
            Assert.Equal("challenge", signIn.AppCodeChallenge);
        }

        [Fact]
        [DisplayName("Deserialize reports no application for a web sign-in")]
        public void Deserialize_WebSignIn_ReturnsNoAppValues()
        {
            byte[] data = PendingAuthorizationCookie.Serialize("Google", new PendingAuthorization("state", null, "https://app.example.com/callback"), s_issuedAt);

            var signIn = PendingAuthorizationCookie.Deserialize(data, s_issuedAt);

            Assert.Null(signIn.AppRedirectUri);
            Assert.Null(signIn.AppCodeChallenge);
        }

        [Theory]
        [DisplayName("Deserialize rejects an application redirect URI without a code challenge, and the reverse")]
        [InlineData("""{"client":"Google","state":"s","redirectUri":"https://app.example.com/callback","issuedAt":1789387200,"appRedirectUri":"com.example.app:/signin"}""")]
        [InlineData("""{"client":"Google","state":"s","redirectUri":"https://app.example.com/callback","issuedAt":1789387200,"appChallenge":"challenge"}""")]
        public void Deserialize_PartialAppValues_ThrowsOAuth2Exception(string json)
        {
            Assert.Throws<OAuth2Exception>(() => PendingAuthorizationCookie.Deserialize(Encoding.UTF8.GetBytes(json), s_issuedAt));
        }
    }
}
