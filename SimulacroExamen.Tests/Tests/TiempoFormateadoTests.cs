using SimulacroExamen.ViewModels;
using Xunit;

namespace SimulacroExamen.Tests.Tests;

// ── CÓDIGO ORIGINAL (UsuarioListaViewModel.cs) ───────────────────
// public string TiempoFormateado
// {
//     get
//     {
//         var t = TiempoDesdeCreacion; // DateTime.Now - FechaCreacion
//         if (t.TotalDays >= 365)
//             return $"{(int)(t.TotalDays / 365)} año(s), {(int)(t.TotalDays % 365 / 30)} mes(es)";
//         if (t.TotalDays >= 30)
//             return $"{(int)(t.TotalDays / 30)} mes(es), {(int)(t.TotalDays % 30)} día(s)";
//         if (t.TotalDays >= 1)
//             return $"{(int)t.TotalDays} día(s)";
//         if (t.TotalHours >= 1)
//             return $"{(int)t.TotalHours} hora(s)";
//         return $"{(int)t.TotalMinutes} minuto(s)";
//     }
// }
// ────────────────────────────────────────────────────────────────

public class TiempoFormateadoTests
{
    private static UsuarioListaViewModel ConFecha(DateTime fecha) =>
        new() { FechaCreacion = fecha };

    [Fact]
    public void TiempoFormateado_CuandoEsMenos60Minutos_MuestraMinutos()
    {
        var vm = ConFecha(DateTime.Now.AddMinutes(-30));

        Assert.Equal("30 minuto(s)", vm.TiempoFormateado);
    }

    [Fact]
    public void TiempoFormateado_CuandoEs3Horas_MuestraHoras()
    {
        var vm = ConFecha(DateTime.Now.AddHours(-3));

        Assert.Equal("3 hora(s)", vm.TiempoFormateado);
    }

    [Fact]
    public void TiempoFormateado_CuandoEs5Dias_MuestraDias()
    {
        var vm = ConFecha(DateTime.Now.AddDays(-5));

        Assert.Equal("5 día(s)", vm.TiempoFormateado);
    }

    [Fact]
    public void TiempoFormateado_CuandoEs2Meses_MuestraMesesYDias()
    {
        var vm = ConFecha(DateTime.Now.AddDays(-65));

        Assert.StartsWith("2 mes(es),", vm.TiempoFormateado);
    }

    [Fact]
    public void TiempoFormateado_CuandoEs1Anio_MuestraAniosYMeses()
    {
        var vm = ConFecha(DateTime.Now.AddDays(-400));

        Assert.StartsWith("1 año(s),", vm.TiempoFormateado);
    }
}
