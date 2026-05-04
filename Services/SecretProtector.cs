using Microsoft.AspNetCore.DataProtection;

namespace SimulacroExamen.Services
{
    /// <summary>
    /// Cifra/descifra cadenas sensibles (ej. contraseña SMTP) usando el sistema
    /// de DataProtection de ASP.NET Core. La clave se persiste por la plataforma
    /// (filesystem en desarrollo; puede configurarse para Azure Key Vault).
    /// </summary>
    public interface ISecretProtector
    {
        string Proteger(string valor);
        string Desproteger(string valorCifrado);
    }

    /// <summary>
    /// Implementación de <see cref="ISecretProtector"/> que usa el sistema de
    /// DataProtection de ASP.NET Core para cifrar y descifrar cadenas sensibles.
    /// Utiliza el propósito fijo <c>"SimulacroExamen.SMTP.v1"</c> para aislar
    /// los datos protegidos de otros usos de DataProtection en la misma app.
    /// Los valores cifrados se almacenan con el prefijo <c>ENC::</c> para
    /// distinguirlos de texto plano en BD (compatibilidad con versiones antiguas).
    /// </summary>
    public class SecretProtector : ISecretProtector
    {
        private readonly IDataProtector _protector;

        // Prefijo que indica que el valor está cifrado; permite detectar
        // contraseñas guardadas en texto plano por versiones anteriores.
        private const string Prefijo = "ENC::";

        /// <summary>
        /// Crea el protector usando el propósito específico de la app.
        /// El <paramref name="provider"/> es inyectado por DI y gestiona
        /// la persistencia y rotación de claves criptográficas.
        /// </summary>
        /// <param name="provider">Proveedor de DataProtection registrado en Program.cs.</param>
        public SecretProtector(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector("SimulacroExamen.SMTP.v1");
        }

        // ── Cifrado ──────────────────────────────────────────────────
        /// <summary>
        /// Cifra el valor y le antepone el prefijo <c>ENC::</c>.
        /// Si el valor está vacío o es nulo lo devuelve sin modificar.
        /// </summary>
        /// <param name="valor">Cadena en texto plano a proteger (ej. contraseña SMTP).</param>
        /// <returns>Cadena cifrada con prefijo <c>ENC::</c>, o el valor original si está vacío.</returns>
        public string Proteger(string valor)
        {
            if (string.IsNullOrEmpty(valor)) return valor;
            return Prefijo + _protector.Protect(valor);
        }

        // ── Descifrado ───────────────────────────────────────────────
        /// <summary>
        /// Descifra un valor previamente cifrado con <see cref="Proteger"/>.
        /// Si el valor no lleva el prefijo <c>ENC::</c>, se asume que es texto
        /// plano (guardado por una versión anterior) y se devuelve tal cual.
        /// Si el descifrado falla (clave rotada, datos corruptos), devuelve
        /// <see cref="string.Empty"/> en lugar de lanzar excepción.
        /// </summary>
        /// <param name="valorCifrado">Cadena almacenada en BD (con o sin prefijo ENC::).</param>
        /// <returns>Texto plano descifrado, el valor original si no tiene prefijo, o vacío si falla.</returns>
        public string Desproteger(string valorCifrado)
        {
            if (string.IsNullOrEmpty(valorCifrado)) return valorCifrado;
            if (!valorCifrado.StartsWith(Prefijo)) return valorCifrado; // texto plano heredado
            try
            {
                return _protector.Unprotect(valorCifrado[Prefijo.Length..]);
            }
            catch
            {
                // La clave fue rotada o el valor está corrupto; devolver vacío
                // para que el llamador detecte configuración inválida.
                return string.Empty;
            }
        }
    }
}
