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

    public class SecretProtector : ISecretProtector
    {
        private readonly IDataProtector _protector;
        private const string Prefijo = "ENC::";

        public SecretProtector(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector("SimulacroExamen.SMTP.v1");
        }

        public string Proteger(string valor)
        {
            if (string.IsNullOrEmpty(valor)) return valor;
            return Prefijo + _protector.Protect(valor);
        }

        public string Desproteger(string valorCifrado)
        {
            if (string.IsNullOrEmpty(valorCifrado)) return valorCifrado;
            if (!valorCifrado.StartsWith(Prefijo)) return valorCifrado;
            try
            {
                return _protector.Unprotect(valorCifrado[Prefijo.Length..]);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
