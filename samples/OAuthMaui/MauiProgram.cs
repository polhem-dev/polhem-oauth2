namespace OAuthMaui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<OAuthMauiApp>();
        return builder.Build();
    }
}
