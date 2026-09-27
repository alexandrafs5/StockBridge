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

    public async Task<PullResponseDto?> PullAsync(DateTime since, string deviceId)
    {
        var sinceIso = since.ToString("o"); // formato ISO 8601, lo que espera el API
        var response = await _http.GetAsync($"/sync/pull?since={Uri.EscapeDataString(sinceIso)}&deviceId={deviceId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PullResponseDto>();
    }
}
