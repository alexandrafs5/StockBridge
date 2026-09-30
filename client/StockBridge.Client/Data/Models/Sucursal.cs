using SQLite;

namespace StockBridge.Client.Data.Models;

[Table("sucursales")]
public class Sucursal
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Nombre { get; set; } = string.Empty;

    public string? Direccion { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string DeviceId { get; set; } = string.Empty;
}
