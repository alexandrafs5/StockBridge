using StockBridge.Client.Data.Models;

namespace StockBridge.Client.Services;

/// <summary>
/// Dueño de la sesión activa. Concentra las reglas de permisos por rol
/// para que no queden duplicadas/dispersas por toda la UI — la jerarquía es
/// cajero ⊂ gerente ⊂ dueño.
/// </summary>
public class SessionService
{
    public Usuario? UsuarioActual { get; private set; }

    public event EventHandler? SesionCambiada;

    public void IniciarSesion(Usuario usuario)
    {
        UsuarioActual = usuario;
        SesionCambiada?.Invoke(this, EventArgs.Empty);
    }

    public void CerrarSesion()
    {
        UsuarioActual = null;
        SesionCambiada?.Invoke(this, EventArgs.Empty);
    }

    // Cualquier usuario logueado puede vender.
    public bool PuedeVender => UsuarioActual != null;

    // Gerente y dueño pueden agregar stock a productos existentes.
    public bool PuedeAgregarStock =>
        UsuarioActual?.Rol is Roles.Gerente or Roles.Dueno;

    // Solo el dueño crea productos nuevos, edita precio, o elimina.
    public bool PuedeGestionarProductos => UsuarioActual?.Rol == Roles.Dueno;

    // Solo el dueño da de alta empleados.
    public bool PuedeGestionarUsuarios => UsuarioActual?.Rol == Roles.Dueno;
}
