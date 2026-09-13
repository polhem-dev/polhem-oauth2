using System.ComponentModel;
using System.Text.Json;

namespace Polhem.OAuth2.UnitTests
{
    public class OAuth2JsonTests
    {
        [Theory]
        [DisplayName("GetString returns a string's value, and the JSON text of any other value")]
        [InlineData("""{"v":"text"}""", "text")]
        [InlineData("""{"v":12345}""", "12345")]
        [InlineData("""{"v":true}""", "true")]
        [InlineData("""{"v":{"a":1}}""", """{"a":1}""")]
        [InlineData("""{"v":[1,2]}""", "[1,2]")]
        public void GetString_PresentValue_ReturnsText(string json, string expected)
        {
            using var document = OAuth2Json.ParseObject(json);

            Assert.Equal(expected, OAuth2Json.GetString(document.RootElement, "v"));
        }

        [Theory]
        [DisplayName("GetString returns null for a missing field or a JSON null")]
        [InlineData("""{}""")]
        [InlineData("""{"v":null}""")]
        public void GetString_MissingOrNullValue_ReturnsNull(string json)
        {
            using var document = OAuth2Json.ParseObject(json);

            Assert.Null(OAuth2Json.GetString(document.RootElement, "v"));
        }

        [Fact]
        [DisplayName("GetString matches field names case-sensitively")]
        public void GetString_DifferentCase_ReturnsNull()
        {
            using var document = OAuth2Json.ParseObject("""{"V":"text"}""");

            Assert.Null(OAuth2Json.GetString(document.RootElement, "v"));
        }

        [Theory]
        [DisplayName("ParseObject rejects JSON whose root value is not an object")]
        [InlineData("[]")]
        [InlineData("\"text\"")]
        [InlineData("42")]
        public void ParseObject_RootNotObject_ThrowsJsonException(string json)
        {
            Assert.Throws<JsonException>(() => OAuth2Json.ParseObject(json));
        }

        [Fact]
        [DisplayName("ParseObject rejects text that is not valid JSON")]
        public void ParseObject_InvalidJson_ThrowsJsonException()
        {
            Assert.ThrowsAny<JsonException>(() => OAuth2Json.ParseObject("{"));
        }
    }
}
