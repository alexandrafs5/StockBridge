namespace StockBridge.Client.Services;

public class OperacionSyncDto
{
    public string Id { get; set; } = string.Empty;
    public string Tabla { get; set; } = string.Empty;
    public string Operacion { get; set; } = string.Empty;
    public object Payload { get; set; } = new();
    public DateTime UpdatedAt { get; set; }
    public string DeviceId { get; set; } = string.Empty;
}

public class PushRequestDto
{
    public List<OperacionSyncDto> Operaciones { get; set; } = new();
}

public class ResultadoOperacionDto
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Mensaje { get; set; }
}

public class PushResponseDto
{
    public List<ResultadoOperacionDto> Resultados { get; set; } = new();
}

public class PullResponseDto
{
    public DateTime ServidorTimestamp { get; set; }
    public List<Data.Models.Producto> Productos { get; set; } = new();
    public List<Data.Models.VentaTicket> VentaTickets { get; set; } = new();
    public List<Data.Models.DetalleVenta> DetalleVentas { get; set; } = new();
}
