namespace StockBridge.Client;

public static class AppConfig
{
    // En desarrollo local con emulador de Android, "10.0.2.2" apunta al
    // localhost de tu máquina host. En Windows (WinUI) usa "localhost" directo.
    // Cuando el backend esté desplegado en Railway, cambia esto por la URL pública.s
    public const string ApiBaseUrl = "https://stockbridge-production-353d.up.railway.app";

    // Debe coincidir exactamente con API_TOKEN en el .env del backend.
    public const string ApiToken = "stockbridge-dev-2026-x7k9";
}
