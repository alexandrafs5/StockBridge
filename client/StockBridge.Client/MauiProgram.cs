using Microsoft.Extensions.Logging;
using StockBridge.Client.Data;
using StockBridge.Client.Services;

namespace StockBridge.Client;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

        // Un deviceId estable por instalación — se genera una vez y se guarda
        // en Preferences, así el mismo dispositivo siempre firma sus cambios igual.
        var deviceId = Preferences.Default.Get("device_id", string.Empty);
        if (string.IsNullOrEmpty(deviceId))
        {
            deviceId = $"device-{Guid.NewGuid():N}"[..16];
            Preferences.Default.Set("device_id", deviceId);
        }

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "stockbridge.db3");

        builder.Services.AddSingleton(sp => new LocalDatabase(dbPath, deviceId));
        builder.Services.AddSingleton(sp => new HttpClient());
        builder.Services.AddSingleton(sp => new ApiSyncClient(
            sp.GetRequiredService<HttpClient>(), AppConfig.ApiBaseUrl, AppConfig.ApiToken));
        builder.Services.AddSingleton(sp => new SyncService(
            sp.GetRequiredService<LocalDatabase>(), sp.GetRequiredService<ApiSyncClient>(), deviceId));

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        return builder.Build();
    }
}
