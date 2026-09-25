using System.ComponentModel;

namespace Polhem.OAuth2.UnitTests
{
    public class OAuth2ExceptionTests
    {
        // The calls compile without a null-forgiving operator only because the parameters are annotated as nullable.
        [Fact]
        [DisplayName("The message and inner exception constructors accept null, as those of Exception do")]
        public void Constructors_NullArguments_UseDefaults()
        {
            var withMessage = new OAuth2Exception(null);
            var withInner = new OAuth2Exception(null, null);

            Assert.False(string.IsNullOrEmpty(withMessage.Message));
            Assert.Null(withInner.InnerException);
            Assert.Null(withInner.Error);
        }
    }
}
