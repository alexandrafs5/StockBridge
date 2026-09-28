using SQLite;

namespace StockBridge.Client.Data.Models;

public static class Roles
{
    public const string Cajero = "cajero";
    public const string Gerente = "gerente";
    public const string Dueno = "dueno";
}

[Table("usuarios")]
public class Usuario
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Nombre { get; set; } = string.Empty;

    public string PinHash { get; set; } = string.Empty;

    public string Rol { get; set; } = Roles.Cajero;

    public bool Deleted { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string DeviceId { get; set; } = string.Empty;
}
