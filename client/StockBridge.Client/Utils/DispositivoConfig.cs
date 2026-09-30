namespace StockBridge.Client.Utils;

/// <summary>
/// Cada instalación de StockBridge queda fija a UNA sucursal (como una
/// terminal de punto de venta física). Este valor vive en Preferences del
/// dispositivo, no en la nube — no es un dato de negocio, es configuración
/// local de este equipo en particular.
/// </summary>
public static class DispositivoConfig
{
    private const string Clave = "sucursal_id";

    public static string? SucursalId
    {
        get => Preferences.Default.Get(Clave, (string?)null);
        set
        {
            if (value == null)
                Preferences.Default.Remove(Clave);
            else
                Preferences.Default.Set(Clave, value);
        }
    }
}
