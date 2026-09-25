using Microsoft.AspNetCore.Http;

namespace Polhem.OAuth2.UnitTests
{
    /// <summary>
    /// Creates HTTP contexts for the tests of the ASP.NET Core package.
    /// </summary>
    internal static class AspNetCoreTestContext
    {
        /// <summary>
        /// Creates the context of an HTTPS request to <c>app.example.com</c>.
        /// </summary>
        /// <param name="queryString">The query string, with its leading question mark, or null for none.</param>
        /// <param name="cookies">The cookies of the request, each written as <c>name=value</c>.</param>
        /// <returns>The context.</returns>
        public static DefaultHttpContext Create(string? queryString = null, params string[] cookies)
        {
            var context = new DefaultHttpContext();
            context.Request.Scheme = "https";
            context.Request.Host = new HostString("app.example.com");
            if (queryString is not null)
                context.Request.QueryString = new QueryString(queryString);
            if (cookies.Length > 0)
                context.Request.Headers.Cookie = string.Join("; ", cookies);
            return context;
        }
    }
}
