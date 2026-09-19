using OAuthSamples;
using Polhem.OAuth2;

namespace OAuthMaui;

/// <summary>
/// Lists a sign-in button for every provider in the packaged settings: a direct sign-in with <see cref="AppOAuth2Client"/>
/// on Android, iOS and Mac Catalyst, a sign-in through the back-end relay of the ASP.NET Core sample, and a loopback
/// sign-in with <see cref="LoopbackOAuth2Client"/> on Windows (ADR-006).
/// </summary>
public sealed class MainPage : ContentPage
{
    private const string SettingsFileName = "OAuthApp.json";

    private readonly VerticalStackLayout _buttons = new() { Spacing = 8 };
    private readonly Label _result = new() { FontSize = 14 };
    private bool _signingIn;

    public MainPage()
    {
        Title = "OAuth MAUI";
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 16,
                Children = { _buttons, _result }
            }
        };
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        OAuthConfig config;
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync(SettingsFileName);
            using var reader = new StreamReader(stream);
            config = OAuthConfig.Parse(await reader.ReadToEndAsync(), SettingsFileName);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException)
        {
            _result.Text = $"The OAuth2 settings could not be read: {ex.Message}";
            return;
        }

        if (DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            foreach (var client in config.GetClients(OAuthClientType.Desktop))
                AddButton($"Sign in with {client.ProviderName} (loopback)", () => SignInWithLoopbackAsync(client));
        }
        else
        {
            var appType = DeviceInfo.Platform == DevicePlatform.Android ? OAuthClientType.Android : OAuthClientType.Ios;
            foreach (var client in config.GetClients(appType))
                AddButton($"Sign in with {client.ProviderName}", () => SignInDirectlyAsync(client));

            if (config.AppRelay is { } relay)
            {
                foreach (var client in config.GetClients(OAuthClientType.Web))
                    AddButton($"Sign in with {client.ProviderName} through the back end", () => SignInThroughBackEndAsync(relay, client.ProviderName));
            }
        }

        if (_buttons.Children.Count == 0)
            _result.Text = "No provider is filled in. Fill in OAuthConfig.json in the repository root and build again.";
    }

    private void AddButton(string text, Func<Task> signIn)
    {
        var button = new Button { Text = text };
        button.Clicked += async (_, _) =>
        {
            if (_signingIn)
                return;
            _signingIn = true;
            _result.Text = "Signing in...";
            try
            {
                await signIn();
            }
            finally
            {
                _signingIn = false;
            }
        };
        _buttons.Children.Add(button);
    }

    private async Task SignInDirectlyAsync(OAuthClientEntry client)
    {
        var appClient = new AppOAuth2Client(client.Options, async (url, redirectUri, cancellationToken) =>
            (await WebAuthenticator.Default.AuthenticateAsync(url, redirectUri)).CallbackUri);

        ShowResult(await appClient.SignInAsync());
    }

    private async Task SignInWithLoopbackAsync(OAuthClientEntry client)
    {
        var loopbackClient = new LoopbackOAuth2Client(client.Options)
        {
            OpenBrowser = url => Launcher.Default.OpenAsync(url)
        };

        ShowResult(await loopbackClient.SignInAsync());
    }

    private async Task SignInThroughBackEndAsync(OAuthAppRelay relay, string providerName)
    {
        try
        {
            var user = await BackEndSignIn.SignInAsync(relay, providerName);
            _result.Text = user is null
                ? "The back end did not return a user."
                : $"Signed in with {providerName} through the back end\nUser ID: {user.UserId}\nName: {user.UserName}\nEmail: {user.Email}";
        }
        catch (TaskCanceledException)
        {
            _result.Text = "The sign-in was canceled.";
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            _result.Text = $"The sign-in failed: {ex.Message}";
        }
    }

    private void ShowResult(AuthorizationResult result)
    {
        _result.Text = result.IsSuccess
            ? $"Signed in with {result.ProviderName}\nUser ID: {result.UserInfo!.UserId}\nName: {result.UserInfo.UserName}\nEmail: {result.UserInfo.Email}"
            : result.Exception is OperationCanceledException
                ? "The sign-in was canceled."
                : $"The sign-in failed: {result.Exception!.Message}";
    }
}
