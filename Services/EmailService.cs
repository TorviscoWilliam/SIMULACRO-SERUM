using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using SimulacroExamen.Data;

namespace SimulacroExamen.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _db;
        private readonly ISecretProtector _secretProtector;

        public EmailService(IConfiguration config, ApplicationDbContext db,
                            ISecretProtector secretProtector)
        {
            _config          = config;
            _db              = db;
            _secretProtector = secretProtector;
        }

        public async Task EnviarAsync(string destinatario, string asunto, string cuerpoHtml)
        {
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
                contrasena = _secretProtector.Desproteger(cfgDb.Contrasena);
                remitente  = cfgDb.NombreRemitente;
                usarSsl    = cfgDb.UsarSsl;

                if (string.IsNullOrEmpty(contrasena))
                    throw new InvalidOperationException(
                        "No se pudo descifrar la contraseña SMTP. Vuelve a guardar la " +
                        "configuración de correo desde Admin → Correo.");
            }
            else
            {
                smtp       = _config["Email:Smtp"] ?? string.Empty;
                usuario    = _config["Email:Usuario"] ?? string.Empty;
                contrasena = _config["Email:Contrasena"] ?? string.Empty;

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

            var mensaje = new MimeMessage();
            mensaje.From.Add(new MailboxAddress(remitente, usuario));
            mensaje.To.Add(MailboxAddress.Parse(destinatario));
            mensaje.Subject = asunto;
            mensaje.Body    = new TextPart("html") { Text = cuerpoHtml };

            using var client = new SmtpClient();

            // Puerto 465 → SSL implícito; puerto 587 (u otro) → STARTTLS
            var secureOption = port == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;

            await client.ConnectAsync(smtp, port, secureOption);
            await client.AuthenticateAsync(usuario, contrasena);
            await client.SendAsync(mensaje);
            await client.DisconnectAsync(true);
        }
    }
}
