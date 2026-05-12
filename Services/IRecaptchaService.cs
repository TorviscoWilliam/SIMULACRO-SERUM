namespace SimulacroExamen.Services
{
    /// <summary>
    /// Contrato para verificar tokens de Google reCAPTCHA v3 contra la API de Google.
    /// Devuelve true si el token es válido y supera el score mínimo configurado.
    /// </summary>
    public interface IRecaptchaService
    {
        /// <summary>
        /// Verifica un token de reCAPTCHA v3 contra https://www.google.com/recaptcha/api/siteverify.
        /// </summary>
        /// <param name="token">Token devuelto por grecaptcha.execute en el cliente.</param>
        /// <param name="accion">Nombre de la acción esperada (ej. "login"). Debe coincidir con la usada en el cliente.</param>
        /// <param name="scoreMinimo">Score mínimo aceptable (0.0 a 1.0). Recomendado: 0.5.</param>
        /// <returns>true si Google validó el token, el score es suficiente y la acción coincide.</returns>
        Task<bool> VerificarAsync(string? token, string accion, double scoreMinimo);
    }
}
