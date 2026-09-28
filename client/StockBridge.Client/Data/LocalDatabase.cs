using System.Text.Json;
using SQLite;
using StockBridge.Client.Data.Models;
using StockBridge.Client.Utils;

namespace StockBridge.Client.Data;

/// <summary>
/// Punto único de acceso a SQLite local. Cualquier escritura de negocio
/// (crear/editar producto, registrar venta) pasa por aquí, y CADA escritura
/// también inserta una fila en sync_queue en la misma operación — así nunca
/// hay un cambio de negocio "huérfano" que se quede sin sincronizar.
/// </summary>
public class LocalDatabase
{
    // El backend (Prisma/JS) espera camelCase (sku, nombre, updatedAt...).
    // Se serializa así explícitamente aquí para no depender de cómo se
    // re-serialice más adelante camino al API.
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly SQLiteAsyncConnection _db;
    private readonly string _deviceId;

    public LocalDatabase(string dbPath, string deviceId)
    {
        _deviceId = deviceId;
        _db = new SQLiteAsyncConnection(dbPath);
        _db.CreateTableAsync<Producto>().Wait();
        _db.CreateTableAsync<VentaTicket>().Wait();
        _db.CreateTableAsync<DetalleVenta>().Wait();
        _db.CreateTableAsync<Usuario>().Wait();
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

    /// <summary>
    /// Registra una venta completa: un ticket (encabezado) con una o varias
    /// líneas de producto. Cada línea descuenta su stock correspondiente.
    /// Ticket y líneas se encolan por separado para sync, pero se guardan
    /// juntos en la misma operación local.
    /// </summary>
    public async Task<VentaTicket> RegistrarVentaAsync(string metodoPago, List<(Producto Producto, int Cantidad)> lineasCarrito)
    {
        var ahora = DateTime.UtcNow;
        var total = lineasCarrito.Sum(l => l.Producto.Precio * l.Cantidad);

        var ticket = new VentaTicket
        {
            MetodoPago = metodoPago,
            Total = total,
            Fecha = ahora,
            UpdatedAt = ahora,
            DeviceId = _deviceId
        };

        await _db.InsertAsync(ticket);
        await EncolarAsync("venta_tickets", "create", ticket.Id, ticket, ticket.UpdatedAt);

        foreach (var (producto, cantidad) in lineasCarrito)
        {
            var detalle = new DetalleVenta
            {
                VentaTicketId = ticket.Id,
                ProductoId = producto.Id,
                Cantidad = cantidad,
                PrecioUnitario = producto.Precio,
                Subtotal = producto.Precio * cantidad,
                UpdatedAt = ahora,
                DeviceId = _deviceId
            };

            await _db.InsertAsync(detalle);
            await EncolarAsync("detalle_ventas", "create", detalle.Id, detalle, detalle.UpdatedAt);

            // Cada línea descuenta su propio stock — eso también es un
            // cambio a productos que hay que sincronizar.
            producto.Stock -= cantidad;
            await GuardarProductoAsync(producto, esNuevo: false);
        }

        return ticket;
    }

    public Task<List<VentaTicket>> ObtenerTicketsAsync() =>
        _db.Table<VentaTicket>().OrderByDescending(t => t.Fecha).ToListAsync();

    public Task<List<DetalleVenta>> ObtenerDetallesAsync() => _db.Table<DetalleVenta>().ToListAsync();

    // ---------- Cola de sincronización ----------

    private async Task EncolarAsync(string tabla, string operacion, string payloadId, object payload, DateTime updatedAt)
    {
        var item = new SyncQueueItem
        {
            Tabla = tabla,
            Operacion = operacion,
            PayloadJson = JsonSerializer.Serialize(payload, _jsonOptions),
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

    public async Task AplicarCambioRemotoVentaTicketAsync(VentaTicket remoto)
    {
        var local = await _db.Table<VentaTicket>().Where(t => t.Id == remoto.Id).FirstOrDefaultAsync();
        if (local == null) await _db.InsertAsync(remoto);
        else if (remoto.UpdatedAt > local.UpdatedAt) await _db.UpdateAsync(remoto);
    }

    public async Task AplicarCambioRemotoDetalleVentaAsync(DetalleVenta remoto)
    {
        var local = await _db.Table<DetalleVenta>().Where(d => d.Id == remoto.Id).FirstOrDefaultAsync();
        if (local == null) await _db.InsertAsync(remoto);
        else if (remoto.UpdatedAt > local.UpdatedAt) await _db.UpdateAsync(remoto);
    }

    // ---------- Usuarios ----------

    public async Task<bool> ExisteAlgunUsuarioAsync() =>
        await _db.Table<Usuario>().CountAsync() > 0;

    public Task<List<Usuario>> ObtenerUsuariosAsync() =>
        _db.Table<Usuario>().OrderBy(u => u.Nombre).ToListAsync();

    public async Task<Usuario> CrearUsuarioAsync(string nombre, string pin, string rol)
    {
        var usuario = new Usuario
        {
            Nombre = nombre.Trim(),
            PinHash = Seguridad.HashPin(pin),
            Rol = rol,
            UpdatedAt = DateTime.UtcNow,
            DeviceId = _deviceId
        };

        await _db.InsertAsync(usuario);
        await EncolarAsync("usuarios", "create", usuario.Id, usuario, usuario.UpdatedAt);
        return usuario;
    }

    /// <summary>Devuelve el usuario si el PIN coincide, o null si no.</summary>
    public async Task<Usuario?> VerificarLoginAsync(string usuarioId, string pin)
    {
        var usuario = await _db.Table<Usuario>().Where(u => u.Id == usuarioId).FirstOrDefaultAsync();
        if (usuario == null) return null;
        return usuario.PinHash == Seguridad.HashPin(pin) ? usuario : null;
    }

    public async Task AplicarCambioRemotoUsuarioAsync(Usuario remoto)
    {
        var local = await _db.Table<Usuario>().Where(u => u.Id == remoto.Id).FirstOrDefaultAsync();
        if (local == null) await _db.InsertAsync(remoto);
        else if (remoto.UpdatedAt > local.UpdatedAt) await _db.UpdateAsync(remoto);
    }
}
