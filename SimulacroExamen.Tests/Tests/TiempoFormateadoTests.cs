using SimulacroExamen.ViewModels;
using Xunit;

namespace SimulacroExamen.Tests.Tests;

// ════════════════════════════════════════════════════════════════════
//  TiempoFormateadoTests
//  Pruebas atomicas sobre la propiedad calculada
//  UsuarioListaViewModel.TiempoFormateado. Es una propiedad pura: a
//  partir de FechaCreacion calcula DateTime.Now - FechaCreacion y
//  devuelve un string formateado en cascada (años → meses → dias →
//  horas → minutos).
// ════════════════════════════════════════════════════════════════════
//
//  CODIGO ORIGINAL (ViewModels/UsuarioListaViewModel.cs:92-117):
//
//      public string TiempoFormateado
//      {
//          get
//          {
//              var t = TiempoDesdeCreacion; // DateTime.Now - FechaCreacion
//              if (t.TotalDays >= 365)
//                  return $"{(int)(t.TotalDays / 365)} año(s), {(int)(t.TotalDays % 365 / 30)} mes(es)";
//              if (t.TotalDays >= 30)
//                  return $"{(int)(t.TotalDays / 30)} mes(es), {(int)(t.TotalDays % 30)} día(s)";
//              if (t.TotalDays >= 1)
//                  return $"{(int)t.TotalDays} día(s)";
//              if (t.TotalHours >= 1)
//                  return $"{(int)t.TotalHours} hora(s)";
//              return $"{(int)t.TotalMinutes} minuto(s)";
//          }
//      }
//
//  USO EN PRODUCCION:
//      Se muestra como badge en la columna "Tiempo Registrado" del panel
//      Admin/Usuarios y en la columna correspondiente del Excel exportado
//      con ExcelService.ExportarUsuarios.
//
//  Trazabilidad: indirecta a varios RFs administrativos (RF-21 gestion
//                de usuarios, RF-36 metricas de crecimiento).

public class TiempoFormateadoTests
{
    // Helper: crea un ViewModel con la fecha de creacion dada.
    private static UsuarioListaViewModel ConFecha(DateTime fecha) =>
        new() { FechaCreacion = fecha };

    // ────────────────────────────────────────────────────────────────
    //  TEST 1 · 3 horas → "3 hora(s)"
    // ────────────────────────────────────────────────────────────────
    //  Que verifica:
    //      Cuando un usuario fue creado hace mas de 1 hora pero menos
    //      de 1 dia, el formato muestra unicamente las horas.
    //
    //  Ejemplos de uso esperados:
    //  +--------------------+---------------------+
    //  | Tiempo transcurrido| TiempoFormateado    |
    //  +--------------------+---------------------+
    //  | 3 horas            | "3 hora(s)"  <test  |
    //  | 1 hora             | "1 hora(s)"         |
    //  | 23 horas           | "23 hora(s)"        |
    //  | 45 minutos         | "45 minuto(s)"      |
    //  +--------------------+---------------------+
    //
    //  Por que importa:
    //      Verifica la cascada en el rango "horas" (entre 1 hora y 1 dia).
    //      Si la condicion fallara, mostraria "0 día(s)" para un usuario
    //      creado hace pocas horas, lo cual es confuso para el admin.
    // ────────────────────────────────────────────────────────────────
    [Fact]
    public void TiempoFormateado_CuandoEs3Horas_MuestraHoras()
    {
        // Arrange — fecha de creacion 3 horas en el pasado
        var vm = ConFecha(DateTime.Now.AddHours(-3));

        // Act
        var resultado = vm.TiempoFormateado;

        // Assert
        Assert.Equal("3 hora(s)", resultado);
    }


    // ────────────────────────────────────────────────────────────────
    //  TEST 2 · 65 dias → "2 mes(es), X día(s)"
    // ────────────────────────────────────────────────────────────────
    //  Que verifica:
    //      Cuando han pasado entre 30 dias y 365 dias, el formato
    //      combina meses y dias restantes (formato: "X mes(es), Y dia(s)").
    //
    //  Ejemplos de uso esperados:
    //  +--------------------+---------------------------+
    //  | Dias transcurridos | TiempoFormateado          |
    //  +--------------------+---------------------------+
    //  | 65                 | "2 mes(es), 5 día(s)" <test
    //  | 30                 | "1 mes(es), 0 día(s)"     |
    //  | 90                 | "3 mes(es), 0 día(s)"     |
    //  | 364                | "12 mes(es), 4 día(s)"    |
    //  +--------------------+---------------------------+
    //
    //  Por que importa:
    //      Verifica la cascada en el rango "meses" (entre 30 y 365 dias),
    //      que es el formato mas usado en el panel admin para usuarios
    //      registrados en los ultimos meses.
    //      Se usa StartsWith en lugar de Equal porque el numero exacto
    //      de dias depende ligeramente del calculo en runtime, pero el
    //      prefijo "2 mes(es)," es estable.
    // ────────────────────────────────────────────────────────────────
    [Fact]
    public void TiempoFormateado_CuandoEs2Meses_MuestraMesesYDias()
    {
        // Arrange — fecha de creacion 65 dias en el pasado
        var vm = ConFecha(DateTime.Now.AddDays(-65));

        // Act
        var resultado = vm.TiempoFormateado;

        // Assert — solo verificamos el prefijo (los dias finales pueden
        //          variar ligeramente al ejecutarse exactamente al cambio
        //          de hora; con StartsWith el test es estable).
        Assert.StartsWith("2 mes(es),", resultado);
    }
}
