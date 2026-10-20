using System.Collections.Specialized;
using System.Web;

namespace Polhem.OAuth2.UnitTests
{
    /// <summary>
    /// An HTTP context for testing System.Web code outside ASP.NET. It provides the method, query string, form and cookies of
    /// the request, and records the cookies and redirect of the response.
    /// </summary>
    internal sealed class FakeHttpContext : HttpContextBase
    {
        private readonly FakeRequest _request;
        private readonly FakeResponse _response = new FakeResponse();

        public FakeHttpContext(string queryString = "", params HttpCookie[] cookies)
        {
            _request = new FakeRequest("GET", HttpUtility.ParseQueryString(queryString.TrimStart('?')), [], cookies);
        }

        private FakeHttpContext(string method, string queryString, string form, HttpCookie[] cookies)
        {
            _request = new FakeRequest(method, HttpUtility.ParseQueryString(queryString.TrimStart('?')), HttpUtility.ParseQueryString(form), cookies);
        }

        /// <summary>
        /// Creates the context of a POST request with a form-encoded body.
        /// </summary>
        /// <param name="form">The form-encoded body.</param>
        /// <param name="queryString">The query string of the request.</param>
        /// <param name="cookies">The cookies of the request.</param>
        /// <returns>The context.</returns>
        public static FakeHttpContext Post(string form, string queryString = "", params HttpCookie[] cookies)
        {
            return new FakeHttpContext("POST", queryString, form, cookies);
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
            private readonly FakeUnvalidatedValues _unvalidated;
            private readonly HttpCookieCollection _cookies = [];

            public FakeRequest(string method, NameValueCollection queryString, NameValueCollection form, HttpCookie[] cookies)
            {
                HttpMethod = method;
                _queryString = queryString;
                _unvalidated = new FakeUnvalidatedValues(form);
                foreach (var cookie in cookies)
                    _cookies.Add(cookie);
            }

            public override string HttpMethod { get; }

            public override NameValueCollection QueryString => _queryString;

            // The validated form throws on purpose: the manager must read the form without request validation.
            public override NameValueCollection Form => throw new InvalidOperationException("Read the form through Unvalidated.");

            public override UnvalidatedRequestValuesBase Unvalidated => _unvalidated;

            public override HttpCookieCollection Cookies => _cookies;
        }

        private sealed class FakeUnvalidatedValues : UnvalidatedRequestValuesBase
        {
            private readonly NameValueCollection _form;

            public FakeUnvalidatedValues(NameValueCollection form)
            {
                _form = form;
            }

            public override NameValueCollection Form => _form;
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
