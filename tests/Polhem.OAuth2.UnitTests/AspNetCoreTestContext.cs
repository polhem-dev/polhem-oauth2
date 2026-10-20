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

        /// <summary>
        /// Creates the context of an HTTPS POST request to <c>app.example.com</c> with a body, as a provider sends with
        /// <c>response_mode=form_post</c>.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <param name="contentType">The content type of the body.</param>
        /// <param name="queryString">The query string, with its leading question mark, or null for none.</param>
        /// <param name="cookies">The cookies of the request, each written as <c>name=value</c>.</param>
        /// <returns>The context.</returns>
        public static DefaultHttpContext CreatePost(
            string body, string contentType = "application/x-www-form-urlencoded", string? queryString = null, params string[] cookies)
        {
            var context = Create(queryString, cookies);
            context.Request.Method = HttpMethods.Post;
            context.Request.ContentType = contentType;
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(body);
            context.Request.Body = new MemoryStream(bytes);
            context.Request.ContentLength = bytes.Length;
            return context;
        }
    }
}
