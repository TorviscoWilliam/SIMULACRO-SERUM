using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using SimulacroExamen.Data;

namespace SimulacroExamen.Services
{
    /// <summary>
    /// Implementación de <see cref="IEmailService"/> que envía correos vía SMTP.
    /// La configuración del servidor SMTP se obtiene primero desde la BD
    /// (tabla ConfiguracionCorreo, gestionada por el SuperAdmin) y, si no existe
    /// ningún registro, se recae en los valores de <c>appsettings.json</c>.
    /// </summary>
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _db;
        private readonly ISecretProtector _secretProtector;

        /// <summary>
        /// Inyecta la configuración de la app, el contexto de BD y el servicio
        /// de cifrado de secretos (para desproteger la contraseña SMTP guardada en BD).
        /// </summary>
        public EmailService(IConfiguration config, ApplicationDbContext db,
                            ISecretProtector secretProtector)
        {
            _config          = config;
            _db              = db;
            _secretProtector = secretProtector;
        }

        // ── Envío de correo ──────────────────────────────────────────
        /// <summary>
        /// Envía un correo electrónico en formato HTML al destinatario indicado.
        /// Resuelve la configuración SMTP en el siguiente orden de prioridad:
        ///   1. Fila en la tabla <c>ConfiguracionCorreo</c> (configurada por el SuperAdmin).
        ///   2. Sección <c>Email</c> de <c>appsettings.json</c> como fallback.
        /// Lanza <see cref="InvalidOperationException"/> si ninguna fuente provee configuración válida.
        /// </summary>
        /// <param name="destinatario">Dirección de correo del receptor.</param>
        /// <param name="asunto">Asunto del mensaje.</param>
        /// <param name="cuerpoHtml">Cuerpo del mensaje en formato HTML.</param>
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
                // La contraseña puede estar cifrada (formato ENC::...) o en texto
                // plano si fue guardada por una versión anterior. Desproteger es idempotente.
                contrasena = _secretProtector.Desproteger(cfgDb.Contrasena);
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
