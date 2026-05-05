using Microsoft.AspNetCore.DataProtection;
using SimulacroExamen.Services;
using Xunit;

namespace SimulacroExamen.Tests.Tests;

// ── CÓDIGO ORIGINAL (SecretProtector.cs) ────────────────────────
// private const string Prefijo = "ENC::";
//
// public string Proteger(string valor)
// {
//     if (string.IsNullOrEmpty(valor)) return valor;
//     return Prefijo + _protector.Protect(valor);
// }
//
// public string Desproteger(string valorCifrado)
// {
//     if (string.IsNullOrEmpty(valorCifrado)) return valorCifrado;
//     if (!valorCifrado.StartsWith(Prefijo)) return valorCifrado;
//     try   { return _protector.Unprotect(valorCifrado[Prefijo.Length..]); }
//     catch { return string.Empty; }
// }
// ────────────────────────────────────────────────────────────────

public class ProtegerTests
{
    // Usa el proveedor de DataProtection en memoria (sin BD, sin archivos)
    private static SecretProtector CrearServicio()
    {
        var provider = DataProtectionProvider.Create("SimulacroExamen.Tests");
        return new SecretProtector(provider);
    }

    [Fact]
    public void Proteger_ValorVacio_RetornaVacio()
    {
        var svc = CrearServicio();

        var resultado = svc.Proteger("");

        Assert.Equal("", resultado);
    }

    [Fact]
    public void Proteger_ValorValido_AgregarPrefijoENC()
    {
        var svc = CrearServicio();

        var resultado = svc.Proteger("mi-password");

        Assert.StartsWith("ENC::", resultado);
    }

    [Fact]
    public void Desproteger_ValorCifrado_RecuperaTextoOriginal()
    {
        var svc = CrearServicio();
        var cifrado = svc.Proteger("mi-password-smtp");

        var resultado = svc.Desproteger(cifrado);

        Assert.Equal("mi-password-smtp", resultado);
    }

    [Fact]
    public void Desproteger_SinPrefijoENC_RetornaValorTalCual()
    {
        var svc = CrearServicio();

        // Simula contraseña guardada en texto plano por versión anterior
        var resultado = svc.Desproteger("texto-plano-sin-cifrar");

        Assert.Equal("texto-plano-sin-cifrar", resultado);
    }

    [Fact]
    public void Desproteger_ValorCifradoCorrupto_RetornaVacio()
    {
        var svc = CrearServicio();

        // ENC:: está presente pero el contenido es basura
        var resultado = svc.Desproteger("ENC::datos-corruptos-xyz");

        Assert.Equal(string.Empty, resultado);
    }
}
