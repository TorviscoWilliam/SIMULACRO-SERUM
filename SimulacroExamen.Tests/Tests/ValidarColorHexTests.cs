using SimulacroExamen.Helpers;
using Xunit;

namespace SimulacroExamen.Tests.Tests;

// ════════════════════════════════════════════════════════════════════
//  ValidarColorHexTests
//  Pruebas atomicas sobre la funcion estatica AdminHelpers.ValidarColorHex.
//  Es una funcion pura (no toca BD, ni HTTP, ni archivos): recibe una
//  cadena y devuelve una cadena validada mediante regex.
// ════════════════════════════════════════════════════════════════════
//
//  CODIGO ORIGINAL (Helpers/AdminHelpers.cs:7-11):
//
//      public static string ValidarColorHex(string? valor, string porDefecto) =>
//          !string.IsNullOrWhiteSpace(valor) &&
//          Regex.IsMatch(valor.Trim(), @"^#[0-9A-Fa-f]{3}([0-9A-Fa-f]{3})?$")
//              ? valor.Trim()
//              : porDefecto;
//
//  USOS EN PRODUCCION (4 invocaciones en Controllers/AdminController.cs):
//      - linea 1921: plan.ColorPrimario   = ValidarColorHex(...);   (CrearPlan)
//      - linea 1922: plan.ColorSecundario = ValidarColorHex(...);   (CrearPlan)
//      - linea 1963: existing.ColorPrimario   = ValidarColorHex(...);  (EditarPlan)
//      - linea 1964: existing.ColorSecundario = ValidarColorHex(...);  (EditarPlan)
//
//  Trazabilidad: RF-17 (CRUD planes con caracteristicas visuales).

public class ValidarColorHexTests
{
    private const string ColorDefecto = "#74c0fc";

    // ────────────────────────────────────────────────────────────────
    //  TEST 1 · Hex valido de 6 digitos
    // ────────────────────────────────────────────────────────────────
    //  Que verifica:
    //      Cuando se pasa un color hex valido de 6 digitos (formato #RRGGBB),
    //      la funcion lo devuelve tal cual (sin tocarlo).
    //
    //  Ejemplos de uso esperados:
    //  +----------+--------------------+-----------+
    //  | Entrada  | Regex match?       | Resultado |
    //  +----------+--------------------+-----------+
    //  | #FF5733  | si (6 hex chars)   | #FF5733  <test
    //  | #abc123  | si                 | #abc123
    //  | #000000  | si (negro)         | #000000
    //  | #ffffff  | si (blanco)        | #ffffff
    //  +----------+--------------------+-----------+
    //
    //  Por que importa:
    //      Garantiza que cuando el admin guarda un color valido del color
    //      picker (que produce siempre #RRGGBB), el valor se persiste sin
    //      modificacion. Cualquier alteracion no deseada cambiaria la
    //      apariencia de la tarjeta del plan en la UI.
    // ────────────────────────────────────────────────────────────────
    [Fact]
    public void ValidarColorHex_ConHexValidoSeisDigitos_RetornaElMismoValor()
    {
        // Arrange & Act
        var resultado = AdminHelpers.ValidarColorHex("#FF5733", ColorDefecto);

        // Assert
        Assert.Equal("#FF5733", resultado);
    }


    // ────────────────────────────────────────────────────────────────
    //  TEST 2 · Cadena vacia
    // ────────────────────────────────────────────────────────────────
    //  Que verifica:
    //      Cuando la entrada es una cadena vacia, la funcion devuelve el
    //      color por defecto (no rompe ni devuelve cadena vacia).
    //
    //  Ejemplos de uso esperados:
    //  +------------+--------------+-------------+
    //  | Entrada    | IsNullOrWS?  | Resultado   |
    //  +------------+--------------+-------------+
    //  | ""         | si           | #74c0fc <test
    //  | null       | si           | #74c0fc
    //  | "   "      | si           | #74c0fc
    //  +------------+--------------+-------------+
    //
    //  Por que importa:
    //      Defensa contra formularios enviados sin valor o con espacios.
    //      Sin este guard, el color en BD seria cadena vacia y la UI se
    //      mostraria sin color (CSS roto).
    // ────────────────────────────────────────────────────────────────
    [Fact]
    public void ValidarColorHex_ConValorVacio_RetornaColorDefecto()
    {
        // Arrange & Act
        var resultado = AdminHelpers.ValidarColorHex("", ColorDefecto);

        // Assert
        Assert.Equal(ColorDefecto, resultado);
    }


    // ────────────────────────────────────────────────────────────────
    //  TEST 3 · Formato invalido (no hex)
    // ────────────────────────────────────────────────────────────────
    //  Que verifica:
    //      Cuando la entrada no cumple el formato hex (ej. la palabra
    //      "rojo"), la funcion devuelve el color por defecto.
    //
    //  Ejemplos de uso esperados:
    //  +-------------+--------------------+-------------+
    //  | Entrada     | Regex match?       | Resultado   |
    //  +-------------+--------------------+-------------+
    //  | "rojo"      | NO (sin #)         | #74c0fc <test
    //  | "FF5733"    | NO (sin #)         | #74c0fc
    //  | "#XYZ"      | NO (no hex)        | #74c0fc
    //  | "#FF"       | NO (longitud 2)    | #74c0fc
    //  | "#FF55"     | NO (longitud 4)    | #74c0fc
    //  +-------------+--------------------+-------------+
    //
    //  Por que importa:
    //      Esta es la defensa CRITICA contra inyeccion CSS. Si un
    //      atacante (o usuario mal intencionado) intentara enviar
    //      algo como '; background: url(evil.com); /*', la regex lo
    //      rechaza y se devuelve un color seguro por defecto.
    //      Evita que se inyecte codigo CSS arbitrario en la UI publica.
    // ────────────────────────────────────────────────────────────────
    [Fact]
    public void ValidarColorHex_ConFormatoInvalido_RetornaColorDefecto()
    {
        // Arrange & Act
        var resultado = AdminHelpers.ValidarColorHex("rojo", ColorDefecto);

        // Assert
        Assert.Equal(ColorDefecto, resultado);
    }
}
