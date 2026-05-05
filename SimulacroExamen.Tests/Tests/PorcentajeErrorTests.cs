using SimulacroExamen.ViewModels;
using Xunit;

namespace SimulacroExamen.Tests.Tests;

// ── CÓDIGO ORIGINAL (EstadisticaPreguntaVM.cs) ───────────────────
// public double PorcentajeError =>
//     TotalVeces > 0 ? (double)Incorrectas / TotalVeces * 100 : 0;
// ────────────────────────────────────────────────────────────────

public class PorcentajeErrorTests
{
    [Fact]
    public void PorcentajeError_CuandoTotalVecesEsCero_RetornaCero()
    {
        var vm = new EstadisticaPreguntaVM { TotalVeces = 0, Incorrectas = 0 };

        var resultado = vm.PorcentajeError;

        Assert.Equal(0, resultado);
    }

    [Fact]
    public void PorcentajeError_CuandoTodosCorrectos_RetornaCero()
    {
        var vm = new EstadisticaPreguntaVM { TotalVeces = 10, Incorrectas = 0 };

        var resultado = vm.PorcentajeError;

        Assert.Equal(0, resultado);
    }

    [Fact]
    public void PorcentajeError_CuandoTodosIncorrectos_RetornaCien()
    {
        var vm = new EstadisticaPreguntaVM { TotalVeces = 5, Incorrectas = 5 };

        var resultado = vm.PorcentajeError;

        Assert.Equal(100, resultado);
    }

    [Fact]
    public void PorcentajeError_MitadIncorrectos_RetornaCincuenta()
    {
        var vm = new EstadisticaPreguntaVM { TotalVeces = 10, Incorrectas = 5 };

        var resultado = vm.PorcentajeError;

        Assert.Equal(50, resultado);
    }

    [Fact]
    public void PorcentajeError_UnoDeDiez_RetornaDiez()
    {
        var vm = new EstadisticaPreguntaVM { TotalVeces = 10, Incorrectas = 1 };

        var resultado = vm.PorcentajeError;

        Assert.Equal(10, resultado);
    }
}
