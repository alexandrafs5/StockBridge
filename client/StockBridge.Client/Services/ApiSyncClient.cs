using System.Net.Http.Json;
using System.Text.Json;
 
namespace StockBridge.Client.Services;
 
public class ApiSyncClient
{
    private readonly HttpClient _http;
 
    public ApiSyncClient(HttpClient http, string baseUrl, string apiToken)
    {
        _http = http;
        _http.BaseAddress = new Uri(baseUrl);
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiToken);
    }
 
    public async Task<PushResponseDto?> PushAsync(List<OperacionSyncDto> operaciones)
    {
        var body = new PushRequestDto { Operaciones = operaciones };
        var response = await _http.PostAsJsonAsync("/sync/push", body);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PushResponseDto>();
    }
 
    /// <summary>
    /// "sucursalId" null = dispositivo todavía sin sucursal asignada (equipo
    /// nuevo en configuración) — el servidor responde solo con la lista de
    /// sucursales y los dueños, sin inventario/ventas de nadie todavía.
    /// </summary>
    public async Task<PullResponseDto?> PullAsync(DateTime since, string deviceId, string? sucursalId)
    {
        var sinceIso = since.ToString("o"); // formato ISO 8601, lo que espera el API
        var url = $"/sync/pull?since={Uri.EscapeDataString(sinceIso)}&deviceId={deviceId}";
        if (!string.IsNullOrEmpty(sucursalId))
            url += $"&sucursalId={Uri.EscapeDataString(sucursalId)}";
 
        var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PullResponseDto>();
    }
}
 
