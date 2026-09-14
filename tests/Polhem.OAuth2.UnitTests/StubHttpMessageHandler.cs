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
        private readonly Queue<Func<CancellationToken, Task<HttpResponseMessage>>> _responses = new();
        private readonly TaskCompletionSource<bool> _hanging = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<RecordedRequest> Requests { get; } = [];

        /// <summary>
        /// Gets a task that completes when a request answered by <see cref="Hang"/> arrives.
        /// </summary>
        public Task Hanging => _hanging.Task;

        public StubHttpMessageHandler Respond(HttpStatusCode statusCode, string body)
        {
            _responses.Enqueue(_ => Task.FromResult(new HttpResponseMessage(statusCode) { Content = new StringContent(body, Encoding.UTF8, "application/json") }));
            return this;
        }

        public StubHttpMessageHandler Fail(Exception exception)
        {
            _responses.Enqueue(_ => Task.FromException<HttpResponseMessage>(exception));
            return this;
        }

        /// <summary>
        /// Queues a response that never arrives, so that the request ends only when it is canceled.
        /// </summary>
        public StubHttpMessageHandler Hang()
        {
            _responses.Enqueue(async cancellationToken =>
            {
                _hanging.TrySetResult(true);
                await Task.Delay(Timeout.Infinite, cancellationToken);
                throw new InvalidOperationException("The request was expected to be canceled.");
            });
            return this;
        }

        public HttpClient CreateClient()
        {
            return new HttpClient(this, disposeHandler: false);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
            Requests.Add(new RecordedRequest(request.Method, request.RequestUri!, request.Headers.Authorization, body));

            if (_responses.Count == 0)
                throw new InvalidOperationException("No response is queued for this request.");
            return await _responses.Dequeue()(cancellationToken);
        }

        public sealed class RecordedRequest
        {
            public RecordedRequest(HttpMethod method, Uri uri, AuthenticationHeaderValue? authorization, string? body)
            {
                Method = method;
                Uri = uri;
                Authorization = authorization;
                Body = body;
            }

            public HttpMethod Method { get; }

            public Uri Uri { get; }

            public AuthenticationHeaderValue? Authorization { get; }

            public string? Body { get; }

            public string? FormValue(string name)
            {
                return Body is null ? null : LoopbackTestHttp.GetParameter(Body, name);
            }
        }
    }
}
