namespace Polhem.OAuth2.UnitTests
{
    /// <summary>
    /// Plays the browser after the back end has redirected: reads the application redirect URI from the sign-in URL and
    /// sends the requests of the templates to it. <c>{redirect}</c> is the path and query of that URI, with its state, and
    /// <c>{path}</c> is its path alone.
    /// </summary>
    internal sealed class FakeRelayBrowser
    {
        public const string BackEnd = "https://api.example.com/";

        private readonly string[] _requests;

        public FakeRelayBrowser(params string[] requests)
        {
            _requests = requests;
        }

        public string? RedirectUri { get; private set; }

        public string? CodeChallenge { get; private set; }

        public List<string> Responses { get; } = [];

        public Task Completed { get; private set; } = Task.CompletedTask;

        public static Uri CreateSignInUrl(Uri redirectUri, string codeChallenge)
        {
            return new Uri($"{BackEnd}auth/app/Google?redirect_uri={Uri.EscapeDataString(redirectUri.AbsoluteUri)}&code_challenge={codeChallenge}");
        }

        public Task Open(Uri url)
        {
            RedirectUri = LoopbackTestHttp.GetQueryValue(url.AbsoluteUri, "redirect_uri")!;
            CodeChallenge = LoopbackTestHttp.GetQueryValue(url.AbsoluteUri, "code_challenge");
            var redirectUri = new Uri(RedirectUri);
            Completed = Task.Run(async () =>
            {
                foreach (string template in _requests)
                {
                    string pathAndQuery = template.Replace("{redirect}", redirectUri.PathAndQuery).Replace("{path}", redirectUri.AbsolutePath);
                    Responses.Add(await LoopbackTestHttp.GetAsync(redirectUri, pathAndQuery));
                }
            });
            return Task.CompletedTask;
        }
    }
}
