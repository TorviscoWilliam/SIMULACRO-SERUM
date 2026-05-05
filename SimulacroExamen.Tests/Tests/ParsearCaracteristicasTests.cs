using SimulacroExamen.Helpers;
using Xunit;

namespace SimulacroExamen.Tests.Tests;

// ── CÓDIGO ORIGINAL (AdminController.cs) ────────────────────────
// private static List<CaracteristicaPlan> ParsearCaracteristicas(string texto) =>
//     texto.Split('\n', StringSplitOptions.RemoveEmptyEntries)
//          .Select(l => l.Trim())
//          .Where(l => !string.IsNullOrWhiteSpace(l))
//          .Select((t, i) => new CaracteristicaPlan { Texto = t, Orden = i })
//          .ToList();
// ────────────────────────────────────────────────────────────────
// Nota: se extrajo a SimulacroExamen.Helpers.AdminHelpers para poder testear.

public class ParsearCaracteristicasTests
{
    [Fact]
    public void ParsearCaracteristicas_ConTresLineas_RetornaTresElementos()
    {
        var texto = "Acceso ilimitado\nCalculadora\nNoticias";

        var resultado = AdminHelpers.ParsearCaracteristicas(texto);

        Assert.Equal(3, resultado.Count);
    }

    [Fact]
    public void ParsearCaracteristicas_AsignaOrdenCorrelativo()
    {
        var texto = "Primera\nSegunda\nTercera";

        var resultado = AdminHelpers.ParsearCaracteristicas(texto);

        Assert.Equal(0, resultado[0].Orden);
        Assert.Equal(1, resultado[1].Orden);
        Assert.Equal(2, resultado[2].Orden);
    }

    [Fact]
    public void ParsearCaracteristicas_IgnoraLineasVacias()
    {
        var texto = "Primera\n\n\nSegunda";

        var resultado = AdminHelpers.ParsearCaracteristicas(texto);

        Assert.Equal(2, resultado.Count);
    }

    [Fact]
    public void ParsearCaracteristicas_ConTextoVacio_RetornaListaVacia()
    {
        var resultado = AdminHelpers.ParsearCaracteristicas("");

        Assert.Empty(resultado);
    }

    [Fact]
    public void ParsearCaracteristicas_RecortaEspaciosEnBlanco()
    {
        var texto = "  Acceso ilimitado  \n  Calculadora  ";

        var resultado = AdminHelpers.ParsearCaracteristicas(texto);

        Assert.Equal("Acceso ilimitado", resultado[0].Texto);
        Assert.Equal("Calculadora", resultado[1].Texto);
    }
}
