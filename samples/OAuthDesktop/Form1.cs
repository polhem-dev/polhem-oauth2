using System.Net.Sockets;
using System.Text.Json;
using Polhem.OAuth2;

namespace OAuthDesktop
{
    public partial class Form1 : Form
    {
        private static readonly JsonSerializerOptions s_readOptions = new() { PropertyNameCaseInsensitive = true };
        private static readonly JsonSerializerOptions s_writeOptions = new() { WriteIndented = true };

        private readonly Dictionary<string, LoopbackOAuth2Client> _clients = new(StringComparer.Ordinal);

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            var config = LoadOAuthConfig(Path.Combine(AppContext.BaseDirectory, "OAuthConfig.json"));
            RegisterIfExists("Google", config.GoogleOAuth);
            RegisterIfExists("Facebook", config.FacebookOAuth);
            RegisterIfExists("Line", config.LineOAuth);
            RegisterIfExists("Azure", config.AzureOAuth);
            RegisterIfExists("Auth0", config.Auth0OAuth);
            RegisterIfExists("Okta", config.OktaOAuth);
        }

        private static OAuthConfig LoadOAuthConfig(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Configuration file not found.", filePath);

            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<OAuthConfig>(json, s_readOptions) ?? new OAuthConfig();
        }

        private void RegisterIfExists(string name, OAuth2Options? options)
        {
            if (options == null)
                return;

            try
            {
                _clients[name] = new LoopbackOAuth2Client(options);
            }
            catch (ArgumentException ex)
            {
                edtUserInfo.AppendText($"{name}: {ex.Message}\r\n");
            }
        }

        private async void Login(string clientName)
        {
            if (!_clients.TryGetValue(clientName, out var client))
            {
                edtUserInfo.Text = $"{clientName} is not configured in OAuthConfig.json.";
                return;
            }

            edtUserInfo.Text = "Finish signing in in the browser.";
            try
            {
                var result = await client.SignInAsync();
                // The browser has the focus when the sign-in ends, so the form is brought back to the front.
                Activate();
                ShowResult(result);
            }
            catch (InvalidOperationException ex)
            {
                edtUserInfo.Text = ex.Message;
            }
            catch (SocketException ex)
            {
                edtUserInfo.Text = $"Cannot listen on the redirect URI: {ex.Message}";
            }
        }

        private void ShowResult(AuthorizationResult result)
        {
            if (result.Exception != null)
            {
                edtUserInfo.Text = result.Exception.Message;
                return;
            }

            var userInfo = result.UserInfo;
            if (userInfo == null)
            {
                edtUserInfo.Text = string.Empty;
                return;
            }

            using var document = JsonDocument.Parse(userInfo.RawJson);
            string json = JsonSerializer.Serialize(document.RootElement, s_writeOptions);
            edtUserInfo.Text = $"ProviderName : {result.ProviderName}\r\n" +
                               $"UserID : {userInfo.UserId}\r\n" +
                               $"UserName : {userInfo.UserName}\r\n" +
                               $"Email : {userInfo.Email}\r\n" +
                               $"RawJson : \r\n{json}";
        }

        private void btnGoogle_Click(object sender, EventArgs e)
        {
            Login("Google");
        }

        private void btnFacebook_Click(object sender, EventArgs e)
        {
            Login("Facebook");
        }

        private void btnLine_Click(object sender, EventArgs e)
        {
            Login("Line");
        }

        private void btnAzure_Click(object sender, EventArgs e)
        {
            Login("Azure");
        }

        private void btnAuth0_Click(object sender, EventArgs e)
        {
            Login("Auth0");
        }

        private void btnOkta_Click(object sender, EventArgs e)
        {
            Login("Okta");
        }
    }
}
