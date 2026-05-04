namespace SimulacroExamen.Services
{
    /// <summary>
    /// Contrato para el servicio de envío de correos electrónicos del sistema.
    /// La implementación concreta (<see cref="EmailService"/>) se resuelve
    /// por inyección de dependencias y obtiene la configuración SMTP desde BD
    /// o desde <c>appsettings.json</c>.
    /// </summary>
    public interface IEmailService
    {
        /// <summary>
        /// Envía un correo electrónico en formato HTML.
        /// </summary>
        /// <param name="destinatario">Dirección de correo del receptor.</param>
        /// <param name="asunto">Asunto del mensaje.</param>
        /// <param name="cuerpoHtml">Cuerpo del mensaje en formato HTML.</param>
        Task EnviarAsync(string destinatario, string asunto, string cuerpoHtml);
    }
}
