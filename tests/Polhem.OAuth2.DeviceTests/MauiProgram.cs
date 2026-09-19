using DeviceRunners.VisualRunners;

namespace Polhem.OAuth2.DeviceTests;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseVisualTestRunner(configuration => configuration
            .AddCliConfiguration()
            .AddConsoleResultChannel()
            .AddTestAssembly(typeof(MauiProgram).Assembly)
            .AddXunit());
        return builder.Build();
    }
}
