using SQLite;

namespace StockBridge.Client.Data.Models;

[Table("ventas")]
public class Venta
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string ProductoId { get; set; } = string.Empty;

    public int Cantidad { get; set; }

    public decimal Total { get; set; }

    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; }

    public string DeviceId { get; set; } = string.Empty;
}
