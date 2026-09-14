using System.Collections.Specialized;
using System.Web;

namespace Polhem.OAuth2.UnitTests
{
    /// <summary>
    /// An HTTP context for testing System.Web code outside ASP.NET. It provides the query string and cookies of the request,
    /// and records the cookies and redirect of the response.
    /// </summary>
    internal sealed class FakeHttpContext : HttpContextBase
    {
        private readonly FakeRequest _request;
        private readonly FakeResponse _response = new FakeResponse();

        public FakeHttpContext(string queryString = "", params HttpCookie[] cookies)
        {
            _request = new FakeRequest(HttpUtility.ParseQueryString(queryString.TrimStart('?')), cookies);
        }

        public override HttpRequestBase Request => _request;

        public override HttpResponseBase Response => _response;

        public override HttpApplication? ApplicationInstance
        {
            get => null;
            set { }
        }

        private sealed class FakeRequest : HttpRequestBase
        {
            private readonly NameValueCollection _queryString;
            private readonly HttpCookieCollection _cookies = [];

            public FakeRequest(NameValueCollection queryString, HttpCookie[] cookies)
            {
                _queryString = queryString;
                foreach (var cookie in cookies)
                    _cookies.Add(cookie);
            }

            public override NameValueCollection QueryString => _queryString;

            public override HttpCookieCollection Cookies => _cookies;
        }

        private sealed class FakeResponse : HttpResponseBase
        {
            private readonly HttpCookieCollection _cookies = [];

            public override HttpCookieCollection Cookies => _cookies;

            public override string? RedirectLocation { get; set; }

            public override int StatusCode { get; set; } = 200;

            public override void Redirect(string url, bool endResponse)
            {
                RedirectLocation = url;
                StatusCode = 302;
            }
        }
    }
}
