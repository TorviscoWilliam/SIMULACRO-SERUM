using Microsoft.AspNetCore.DataProtection;
using SimulacroExamen.Services;
using Xunit;

namespace SimulacroExamen.Tests.Tests;

// ════════════════════════════════════════════════════════════════════
//  ProtegerTests
//  Pruebas atomicas sobre SecretProtector.Proteger / Desproteger.
//  Usan el proveedor DataProtection EN MEMORIA (sin BD, sin archivos)
//  mediante DataProtectionProvider.Create(), por lo que siguen siendo
//  tests rapidos y deterministas.
// ════════════════════════════════════════════════════════════════════
//
//  CODIGO ORIGINAL (Services/SecretProtector.cs:24-80):
//
//      private const string Prefijo = "ENC::";
//
//      public string Proteger(string valor)
//      {
//          if (string.IsNullOrEmpty(valor)) return valor;
//          return Prefijo + _protector.Protect(valor);
//      }
//
//      public string Desproteger(string valorCifrado)
//      {
//          if (string.IsNullOrEmpty(valorCifrado)) return valorCifrado;
//          if (!valorCifrado.StartsWith(Prefijo)) return valorCifrado;
//          try   { return _protector.Unprotect(valorCifrado[Prefijo.Length..]); }
//          catch { return string.Empty; }
//      }
//
//  USOS EN PRODUCCION (2 lugares criticos):
//      1. Controllers/AdminController.cs:2182 (cifrar contrasena SMTP
//         al guardar configuracion de correo).
//      2. Services/EmailService.cs:44 (descifrar contrasena SMTP al
//         enviar correo de verificacion, reset, etc.).
//
//  Trazabilidad: RF-01 (envio email confirmacion) + RF-03 (envio
//                enlace recuperacion) — ambos dependen del SMTP cifrado.

public class ProtegerTests
{
    // Helper: crea un SecretProtector con proveedor DataProtection
    // en memoria. Sin BD, sin archivos, sin estado entre tests.
    private static SecretProtector CrearServicio()
    {
        var provider = DataProtectionProvider.Create("SimulacroExamen.Tests");
        return new SecretProtector(provider);
    }

    // ────────────────────────────────────────────────────────────────
    //  TEST 1 · Proteger cadena valida → prefija con "ENC::"
    // ────────────────────────────────────────────────────────────────
    //  Que verifica:
    //      Al cifrar una contrasena en texto plano, el resultado SIEMPRE
    //      comienza con el prefijo literal "ENC::" para que el sistema
    //      pueda detectar si un valor en BD ya esta cifrado o aun esta
    //      en texto plano (backward compatibility).
    //
    //  Ejemplos de uso esperados:
    //  +----------------+----------------------------------+
    //  | Entrada        | Resultado (formato)              |
    //  +----------------+----------------------------------+
    //  | "mi-password"  | "ENC::Cf+abc...xyz123=="  <test  |
    //  | "secret123"    | "ENC::AbCdEf...="                |
    //  +----------------+----------------------------------+
    //
    //  Por que importa:
    //      Si el prefijo se omitiera, Desproteger no sabria que el
    //      valor esta cifrado e intentaria devolverlo como texto plano
    //      al EmailService, que fallaria al autenticarse contra el SMTP.
    // ────────────────────────────────────────────────────────────────
    [Fact]
    public void Proteger_ValorValido_AgregarPrefijoENC()
    {
        // Arrange
        var svc = CrearServicio();

        // Act
        var resultado = svc.Proteger("mi-password");

        // Assert
        Assert.StartsWith("ENC::", resultado);
    }


    // ────────────────────────────────────────────────────────────────
    //  TEST 2 · Round-trip cifrar/descifrar → recupera original
    // ────────────────────────────────────────────────────────────────
    //  Que verifica:
    //      Al cifrar un valor y luego descifrarlo, se recupera EXACTAMENTE
    //      el valor original (sin perder caracteres, sin agregar espacios).
    //
    //  Ejemplos de uso esperados:
    //  +----------------------+-------------------+
    //  | Original             | Despues round-trip|
    //  +----------------------+-------------------+
    //  | "mi-password-smtp"   | "mi-password-smtp" <test
    //  | "Pass!@#$%2024"      | "Pass!@#$%2024"   |
    //  | "  con espacios  "   | "  con espacios  "|
    //  +----------------------+-------------------+
    //
    //  Por que importa:
    //      Este es el caso CRITICO del cifrado: el sistema debe poder
    //      recuperar la contrasena SMTP original para autenticarse
    //      contra Gmail/SendGrid/etc. Si el round-trip falla, los
    //      correos transaccionales (verificacion, reset) no se envian.
    // ────────────────────────────────────────────────────────────────
    [Fact]
    public void Desproteger_ValorCifrado_RecuperaTextoOriginal()
    {
        // Arrange
        var svc = CrearServicio();
        var cifrado = svc.Proteger("mi-password-smtp");

        // Act
        var resultado = svc.Desproteger(cifrado);

        // Assert
        Assert.Equal("mi-password-smtp", resultado);
    }


    // ────────────────────────────────────────────────────────────────
    //  TEST 3 · Backward compat: valor sin prefijo → se devuelve igual
    // ────────────────────────────────────────────────────────────────
    //  Que verifica:
    //      Si una contrasena fue guardada en texto plano por una version
    //      anterior del sistema (antes de implementar el cifrado), la
    //      funcion la devuelve tal cual sin intentar descifrarla.
    //
    //  Ejemplos de uso esperados:
    //  +--------------------------+--------------------------+
    //  | Entrada (sin prefijo)    | Resultado                |
    //  +--------------------------+--------------------------+
    //  | "texto-plano-sin-cifrar" | "texto-plano-sin-cifrar" <test
    //  | "old-smtp-pwd"           | "old-smtp-pwd"           |
    //  +--------------------------+--------------------------+
    //
    //  Por que importa:
    //      Permite migrar gradualmente: cuando un admin actualice la
    //      configuracion SMTP por primera vez tras el deploy, el valor
    //      se cifrara. Mientras tanto, los valores viejos siguen
    //      funcionando sin romper el sistema de correos.
    // ────────────────────────────────────────────────────────────────
    [Fact]
    public void Desproteger_SinPrefijoENC_RetornaValorTalCual()
    {
        // Arrange
        var svc = CrearServicio();

        // Act
        var resultado = svc.Desproteger("texto-plano-sin-cifrar");

        // Assert
        Assert.Equal("texto-plano-sin-cifrar", resultado);
    }


    // ────────────────────────────────────────────────────────────────
    //  TEST 4 · Tolerancia a corrupcion: prefijo ENC:: con basura
    // ────────────────────────────────────────────────────────────────
    //  Que verifica:
    //      Si el contenido despues del prefijo "ENC::" esta corrupto
    //      (ej. alteracion manual de la BD, rotacion de claves), la
    //      funcion devuelve cadena vacia sin lanzar excepcion.
    //
    //  Ejemplos de uso esperados:
    //  +------------------------------+------------+
    //  | Entrada                      | Resultado  |
    //  +------------------------------+------------+
    //  | "ENC::datos-corruptos-xyz"   | ""  <test  |
    //  | "ENC::!!!"                   | ""         |
    //  | "ENC::truncado"              | ""         |
    //  +------------------------------+------------+
    //
    //  Por que importa:
    //      Si las claves de DataProtection se regeneran (ej. tras
    //      cambiar la maquina sin migrar las llaves persistidas en BD),
    //      los valores cifrados antiguos se vuelven ilegibles. En lugar
    //      de hacer crashear todo el sistema de correos, devolvemos ""
    //      y dejamos que el admin reconfigure la contrasena SMTP.
    // ────────────────────────────────────────────────────────────────
    [Fact]
    public void Desproteger_ValorCifradoCorrupto_RetornaVacio()
    {
        // Arrange
        var svc = CrearServicio();

        // Act
        var resultado = svc.Desproteger("ENC::datos-corruptos-xyz");

        // Assert
        Assert.Equal(string.Empty, resultado);
    }
}
