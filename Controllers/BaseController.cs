using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace SimulacroExamen.Controllers
{
    /// <summary>
    /// Controlador base para todos los controladores autenticados.
    /// Centraliza el acceso a datos del usuario en sesión (ID, nombre, rol),
    /// evitando duplicar la misma lógica de lectura de claims en cada controlador.
    /// </summary>
    public abstract class BaseController : Controller
    {
        /// <summary>
        /// ID del usuario autenticado actualmente.
        /// Lanza <see cref="UnauthorizedAccessException"/> si el claim no existe
        /// (esto no debería ocurrir en rutas protegidas con [Authorize]).
        /// </summary>
        protected int CurrentUserId =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
                ? id
                : throw new UnauthorizedAccessException("El claim NameIdentifier no está presente.");

        /// <summary>Nombre de usuario (NombreUsuario) del usuario en sesión.</summary>
        protected string CurrentUserName =>
            User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;

        /// <summary>
        /// true si el usuario en sesión tiene el rol SuperAdmin.
        /// Propiedad en lugar de método para uso más limpio en guardas de autorización.
        /// </summary>
        protected bool EsSuperAdmin =>
            User.IsInRole("SuperAdmin");
    }
}
