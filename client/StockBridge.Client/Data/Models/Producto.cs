using SQLite;

namespace StockBridge.Client.Data.Models;

[Table("productos")]
public class Producto
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Sku { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public decimal Precio { get; set; }

    public int Stock { get; set; }

    public bool Deleted { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string DeviceId { get; set; } = string.Empty;
}
