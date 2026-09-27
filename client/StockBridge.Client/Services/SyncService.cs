using System.Text.Json;
using Microsoft.Maui.Networking;
using StockBridge.Client.Data;
using StockBridge.Client.Data.Models;

namespace StockBridge.Client.Services;

/// <summary>
/// Escucha cambios de conectividad y dispara la sincronización automática.
/// Este es el componente que le da al sistema su "resiliencia": si la sync
/// falla a medio camino, el estado queda marcado como Fallido en sync_queue
/// y el próximo evento de conectividad (o el próximo timer) lo reintenta.
/// </summary>
public class SyncService : IDisposable
{
    private const string ClavePreferenciaUltimaSync = "ultima_sync_utc";

    private readonly LocalDatabase _db;
    private readonly ApiSyncClient _api;
    private readonly string _deviceId;
    private bool _sincronizando;

    public event EventHandler<string>? EstadoCambiado; // para que la UI muestre "Sincronizando...", "Al día", etc.

    public SyncService(LocalDatabase db, ApiSyncClient api, string deviceId)
    {
        _db = db;
        _api = api;
        _deviceId = deviceId;

        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
    }

    private async void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess == NetworkAccess.Internet)
        {
            await SincronizarAsync();
        }
    }

    /// <summary>
    /// Punto de entrada manual (útil para un botón "Sincronizar ahora" en la UI,
    /// además del disparo automático por conectividad).
    /// </summary>
    public async Task SincronizarAsync()
    {
        if (_sincronizando) return; // evita sync duplicada si se dispara dos veces seguidas
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return;

        _sincronizando = true;
        EstadoCambiado?.Invoke(this, "Sincronizando...");

        try
        {
            await EmpujarPendientesAsync();
            await TraerCambiosRemotosAsync();
            EstadoCambiado?.Invoke(this, "Al día");
        }
        catch (HttpRequestException)
        {
            // No relanzamos: un fallo de red no debe tumbar la app.
            // Las operaciones quedan como Pendiente/Fallido y se reintentan después.
            EstadoCambiado?.Invoke(this, "Sin conexión con el servidor — se reintentará automáticamente");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error de sincronización: {ex}"); // detalle técnico solo en consola de debug
            EstadoCambiado?.Invoke(this, "No se pudo sincronizar — se reintentará automáticamente");
        }
        finally
        {
            _sincronizando = false;
        }
    }

    private async Task EmpujarPendientesAsync()
    {
        var pendientes = await _db.ObtenerPendientesAsync();
        if (pendientes.Count == 0) return;

        foreach (var item in pendientes)
        {
            item.Estado = EstadoSync.Enviando;
            await _db.ActualizarEstadoSyncAsync(item);
        }

        var operaciones = pendientes.Select(item => new OperacionSyncDto
        {
            Id = item.Id,
            Tabla = item.Tabla,
            Operacion = item.Operacion,
            Payload = JsonSerializer.Deserialize<object>(item.PayloadJson)!,
            UpdatedAt = item.UpdatedAt,
            DeviceId = item.DeviceId
        }).ToList();

        try
        {
            var respuesta = await _api.PushAsync(operaciones);
            var resultadosPorId = respuesta?.Resultados.ToDictionary(r => r.Id) ?? new();

            foreach (var item in pendientes)
            {
                if (resultadosPorId.TryGetValue(item.Id, out var resultado) &&
                    (resultado.Status == "aplicada" || resultado.Status == "duplicado-ignorado" || resultado.Status == "conflicto-descartado"))
                {
                    item.Estado = EstadoSync.Confirmado;
                }
                else
                {
                    item.Estado = EstadoSync.Fallido;
                    item.Intentos++;
                }

                await _db.ActualizarEstadoSyncAsync(item);
            }
        }
        catch
        {
            // Fallo de red a medio push: todo lo que mandamos como "Enviando"
            // regresa a Fallido para reintentarse en la próxima pasada.
            foreach (var item in pendientes)
            {
                item.Estado = EstadoSync.Fallido;
                item.Intentos++;
                await _db.ActualizarEstadoSyncAsync(item);
            }
            throw;
        }
    }

    private async Task TraerCambiosRemotosAsync()
    {
        var ultimaSync = Preferences.Default.Get(ClavePreferenciaUltimaSync, DateTime.MinValue);

        var respuesta = await _api.PullAsync(ultimaSync, _deviceId);
        if (respuesta == null) return;

        foreach (var producto in respuesta.Productos)
        {
            await _db.AplicarCambioRemotoProductoAsync(producto);
        }

        foreach (var ticket in respuesta.VentaTickets)
        {
            await _db.AplicarCambioRemotoVentaTicketAsync(ticket);
        }

        foreach (var detalle in respuesta.DetalleVentas)
        {
            await _db.AplicarCambioRemotoDetalleVentaAsync(detalle);
        }

        Preferences.Default.Set(ClavePreferenciaUltimaSync, respuesta.ServidorTimestamp);
    }

    public void Dispose()
    {
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
    }
}
