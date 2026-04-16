using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using SimulacroExamen.Data;

namespace SimulacroExamen.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _db;

        public EmailService(IConfiguration config, ApplicationDbContext db)
        {
            _config = config;
            _db     = db;
        }

        public async Task EnviarAsync(string destinatario, string asunto, string cuerpoHtml)
        {
            // Intenta leer la configuración guardada por el SuperAdmin en la BD.
            // Si no existe fila, usa los valores de appsettings.json como fallback.
            var cfgDb = await _db.ConfiguracionCorreo
                .OrderBy(c => c.Id)
                .FirstOrDefaultAsync();

            string smtp, usuario, contrasena, remitente;
            int    port;
            bool   usarSsl;

            if (cfgDb != null)
            {
                smtp       = cfgDb.Smtp;
                port       = cfgDb.Puerto;
                usuario    = cfgDb.UsuarioCorreo;
                contrasena = cfgDb.Contrasena;
                remitente  = cfgDb.NombreRemitente;
                usarSsl    = cfgDb.UsarSsl;
            }
            else
            {
                // Fallback a appsettings.json (solo si existe la sección Email completa)
                smtp       = _config["Email:Smtp"];
                usuario    = _config["Email:Usuario"];
                contrasena = _config["Email:Contrasena"];

                if (string.IsNullOrWhiteSpace(smtp) ||
                    string.IsNullOrWhiteSpace(usuario) ||
                    string.IsNullOrWhiteSpace(contrasena))
                    throw new InvalidOperationException(
                        "El correo no está configurado. El SuperAdmin debe configurarlo " +
                        "desde Admin → Correo antes de que el sistema pueda enviar emails.");

                port      = int.Parse(_config["Email:Port"] ?? "587");
                remitente = _config["Email:Remitente"] ?? usuario;
                usarSsl   = true;
            }

            using var client = new SmtpClient(smtp, port)
            {
                EnableSsl   = usarSsl,
                Credentials = new NetworkCredential(usuario, contrasena)
            };

            using var mensaje = new MailMessage
            {
                From       = new MailAddress(usuario, remitente),
                Subject    = asunto,
                Body       = cuerpoHtml,
                IsBodyHtml = true
            };
            mensaje.To.Add(destinatario);

            await client.SendMailAsync(mensaje);
        }
    }
}
