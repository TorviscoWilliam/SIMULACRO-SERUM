using SimulacroExamen.Services;
using SimulacroExamen.ViewModels;
using Xunit;

namespace SimulacroExamen.Tests.Tests;

// ════════════════════════════════════════════════════════════════════
//  CalculosNegocioTests
//  Agrupa tres tests atomicos sobre funciones criticas de negocio que
//  no estaban cubiertas por la suite original. Cada test prueba UNA
//  funcion pura en aislamiento (sin BD, sin HTTP, sin archivos).
//
//  Funciones cubiertas:
//   1. UsuarioListaViewModel.SuscripcionVencida (RF-30)
//   2. MesFinancieroVM.TasaConversion           (RF-35)
//   3. ParametrosGlobalesDefaults               (RF-04)
// ════════════════════════════════════════════════════════════════════
public class CalculosNegocioTests
{
    // ────────────────────────────────────────────────────────────────
    //  TEST 1 · SuscripcionVencida
    // ────────────────────────────────────────────────────────────────
    //  Funcion bajo prueba:
    //      UsuarioListaViewModel.SuscripcionVencida
    //      (ViewModels/UsuarioListaViewModel.cs:72)
    //
    //  Codigo original:
    //      public bool SuscripcionVencida =>
    //          !EsTrial
    //          && FechaVencimiento.HasValue
    //          && FechaVencimiento.Value < DateTime.Now;
    //
    //  Que hace:
    //      Devuelve TRUE solo cuando se cumplen las TRES condiciones:
    //        a) el usuario NO es trial,
    //        b) tiene fecha de vencimiento asignada,
    //        c) esa fecha ya paso.
    //      Cualquier otro caso (es trial, sin fecha, fecha futura) -> FALSE.
    //
    //  Ejemplos de uso esperados:
    //  +-----------+--------------------+-------------+
    //  | EsTrial   | FechaVencimiento   | Resultado   |
    //  +-----------+--------------------+-------------+
    //  | false     | ayer               | TRUE  <test |
    //  | true      | ayer               | false       |
    //  | false     | manana             | false       |
    //  | false     | null               | false       |
    //  +-----------+--------------------+-------------+
    //
    //  Por que importa:
    //      Controla el bloqueo de acceso a simulacros cuando la
    //      suscripcion vencio. Un bug aqui dejaria entrar usuarios
    //      con plan expirado, o bloquearia a quienes pagan al dia.
    //
    //  Trazabilidad: RF-30 (limite de intentos / control de acceso).
    // ────────────────────────────────────────────────────────────────
    [Fact]
    public void SuscripcionVencida_ConFechaPasadaYNoTrial_RetornaTrue()
    {
        // Arrange — usuario con plan pagado y fecha de ayer
        var vm = new UsuarioListaViewModel
        {
            EsTrial          = false,
            FechaVencimiento = DateTime.Now.AddDays(-1)
        };

        // Act — leer la propiedad calculada
        var resultado = vm.SuscripcionVencida;

        // Assert — la suscripcion debe figurar como vencida
        Assert.True(resultado);
    }


    // ────────────────────────────────────────────────────────────────
    //  TEST 2 · TasaConversion mensual
    // ────────────────────────────────────────────────────────────────
    //  Funcion bajo prueba:
    //      MesFinancieroVM.TasaConversion
    //      (ViewModels/PanelFinancieroViewModel.cs:35)
    //
    //  Codigo original:
    //      public double TasaConversion => RegistrosNuevos > 0
    //          ? Math.Round((double)UsuariosPago / RegistrosNuevos * 100, 1)
    //          : 0;
    //
    //  Que hace:
    //      Calcula que porcentaje de los usuarios registrados en el mes
    //      pasaron del modo trial a un plan pagado. Se redondea a 1 decimal.
    //      Si en el mes no hubo registros nuevos (division por cero),
    //      devuelve 0 sin lanzar excepcion.
    //
    //  Formula:
    //      (UsuariosPago / RegistrosNuevos) * 100
    //
    //  Ejemplos de uso esperados:
    //  +-----------------+--------------+----------------+
    //  | RegistrosNuevos | UsuariosPago | TasaConversion |
    //  +-----------------+--------------+----------------+
    //  | 40              | 10           | 25.0  <test    |
    //  | 100             | 20           | 20.0           |
    //  | 3               | 1            | 33.3           |
    //  | 0               | 0            | 0 (defensa)    |
    //  +-----------------+--------------+----------------+
    //
    //  Por que importa:
    //      Es la metrica clave del panel financiero del SuperAdmin.
    //      Con esa cifra decide si subir/bajar precios o lanzar promos.
    //      Un error en la formula falsearia toda la estrategia comercial.
    //
    //  Trazabilidad: RF-35 (panel financiero, tasa de conversion trial -> pago).
    // ────────────────────────────────────────────────────────────────
    [Fact]
    public void TasaConversion_Con10PagosDe40Registros_Retorna25()
    {
        // Arrange — mes con 40 registros nuevos y 10 que pagaron
        var mes = new MesFinancieroVM
        {
            RegistrosNuevos = 40,
            UsuariosPago    = 10
        };

        // Act — leer la propiedad calculada
        var resultado = mes.TasaConversion;

        // Assert — (10/40)*100 = 25.0
        Assert.Equal(25.0, resultado);
    }


    // ────────────────────────────────────────────────────────────────
    //  TEST 3 · ParametrosGlobalesDefaults.TiempoInactividadMinutos
    // ────────────────────────────────────────────────────────────────
    //  Funcion bajo prueba:
    //      ParametrosGlobalesDefaults.TiempoInactividadMinutos
    //      (Services/ParametrosGlobalesDefaults.cs:6)
    //
    //  Codigo original:
    //      public static class ParametrosGlobalesDefaults
    //      {
    //          public const int TiempoMaximoSimulacroMinutos = 120;
    //          public const int TiempoInactividadMinutos     = 30;
    //          public const int UmbralAprobacionPorcentaje   = 70;
    //          public const int UmbralRegularPorcentaje      = 50;
    //      }
    //
    //  Que hace:
    //      Define las constantes que el sistema usa cuando el SuperAdmin
    //      no ha guardado una configuracion personalizada en la BD.
    //      La cookie de autenticacion en Program.cs lee este default para
    //      cerrar la sesion tras 30 minutos de inactividad.
    //
    //  El RF-04 del PDF EXIGE 30 minutos:
    //      "El sistema debe cerrar la sesion del usuario automaticamente
    //       tras 30 minutos de inactividad."
    //
    //  Ejemplos / matriz de constantes:
    //  +---------------------------------+---------+-------------+
    //  | Constante                       | Valor   | RF asociado |
    //  +---------------------------------+---------+-------------+
    //  | TiempoInactividadMinutos        | 30 <test | RF-04       |
    //  | TiempoMaximoSimulacroMinutos    | 120      | RF-10/24    |
    //  | UmbralAprobacionPorcentaje      | 70       | RF-32       |
    //  | UmbralRegularPorcentaje         | 50       | RF-32       |
    //  +---------------------------------+---------+-------------+
    //
    //  Por que importa:
    //      Este test funciona como GUARDIAN DE REGRESION.
    //      Si alguien (refactor o tipeo) cambia 30 por otro numero,
    //      el RF-04 se rompe silenciosamente: el usuario seria
    //      desconectado antes/despues de lo que pide la especificacion.
    //      Este test detecta el cambio al instante.
    //
    //  Trazabilidad: RF-04 (logout 30 min de inactividad).
    // ────────────────────────────────────────────────────────────────
    [Fact]
    public void Defaults_TiempoInactividad_EsTreintaMinutos()
    {
        // Arrange & Act — leer la constante
        var valor = ParametrosGlobalesDefaults.TiempoInactividadMinutos;

        // Assert — el RF-04 exige exactamente 30 minutos
        Assert.Equal(30, valor);
    }
}
