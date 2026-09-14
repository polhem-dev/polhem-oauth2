using System.ComponentModel;

namespace Polhem.OAuth2.UnitTests
{
    public class LoopbackCallbackTests
    {
        [Fact]
        [DisplayName("FromQuery decodes percent-encoded values and plus signs")]
        public void FromQuery_EncodedValues_DecodesValues()
        {
            var callback = LoopbackCallback.FromQuery("code=a%2Fb+c&state=s%3D1");

            Assert.Equal("a/b c", callback.Code);
            Assert.Equal("s=1", callback.State);
            Assert.Null(callback.Error);
        }

        [Fact]
        [DisplayName("FromQuery keeps the first value of a repeated parameter")]
        public void FromQuery_RepeatedName_KeepsFirstValue()
        {
            var callback = LoopbackCallback.FromQuery("state=first&state=second");

            Assert.Equal("first", callback.State);
        }

        [Fact]
        [DisplayName("FromQuery reads a parameter without a value as an empty string")]
        public void FromQuery_NameWithoutValue_ReturnsEmptyString()
        {
            var callback = LoopbackCallback.FromQuery("code&error=access_denied");

            Assert.Equal(string.Empty, callback.Code);
            Assert.Equal("access_denied", callback.Error);
        }

        [Fact]
        [DisplayName("FromQuery returns null for parameters that are not present")]
        public void FromQuery_EmptyQuery_ReturnsNull()
        {
            var callback = LoopbackCallback.FromQuery(string.Empty);

            Assert.Null(callback.Code);
            Assert.Null(callback.State);
            Assert.Null(callback.Error);
        }
    }
}
