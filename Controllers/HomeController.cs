using Microsoft.AspNetCore.Mvc;

namespace SimulacroExamen.Controllers
{
    /// <summary>
    /// Controlador mínimo para servir la página de error genérica.
    /// Program.cs registra app.UseExceptionHandler("/Home/Error"); si esa
    /// ruta no existe, ASP.NET devuelve una respuesta malformada y el
    /// navegador muestra ERR_HTTP_RESPONSE_CODE_FAILURE en lugar del error.
    /// </summary>
    public class HomeController : Controller
    {
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() => View();
    }
}
