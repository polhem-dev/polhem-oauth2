using System.ComponentModel;

namespace Polhem.OAuth2.UnitTests
{
    public class CallbackQueryTests
    {
        [Fact]
        [DisplayName("FromQuery decodes percent-encoded values and plus signs")]
        public void FromQuery_EncodedValues_DecodesValues()
        {
            var callback = CallbackQuery.FromQuery("code=a%2Fb+c&state=s%3D1");

            Assert.Equal("a/b c", callback.Code);
            Assert.Equal("s=1", callback.State);
            Assert.Null(callback.Error);
        }

        [Fact]
        [DisplayName("FromQuery reads the error code and its description")]
        public void FromQuery_ErrorWithDescription_ReadsBoth()
        {
            var callback = CallbackQuery.FromQuery("error=access_denied&error_description=The+user+denied+access.&state=s");

            Assert.Equal("access_denied", callback.Error);
            Assert.Equal("The user denied access.", callback.ErrorDescription);
        }

        [Fact]
        [DisplayName("FromQuery keeps the first value of a repeated parameter")]
        public void FromQuery_RepeatedName_KeepsFirstValue()
        {
            var callback = CallbackQuery.FromQuery("state=first&state=second");

            Assert.Equal("first", callback.State);
        }

        [Fact]
        [DisplayName("FromQuery reads a parameter without a value as an empty string")]
        public void FromQuery_NameWithoutValue_ReturnsEmptyString()
        {
            var callback = CallbackQuery.FromQuery("code&error=access_denied");

            Assert.Equal(string.Empty, callback.Code);
            Assert.Equal("access_denied", callback.Error);
        }

        [Fact]
        [DisplayName("FromQuery returns null for parameters that are not present")]
        public void FromQuery_EmptyQuery_ReturnsNull()
        {
            var callback = CallbackQuery.FromQuery(string.Empty);

            Assert.Null(callback.Code);
            Assert.Null(callback.State);
            Assert.Null(callback.Error);
            Assert.Null(callback.ErrorDescription);
        }

        [Fact]
        [DisplayName("FromUri reads the query of a custom scheme URI")]
        public void FromUri_CustomSchemeQuery_ReadsParameters()
        {
            var callback = CallbackQuery.FromUri(new Uri("com.example.app:/oauth2redirect?code=a%2Fb&state=s"));

            Assert.Equal("a/b", callback.Code);
            Assert.Equal("s", callback.State);
        }

        [Fact]
        [DisplayName("FromUri reads parameters that the provider put in the fragment")]
        public void FromUri_ParametersInFragment_ReadsParameters()
        {
            var callback = CallbackQuery.FromUri(new Uri("com.example.app:/oauth2redirect#code=abc&state=s"));

            Assert.Equal("abc", callback.Code);
            Assert.Equal("s", callback.State);
        }

        [Fact]
        [DisplayName("FromUri prefers the query over the fragment when both have a parameter")]
        public void FromUri_QueryAndFragment_KeepsQueryValue()
        {
            var callback = CallbackQuery.FromUri(new Uri("com.example.app:/oauth2redirect?state=query#state=fragment&code=abc"));

            Assert.Equal("query", callback.State);
            Assert.Equal("abc", callback.Code);
        }

        [Fact]
        [DisplayName("FromUri reads the Facebook redirect, whose fragment is #_=_")]
        public void FromUri_FacebookRedirect_ReadsQuery()
        {
            var callback = CallbackQuery.FromUri(new Uri("fb1234567890://authorize/?code=abc&state=s#_=_"));

            Assert.Equal("abc", callback.Code);
            Assert.Equal("s", callback.State);
        }

        [Fact]
        [DisplayName("FromUri returns null for every parameter of a URI without a query or fragment")]
        public void FromUri_NoParameters_ReturnsNull()
        {
            var callback = CallbackQuery.FromUri(new Uri("com.example.app:/oauth2redirect"));

            Assert.Null(callback.Code);
            Assert.Null(callback.State);
            Assert.Null(callback.Error);
        }
    }
}
