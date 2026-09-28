using SQLite;

namespace StockBridge.Client.Data.Models;

[Table("venta_tickets")]
public class VentaTicket
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string MetodoPago { get; set; } = "efectivo"; // "efectivo" | "transferencia" | "tarjeta"

    public decimal Total { get; set; }

    public string? VendedorId { get; set; }

    public string? VendedorNombre { get; set; }

    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; }

    public string DeviceId { get; set; } = string.Empty;
}
