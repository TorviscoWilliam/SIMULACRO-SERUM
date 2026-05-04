using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SimulacroExamen.Data;
using System.Security.Claims;

namespace SimulacroExamen.Middleware
{
    /// <summary>
    /// Garantiza sesión única por usuario.
    /// En cada request autenticado compara el "SessionToken" del claim
    /// con el valor almacenado en BD. Si no coinciden (el usuario inició
    /// sesión desde otro dispositivo), cierra esta sesión y redirige al login.
    /// </summary>
    public class SessionValidationMiddleware
    {
        private readonly RequestDelegate _next;

        /// <summary>
        /// Almacena el siguiente delegado del pipeline de middleware para
        /// que el request continúe su flujo normal cuando la sesión es válida.
        /// </summary>
        /// <param name="next">Siguiente middleware en el pipeline de ASP.NET Core.</param>
        public SessionValidationMiddleware(RequestDelegate next) => _next = next;

        // ── Validación por request ───────────────────────────────────
        /// <summary>
        /// Punto de entrada del middleware; se invoca en cada request HTTP.
        /// Si el usuario está autenticado, verifica que el <c>SessionToken</c>
        /// del claim coincida con el token almacenado en la BD para ese usuario.
        /// Un token diferente indica que el usuario inició sesión desde otro dispositivo,
        /// por lo que se invalida esta sesión y se redirige al login con el parámetro
        /// <c>sesionExpirada=1</c> para mostrar un mensaje informativo.
        /// <para>
        /// El acceso a BD está envuelto en try/catch defensivo: si la columna
        /// <c>SessionToken</c> no existe aún en el esquema (despliegue con esquema viejo)
        /// o hay un fallo transitorio, el request continúa sin interrupción
        /// para evitar una pantalla de error 500 en todo el sitio.
        /// </para>
        /// <para>
        /// El <paramref name="db"/> se inyecta por parámetro (no en el constructor)
        /// porque el middleware es Singleton y el DbContext es Scoped; inyectarlo
        /// en el constructor causaría captive dependency.
        /// </para>
        /// </summary>
        /// <param name="ctx">Contexto HTTP del request actual.</param>
        /// <param name="db">Contexto de BD inyectado como Scoped por el pipeline.</param>
        public async Task InvokeAsync(HttpContext ctx, ApplicationDbContext db)
        {
            if (ctx.User.Identity?.IsAuthenticated == true)
            {
                var idClaim    = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
                var tokenClaim = ctx.User.FindFirstValue("SessionToken");

                // Solo validamos si los claims están presentes (evita romper sesiones antiguas
                // que no tienen el claim SessionToken; simplemente las dejamos pasar esta vez)
                if (idClaim != null && tokenClaim != null && int.TryParse(idClaim, out var userId))
                {
                    // try/catch defensivo: si la columna SessionToken no existe en el
                    // esquema desplegado o hay un fallo transitorio de BD, dejamos pasar
                    // el request en lugar de devolver 500 / romper el pipeline.
                    string? tokenEnBd = null;
                    bool consultaOk = true;
                    try
                    {
                        tokenEnBd = await db.Usuarios
                            .Where(u => u.Id == userId)
                            .Select(u => u.SessionToken)
                            .FirstOrDefaultAsync();
                    }
                    catch
                    {
                        consultaOk = false;
                    }

                    if (consultaOk && tokenEnBd != tokenClaim)
                    {
                        // La sesión fue invalidada por un login desde otro dispositivo
                        await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                        // Redirigir al login con mensaje informativo
                        ctx.Response.Redirect("/Account/Login?sesionExpirada=1");
                        return;
                    }
                }
            }

            await _next(ctx);
        }
    }
}
