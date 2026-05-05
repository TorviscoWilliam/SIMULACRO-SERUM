using SimulacroExamen.Helpers;
using Xunit;

namespace SimulacroExamen.Tests.Tests;

// ── CÓDIGO ORIGINAL (AdminController.cs) ────────────────────────
// private static string ValidarColorHex(string? valor, string porDefecto) =>
//     !string.IsNullOrWhiteSpace(valor) &&
//     Regex.IsMatch(valor.Trim(), @"^#[0-9A-Fa-f]{3}([0-9A-Fa-f]{3})?$")
//         ? valor.Trim()
//         : porDefecto;
// ────────────────────────────────────────────────────────────────
// Nota: se extrajo a SimulacroExamen.Helpers.AdminHelpers para poder testear.

public class ValidarColorHexTests
{
    private const string ColorDefecto = "#74c0fc";

    [Fact]
    public void ValidarColorHex_ConHexValidoSeisDigitos_RetornaElMismoValor()
    {
        var resultado = AdminHelpers.ValidarColorHex("#FF5733", ColorDefecto);

        Assert.Equal("#FF5733", resultado);
    }

    [Fact]
    public void ValidarColorHex_ConHexValidoTresDigitos_RetornaElMismoValor()
    {
        var resultado = AdminHelpers.ValidarColorHex("#FFF", ColorDefecto);

        Assert.Equal("#FFF", resultado);
    }

    [Fact]
    public void ValidarColorHex_ConValorNulo_RetornaColorDefecto()
    {
        var resultado = AdminHelpers.ValidarColorHex(null, ColorDefecto);

        Assert.Equal(ColorDefecto, resultado);
    }

    [Fact]
    public void ValidarColorHex_ConValorVacio_RetornaColorDefecto()
    {
        var resultado = AdminHelpers.ValidarColorHex("", ColorDefecto);

        Assert.Equal(ColorDefecto, resultado);
    }

    [Fact]
    public void ValidarColorHex_ConFormatoInvalido_RetornaColorDefecto()
    {
        var resultado = AdminHelpers.ValidarColorHex("rojo", ColorDefecto);

        Assert.Equal(ColorDefecto, resultado);
    }
}
