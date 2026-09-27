using System.Text.Json;
using SQLite;
using StockBridge.Client.Data.Models;

namespace StockBridge.Client.Data;

/// <summary>
/// Punto único de acceso a SQLite local. Cualquier escritura de negocio
/// (crear/editar producto, registrar venta) pasa por aquí, y CADA escritura
/// también inserta una fila en sync_queue en la misma operación — así nunca
/// hay un cambio de negocio "huérfano" que se quede sin sincronizar.
/// </summary>
public class LocalDatabase
{
    private readonly SQLiteAsyncConnection _db;
    private readonly string _deviceId;

    public LocalDatabase(string dbPath, string deviceId)
    {
        _deviceId = deviceId;
        _db = new SQLiteAsyncConnection(dbPath);
        _db.CreateTableAsync<Producto>().Wait();
        _db.CreateTableAsync<Venta>().Wait();
        _db.CreateTableAsync<SyncQueueItem>().Wait();
    }

    // ---------- Productos ----------

    public Task<List<Producto>> ObtenerProductosAsync() =>
        _db.Table<Producto>().Where(p => !p.Deleted).ToListAsync();

    public async Task GuardarProductoAsync(Producto producto, bool esNuevo)
    {
        producto.UpdatedAt = DateTime.UtcNow;
        producto.DeviceId = _deviceId;

        if (esNuevo)
            await _db.InsertAsync(producto);
        else
            await _db.UpdateAsync(producto);

        await EncolarAsync("productos", esNuevo ? "create" : "update", producto.Id, producto, producto.UpdatedAt);
    }

    public async Task EliminarProductoAsync(Producto producto)
    {
        producto.Deleted = true;
        producto.UpdatedAt = DateTime.UtcNow;
        producto.DeviceId = _deviceId;
        await _db.UpdateAsync(producto);

        await EncolarAsync("productos", "delete", producto.Id, producto, producto.UpdatedAt);
    }

    // ---------- Ventas ----------

    public async Task RegistrarVentaAsync(Venta venta, Producto productoAfectado)
    {
        venta.UpdatedAt = DateTime.UtcNow;
        venta.DeviceId = _deviceId;
        await _db.InsertAsync(venta);
        await EncolarAsync("ventas", "create", venta.Id, venta, venta.UpdatedAt);

        // La venta descuenta stock — eso también es un cambio a productos
        // que hay que sincronizar.
        productoAfectado.Stock -= venta.Cantidad;
        await GuardarProductoAsync(productoAfectado, esNuevo: false);
    }

    public Task<List<Venta>> ObtenerVentasAsync() => _db.Table<Venta>().ToListAsync();

    // ---------- Cola de sincronización ----------

    private async Task EncolarAsync(string tabla, string operacion, string payloadId, object payload, DateTime updatedAt)
    {
        var item = new SyncQueueItem
        {
            Tabla = tabla,
            Operacion = operacion,
            PayloadJson = JsonSerializer.Serialize(payload),
            UpdatedAt = updatedAt,
            DeviceId = _deviceId,
            Estado = EstadoSync.Pendiente
        };

        await _db.InsertAsync(item);
    }

    public Task<List<SyncQueueItem>> ObtenerPendientesAsync() =>
        _db.Table<SyncQueueItem>()
           .Where(i => i.Estado == EstadoSync.Pendiente || i.Estado == EstadoSync.Fallido)
           .OrderBy(i => i.CreatedAt)
           .ToListAsync();

    public Task ActualizarEstadoSyncAsync(SyncQueueItem item) => _db.UpdateAsync(item);

    // Aplica un cambio que vino del servidor (via /sync/pull) al SQLite local,
    // sin volver a encolarlo (si no, entraríamos en un loop de sync infinito).
    public async Task AplicarCambioRemotoProductoAsync(Producto remoto)
    {
        var local = await _db.Table<Producto>().Where(p => p.Id == remoto.Id).FirstOrDefaultAsync();

        if (local == null)
        {
            await _db.InsertAsync(remoto);
        }
        else if (remoto.UpdatedAt > local.UpdatedAt)
        {
            // last-write-wins: el remoto es más nuevo, gana
            await _db.UpdateAsync(remoto);
        }
        // si el local es más nuevo o igual, no se toca —
        // ya se encargará el próximo push de mandarlo al servidor.
    }
}
