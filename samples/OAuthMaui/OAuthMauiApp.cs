namespace OAuthMaui;

public sealed class OAuthMauiApp : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new NavigationPage(new MainPage())) { Title = "OAuth MAUI" };
    }
}
