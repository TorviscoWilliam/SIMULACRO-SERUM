using SimulacroExamen.ViewModels;
using Xunit;

namespace SimulacroExamen.Tests.Tests;

// ════════════════════════════════════════════════════════════════════
//  PorcentajeErrorTests
//  Pruebas atomicas sobre la propiedad calculada
//  EstadisticaPreguntaVM.PorcentajeError. Es una propiedad pura: solo
//  hace una division aritmetica con proteccion contra division por cero.
// ════════════════════════════════════════════════════════════════════
//
//  CODIGO ORIGINAL (ViewModels/EstadisticaPreguntaVM.cs:11):
//
//      public double PorcentajeError =>
//          TotalVeces > 0 ? (double)Incorrectas / TotalVeces * 100 : 0;
//
//  USO EN PRODUCCION:
//      El panel de admin "Estadisticas de preguntas" usa esta propiedad
//      para identificar las 100 preguntas con mayor tasa de error.
//      Las preguntas con PorcentajeError >= 70 se resaltan en rojo en
//      la vista para que el admin las revise.
//
//  Trazabilidad: RF-32 (top 100 preguntas con mayor tasa de error +
//                resaltado >= 70%).

public class PorcentajeErrorTests
{
    // ────────────────────────────────────────────────────────────────
    //  TEST 1 · Total cero → 0 (defensa division por cero)
    // ────────────────────────────────────────────────────────────────
    //  Que verifica:
    //      Cuando una pregunta nunca se ha respondido (TotalVeces = 0),
    //      el porcentaje devuelve 0 SIN lanzar DivideByZeroException
    //      ni devolver NaN.
    //
    //  Ejemplos de uso esperados:
    //  +------------+-------------+----------------+
    //  | TotalVeces | Incorrectas | PorcentajeError|
    //  +------------+-------------+----------------+
    //  | 0          | 0           | 0  <test       |
    //  | 0          | 5           | 0 (defensa)    |
    //  +------------+-------------+----------------+
    //
    //  Por que importa:
    //      Cuando se agrega una pregunta nueva al banco, aun no ha
    //      sido respondida por nadie. Sin esta defensa, el panel
    //      mostraria "NaN%" o crashearia al cargar la vista.
    // ────────────────────────────────────────────────────────────────
    [Fact]
    public void PorcentajeError_CuandoTotalVecesEsCero_RetornaCero()
    {
        // Arrange
        var vm = new EstadisticaPreguntaVM { TotalVeces = 0, Incorrectas = 0 };

        // Act
        var resultado = vm.PorcentajeError;

        // Assert
        Assert.Equal(0, resultado);
    }


    // ────────────────────────────────────────────────────────────────
    //  TEST 2 · Todos correctos → 0
    // ────────────────────────────────────────────────────────────────
    //  Que verifica:
    //      Si una pregunta se ha respondido N veces y siempre
    //      correctamente (Incorrectas = 0), el porcentaje de error es 0.
    //
    //  Ejemplos de uso esperados:
    //  +------------+-------------+----------------+
    //  | TotalVeces | Incorrectas | PorcentajeError|
    //  +------------+-------------+----------------+
    //  | 10         | 0           | 0  <test       |
    //  | 100        | 0           | 0              |
    //  +------------+-------------+----------------+
    //
    //  Por que importa:
    //      Esta es la pregunta "ideal" — todos la responden bien.
    //      No debe aparecer destacada en rojo. El test confirma que
    //      el calculo no exagera el error cuando es 0%.
    // ────────────────────────────────────────────────────────────────
    [Fact]
    public void PorcentajeError_CuandoTodosCorrectos_RetornaCero()
    {
        // Arrange
        var vm = new EstadisticaPreguntaVM { TotalVeces = 10, Incorrectas = 0 };

        // Act
        var resultado = vm.PorcentajeError;

        // Assert
        Assert.Equal(0, resultado);
    }


    // ────────────────────────────────────────────────────────────────
    //  TEST 3 · Todos incorrectos → 100 (caso limite superior)
    // ────────────────────────────────────────────────────────────────
    //  Que verifica:
    //      Cuando todas las respuestas fueron incorrectas
    //      (Incorrectas == TotalVeces), el porcentaje es exactamente 100.
    //
    //  Ejemplos de uso esperados:
    //  +------------+-------------+----------------+
    //  | TotalVeces | Incorrectas | PorcentajeError|
    //  +------------+-------------+----------------+
    //  | 5          | 5           | 100  <test     |
    //  | 50         | 50          | 100            |
    //  +------------+-------------+----------------+
    //
    //  Por que importa:
    //      Esta es la pregunta CRITICA: nadie la responde bien.
    //      Probablemente esta mal redactada o tiene la respuesta
    //      correcta marcada incorrectamente. Debe aparecer destacada
    //      en rojo (PorcentajeError >= 70%) para revision urgente.
    // ────────────────────────────────────────────────────────────────
    [Fact]
    public void PorcentajeError_CuandoTodosIncorrectos_RetornaCien()
    {
        // Arrange
        var vm = new EstadisticaPreguntaVM { TotalVeces = 5, Incorrectas = 5 };

        // Act
        var resultado = vm.PorcentajeError;

        // Assert
        Assert.Equal(100, resultado);
    }


    // ────────────────────────────────────────────────────────────────
    //  TEST 4 · Mitad incorrectos → 50
    // ────────────────────────────────────────────────────────────────
    //  Que verifica:
    //      Caso central: la mitad de las respuestas son incorrectas →
    //      el porcentaje es exactamente 50.
    //
    //  Ejemplos de uso esperados:
    //  +------------+-------------+----------------+
    //  | TotalVeces | Incorrectas | PorcentajeError|
    //  +------------+-------------+----------------+
    //  | 10         | 5           | 50  <test      |
    //  | 100        | 50          | 50             |
    //  | 4          | 2           | 50             |
    //  +------------+-------------+----------------+
    //
    //  Por que importa:
    //      Verifica que la formula aritmetica funciona correctamente
    //      con valores intermedios. Una pregunta con 50% de error no
    //      llega al umbral del 70% y no se destaca en rojo, pero es
    //      candidata a revision moderada.
    // ────────────────────────────────────────────────────────────────
    [Fact]
    public void PorcentajeError_MitadIncorrectos_RetornaCincuenta()
    {
        // Arrange
        var vm = new EstadisticaPreguntaVM { TotalVeces = 10, Incorrectas = 5 };

        // Act
        var resultado = vm.PorcentajeError;

        // Assert
        Assert.Equal(50, resultado);
    }


    // ────────────────────────────────────────────────────────────────
    //  TEST 5 · Uno de diez → 10
    // ────────────────────────────────────────────────────────────────
    //  Que verifica:
    //      Caso pequeño: 1 incorrecta de 10 respuestas → 10%.
    //
    //  Ejemplos de uso esperados:
    //  +------------+-------------+----------------+
    //  | TotalVeces | Incorrectas | PorcentajeError|
    //  +------------+-------------+----------------+
    //  | 10         | 1           | 10  <test      |
    //  | 100        | 10          | 10             |
    //  +------------+-------------+----------------+
    //
    //  Por que importa:
    //      Verifica la precision de la division con valores pequeños.
    //      Confirma que (1/10)*100 = 10 (no 10.0001 ni 9.9999).
    //      Esto es importante porque la vista compara con el umbral
    //      del 70% exactamente.
    // ────────────────────────────────────────────────────────────────
    [Fact]
    public void PorcentajeError_UnoDeDiez_RetornaDiez()
    {
        // Arrange
        var vm = new EstadisticaPreguntaVM { TotalVeces = 10, Incorrectas = 1 };

        // Act
        var resultado = vm.PorcentajeError;

        // Assert
        Assert.Equal(10, resultado);
    }
}
