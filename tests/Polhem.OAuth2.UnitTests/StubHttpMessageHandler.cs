using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Polhem.OAuth2.UnitTests
{
    /// <summary>
    /// Answers HTTP requests with queued responses and records every request, so that provider requests are tested without a network.
    /// </summary>
    internal sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _responses = new();

        public List<RecordedRequest> Requests { get; } = [];

        public StubHttpMessageHandler Respond(HttpStatusCode statusCode, string body)
        {
            _responses.Enqueue(() => new HttpResponseMessage(statusCode) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
            return this;
        }

        public StubHttpMessageHandler Fail(Exception exception)
        {
            _responses.Enqueue(() => throw exception);
            return this;
        }

        public HttpClient CreateClient()
        {
            return new HttpClient(this, disposeHandler: false);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RecordedRequest(request.Method, request.RequestUri!, request.Headers.Authorization, body));

            if (!_responses.TryDequeue(out var respond))
                throw new InvalidOperationException("No response is queued for this request.");
            return respond();
        }

        public sealed record RecordedRequest(HttpMethod Method, Uri Uri, AuthenticationHeaderValue? Authorization, string? Body)
        {
            public string? FormValue(string name)
            {
                return Body is null ? null : LoopbackTestHttp.GetParameter(Body, name);
            }
        }
    }
}
