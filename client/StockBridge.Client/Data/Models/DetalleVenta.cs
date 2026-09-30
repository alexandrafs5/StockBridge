using SQLite;

namespace StockBridge.Client.Data.Models;

[Table("detalle_ventas")]
public class DetalleVenta
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string SucursalId { get; set; } = string.Empty;

    public string VentaTicketId { get; set; } = string.Empty;

    public string ProductoId { get; set; } = string.Empty;

    public int Cantidad { get; set; }

    public decimal PrecioUnitario { get; set; }

    public decimal Subtotal { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string DeviceId { get; set; } = string.Empty;
}
