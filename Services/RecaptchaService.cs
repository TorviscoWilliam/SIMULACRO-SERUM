using System.Text.Json;

namespace SimulacroExamen.Services
{
    /// <summary>
    /// Implementación de <see cref="IRecaptchaService"/> que llama a la API
    /// de Google reCAPTCHA v3 para validar tokens enviados desde el cliente.
    /// </summary>
    public class RecaptchaService : IRecaptchaService
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<RecaptchaService> _log;

        private const string VerifyUrl = "https://www.google.com/recaptcha/api/siteverify";

        public RecaptchaService(IHttpClientFactory httpFactory, IConfiguration config,
                                ILogger<RecaptchaService> log)
        {
            _httpFactory = httpFactory;
            _config      = config;
            _log         = log;
        }

        public async Task<bool> VerificarAsync(string? token, string accion, double scoreMinimo)
        {
            // La comprobación de SecretKey va ANTES que la del token: si no hay SecretKey
            // tampoco hay SiteKey en la vista → el script JS no carga → el token siempre
            // llega vacío. Verificar el token primero haría que el bypass de dev nunca aplique.
            var secret = _config["Recaptcha:SecretKey"];
            if (string.IsNullOrWhiteSpace(secret))
            {
                _log.LogWarning("Recaptcha:SecretKey no configurado. Saltando verificación reCAPTCHA.");
                return true;
            }

            // Con SecretKey configurado, el token es obligatorio.
            if (string.IsNullOrWhiteSpace(token)) return false;

            try
            {
                var client = _httpFactory.CreateClient();
                var body = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("secret",   secret),
                    new KeyValuePair<string, string>("response", token)
                });

                var resp = await client.PostAsync(VerifyUrl, body);
                if (!resp.IsSuccessStatusCode)
                {
                    _log.LogWarning("reCAPTCHA HTTP {Status} al verificar token.", (int)resp.StatusCode);
                    return false;
                }

                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var success = root.TryGetProperty("success", out var s) && s.GetBoolean();
                var score   = root.TryGetProperty("score",   out var sc) ? sc.GetDouble() : 0;
                var action  = root.TryGetProperty("action",  out var a)  ? a.GetString()  : "";

                var ok = success && score >= scoreMinimo && action == accion;
                if (!ok)
                    _log.LogInformation("reCAPTCHA rechazó token. success={Success}, score={Score}, action={Action}",
                        success, score, action);
                return ok;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error verificando reCAPTCHA. Se rechaza el intento por seguridad.");
                return false;
            }
        }
    }
}
