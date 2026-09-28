using System.Security.Cryptography;
using System.Text;

namespace StockBridge.Client.Utils;

public static class Seguridad
{
    /// <summary>
    /// El PIN nunca se guarda ni se sincroniza en claro — solo su hash.
    /// El servidor nunca ve el PIN real, solo recibe (y almacena) este hash
    /// como cualquier otro campo de la tabla usuarios.
    /// </summary>
    public static string HashPin(string pin)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(pin));
        return Convert.ToHexString(bytes);
    }
}
