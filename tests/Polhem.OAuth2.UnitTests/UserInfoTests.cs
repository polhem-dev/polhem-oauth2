using System.ComponentModel;

namespace Polhem.OAuth2.UnitTests
{
    public class UserInfoTests
    {
        [Fact]
        [DisplayName("The constructor keeps every value it is given")]
        public void Constructor_AllValues_KeepsValues()
        {
            var user = new UserInfo("1", "Ada", "ada@example.com", """{"sub":"1"}""");

            Assert.Equal("1", user.UserId);
            Assert.Equal("Ada", user.UserName);
            Assert.Equal("ada@example.com", user.Email);
            Assert.Equal("""{"sub":"1"}""", user.RawJson);
        }

        [Fact]
        [DisplayName("The constructor rejects null raw JSON")]
        public void Constructor_NullRawJson_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new UserInfo("1", null, null, null!));
        }
    }
}
