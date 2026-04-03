using System.Net;
using System.Net.Mail;

namespace SimulacroExamen.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config) => _config = config;

        public async Task EnviarAsync(string destinatario, string asunto, string cuerpoHtml)
        {
            var smtp      = _config["Email:Smtp"]      ?? "smtp.gmail.com";
            var port      = int.Parse(_config["Email:Port"] ?? "587");
            var usuario   = _config["Email:Usuario"]   ?? "";
            var contrasena= _config["Email:Contrasena"]?? "";
            var remitente = _config["Email:Remitente"] ?? usuario;

            using var client = new SmtpClient(smtp, port)
            {
                EnableSsl   = true,
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
