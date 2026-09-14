using System.ComponentModel;

namespace Polhem.OAuth2.UnitTests
{
    public class PkceTests
    {
        [Fact]
        [DisplayName("GenerateCodeChallenge matches the S256 example in RFC 7636, appendix B")]
        public void GenerateCodeChallenge_Rfc7636Example_ReturnsExpectedChallenge()
        {
            string challenge = Pkce.GenerateCodeChallenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk");

            Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", challenge);
        }

        [Fact]
        [DisplayName("GenerateCodeVerifier returns 43 base64url characters")]
        public void GenerateCodeVerifier_ReturnsFortyThreeBase64UrlCharacters()
        {
            string verifier = Pkce.GenerateCodeVerifier();

            Assert.Equal(43, verifier.Length);
            Assert.All(verifier, c => Assert.True(char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_', $"Unexpected character '{c}'."));
        }

        [Fact]
        [DisplayName("GenerateCodeVerifier returns a different value each time")]
        public void GenerateCodeVerifier_TwoCalls_ReturnDifferentValues()
        {
            Assert.NotEqual(Pkce.GenerateCodeVerifier(), Pkce.GenerateCodeVerifier());
        }
    }
}
