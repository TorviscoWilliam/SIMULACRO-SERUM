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

        public SessionValidationMiddleware(RequestDelegate next) => _next = next;

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
