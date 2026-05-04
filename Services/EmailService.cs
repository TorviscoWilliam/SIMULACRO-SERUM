using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using SimulacroExamen.Data;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SimulacroExamen.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _db;
        private readonly ISecretProtector _secretProtector;
        private readonly IHttpClientFactory _httpClientFactory;

        public EmailService(IConfiguration config, ApplicationDbContext db,
                            ISecretProtector secretProtector,
                            IHttpClientFactory httpClientFactory)
        {
            _config            = config;
            _db                = db;
            _secretProtector   = secretProtector;
            _httpClientFactory = httpClientFactory;
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

            if (smtp == "api.resend.com")
                await EnviarViaResendAsync(destinatario, asunto, cuerpoHtml, usuario, remitente, contrasena);
            else
                await EnviarViaSmtpAsync(destinatario, asunto, cuerpoHtml, smtp, port, usuario, contrasena, usarSsl);
        }

        private async Task EnviarViaResendAsync(string destinatario, string asunto, string cuerpoHtml,
                                                string desde, string remitente, string apiKey)
        {
            var payload = new
            {
                from    = $"{remitente} <{desde}>",
                to      = new[] { destinatario },
                subject = asunto,
                html    = cuerpoHtml
            };

            var client  = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Resend API error {(int)response.StatusCode}: {body}");
            }
        }

        private static async Task EnviarViaSmtpAsync(string destinatario, string asunto, string cuerpoHtml,
                                                     string smtp, int port, string usuario,
                                                     string contrasena, bool usarSsl)
        {
            var mensaje = new MimeMessage();
            mensaje.From.Add(new MailboxAddress(usuario, usuario));
            mensaje.To.Add(MailboxAddress.Parse(destinatario));
            mensaje.Subject = asunto;
            mensaje.Body    = new TextPart("html") { Text = cuerpoHtml };

            using var client = new SmtpClient();
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
