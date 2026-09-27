using System.Globalization;

namespace StockBridge.Client.Utils;

public static class Formato
{
    /// <summary>
    /// Formatea como "$123,456.00" — signo de pesos y coma de miles,
    /// sin depender de la configuración regional del dispositivo (que
    /// podría mostrar euros o usar coma como separador decimal).
    /// </summary>
    public static string Moneda(decimal valor) =>
        "$" + valor.ToString("#,0.00", CultureInfo.InvariantCulture);
}
