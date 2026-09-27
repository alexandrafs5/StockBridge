using SQLite;

namespace StockBridge.Client.Data.Models;

public enum EstadoSync
{
    Pendiente,
    Enviando,
    Confirmado,
    Fallido
}

[Table("sync_queue")]
public class SyncQueueItem
{
    // Este Id viaja al servidor como identificador de la operación —
    // es la clave que usa el API para garantizar idempotencia.
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Tabla { get; set; } = string.Empty; // "productos" | "ventas"

    public string Operacion { get; set; } = string.Empty; // "create" | "update" | "delete"

    // El registro completo (Producto o Venta) serializado como JSON.
    public string PayloadJson { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; }

    public string DeviceId { get; set; } = string.Empty;

    public EstadoSync Estado { get; set; } = EstadoSync.Pendiente;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int Intentos { get; set; } = 0;
}
