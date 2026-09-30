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
    private readonly object _lock = new();
    private Task<bool>? _syncEnCurso;
 
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
    /// Devuelve true si la sincronización se completó, false si no hubo conexión
    /// o falló. Si ya hay una sincronización en curso, quien llame espera esa
    /// misma en vez de lanzar otra.
    /// </summary>
    public Task<bool> SincronizarAsync()
    {
        lock (_lock)
        {
            if (_syncEnCurso != null) return _syncEnCurso;
 
            var tarea = EjecutarSincronizacionAsync();
            if (tarea.IsCompleted) return tarea; // terminó al instante (p. ej. sin internet)
 
            _syncEnCurso = tarea;
            tarea.ContinueWith(_ =>
            {
                lock (_lock) { _syncEnCurso = null; }
            }, TaskScheduler.Default);
 
            return tarea;
        }
    }
 
    private async Task<bool> EjecutarSincronizacionAsync()
    {
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return false;
 
        EstadoCambiado?.Invoke(this, "Sincronizando...");
 
        try
        {
            await EmpujarPendientesAsync();
            await TraerCambiosRemotosAsync();
            EstadoCambiado?.Invoke(this, "Al día");
            return true;
        }
        catch (HttpRequestException)
        {
            // No relanzamos: un fallo de red no debe tumbar la app.
            // Las operaciones quedan como Pendiente/Fallido y se reintentan después.
            EstadoCambiado?.Invoke(this, "Sin conexión con el servidor — se reintentará automáticamente");
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error de sincronización: {ex}"); // detalle técnico solo en consola de debug
            EstadoCambiado?.Invoke(this, "No se pudo sincronizar — se reintentará automáticamente");
            return false;
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
 
        var respuesta = await _api.PullAsync(ultimaSync, _deviceId, Utils.DispositivoConfig.SucursalId);
        if (respuesta == null) return;
 
        foreach (var sucursal in respuesta.Sucursales)
        {
            await _db.AplicarCambioRemotoSucursalAsync(sucursal);
        }
 
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
 
        foreach (var usuario in respuesta.Usuarios)
        {
            await _db.AplicarCambioRemotoUsuarioAsync(usuario);
        }
 
        Preferences.Default.Set(ClavePreferenciaUltimaSync, respuesta.ServidorTimestamp);
    }
 
    /// <summary>
    /// Borra el marcador de "última sincronización" para que el próximo pull
    /// traiga TODO el historial, no solo lo nuevo desde la última vez. Se usa
    /// cuando el dispositivo recién se asigna a una sucursal: los pulls
    /// anteriores (antes de tener sucursal) no traían inventario/ventas de
    /// nadie, así que hay que traerlos completos desde cero.
    /// </summary>
    public void ForzarResincronizacionCompleta() =>
        Preferences.Default.Remove(ClavePreferenciaUltimaSync);
 
    public void Dispose()
    {
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
    }
}
 
