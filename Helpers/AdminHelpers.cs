using SimulacroExamen.Models;

namespace SimulacroExamen.Helpers;

public static class AdminHelpers
{
    public static string ValidarColorHex(string? valor, string porDefecto) =>
        !string.IsNullOrWhiteSpace(valor) &&
        System.Text.RegularExpressions.Regex.IsMatch(valor.Trim(), @"^#[0-9A-Fa-f]{3}([0-9A-Fa-f]{3})?$")
            ? valor.Trim()
            : porDefecto;

    public static List<CaracteristicaPlan> ParsearCaracteristicas(string texto) =>
        texto.Split('\n', StringSplitOptions.RemoveEmptyEntries)
             .Select(l => l.Trim())
             .Where(l => !string.IsNullOrWhiteSpace(l))
             .Select((t, i) => new CaracteristicaPlan { Texto = t, Orden = i })
             .ToList();
}
