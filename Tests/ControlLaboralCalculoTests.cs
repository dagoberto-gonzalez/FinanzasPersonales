using FinanzasPersonales.Models;
using FinanzasPersonales.Services;
using FinanzasPersonales.ViewModels;
using Xunit;

namespace FinanzasPersonales.Tests;

/// <summary>
/// El cálculo del período laboral, con la configuración real de Dagoberto: quincenal con cortes
/// el 14 y el 28, jornada de 42,5 h en 5 días, deducción del 10,83 %.
/// </summary>
[Collection("Sesion")]
public class ControlLaboralCalculoTests
{
    private const decimal SalHora = 2336m;

    private static ControlLaboralViewModel Abrir(BaseDePrueba t, decimal viaticos = 0m,
                                                 Func<DateTime>? reloj = null)
    {
        SessionService.IniciarSesion(t.UsuarioId, "tester", "Normal");
        t.Db.GuardarConfiguracionLaboral(new ConfiguracionLaboral
        {
            UsuarioId           = t.UsuarioId,
            SalarioPorHora      = SalHora,
            JornadaSemanal      = 42.5m,
            DiasPorSemana       = 5,
            PorcentajeDeduccion = 10.83m,
            ModoPago            = "Quincenal",
            DiaPago             = 14,
            DiaPago2            = 28,
            Viaticos            = viaticos
        });
        return new ControlLaboralViewModel(t.Db, t.UsuarioId, reloj);
    }

    /// <summary>Un día dentro del período que la pantalla muestra al abrirse.</summary>
    private static DateTime DiaDelPeriodoActual(BaseDePrueba t)
    {
        var cfg = t.Db.ObtenerConfiguracionLaboral(t.UsuarioId);
        return cfg.PeriodoQueContiene(DateTime.Today).Inicio;
    }

    // ── El fallo de fondo: base y horas del mismo rango ──

    /// <summary>
    /// Antes la base era la de una quincena pero las horas se sumaban del mes natural: una hora
    /// extra de la quincena anterior engordaba el pago de ésta.
    /// </summary>
    [Fact]
    public void LasHorasDeOtroPeriodoNoEntranEnEste()
    {
        using var t   = new BaseDePrueba();
        var       cfg = t.Db.ObtenerConfiguracionLaboral(t.UsuarioId);

        SessionService.IniciarSesion(t.UsuarioId, "tester", "Normal");
        t.Db.GuardarConfiguracionLaboral(new ConfiguracionLaboral
        {
            UsuarioId = t.UsuarioId, SalarioPorHora = SalHora, JornadaSemanal = 42.5m,
            DiasPorSemana = 5, PorcentajeDeduccion = 10.83m,
            ModoPago = "Quincenal", DiaPago = 14, DiaPago2 = 28
        });
        cfg = t.Db.ObtenerConfiguracionLaboral(t.UsuarioId);

        var (inicio, _) = cfg.PeriodoQueContiene(DateTime.Today);

        // 8 horas extra el día ANTERIOR al inicio del período: son de la quincena pasada.
        t.Db.InsertarOActualizarRegistroDia(new RegistroDiaLaboral
        {
            UsuarioId = t.UsuarioId, Fecha = inicio.AddDays(-1), HorasExtraDiurnas = 8
        });

        var vm = new ControlLaboralViewModel(t.Db, t.UsuarioId);

        Assert.Empty(vm.Registros);                       // no se muestran
        Assert.Contains("0", vm.ResumenExtraD);           // ni suman
    }

    [Fact]
    public void LasHorasDelPeriodoSiEntran()
    {
        using var t  = new BaseDePrueba();
        var       vm = Abrir(t);

        t.Db.InsertarOActualizarRegistroDia(new RegistroDiaLaboral
        {
            UsuarioId = t.UsuarioId, Fecha = DiaDelPeriodoActual(t), HorasExtraDiurnas = 8
        });
        vm.Cargar();

        Assert.Single(vm.Registros);
        // 8 h × 2336 × 1,5 = 28.032
        Assert.Contains("28.032", vm.ResumenExtraD);
    }

    // ── El divisor de días ──

    /// <summary>
    /// Con el 6 clavado, la hora diaria salía 7,08 en vez de 8,5 y cada feriado se pagaba unos
    /// ₡3.300 por debajo.
    /// </summary>
    [Fact]
    public void UnFeriadoSePagaSegunLosDiasQueSeTrabajan()
    {
        using var t  = new BaseDePrueba();
        var       vm = Abrir(t);

        t.Db.InsertarOActualizarRegistroDia(new RegistroDiaLaboral
        {
            UsuarioId = t.UsuarioId, Fecha = DiaDelPeriodoActual(t), EsFeriado = true
        });
        vm.Cargar();

        // 8,5 h × 2336 = 19.856. Con el divisor viejo daba 16.547.
        Assert.Contains("19.856", vm.ResumenFeriados);
        Assert.DoesNotContain("16.547", vm.ResumenFeriados);
    }

    // ── Ausencias por horas ──

    [Fact]
    public void SePuedeFaltarSoloUnasHoras()
    {
        using var t  = new BaseDePrueba();
        var       vm = Abrir(t);

        t.Db.InsertarOActualizarRegistroDia(new RegistroDiaLaboral
        {
            UsuarioId = t.UsuarioId, Fecha = DiaDelPeriodoActual(t), HorasAusencia = 3
        });
        vm.Cargar();

        // 3 h × 2336 = 7.008. Antes sólo existía el día completo.
        Assert.Contains("7.008", vm.ResumenDescAusencias);
        Assert.Contains("3", vm.ResumenDescAusencias);
    }

    [Fact]
    public void UnaAusenciaConGoceNoDescuenta()
    {
        using var t  = new BaseDePrueba();
        var       vm = Abrir(t);

        t.Db.InsertarOActualizarRegistroDia(new RegistroDiaLaboral
        {
            UsuarioId        = t.UsuarioId, Fecha = DiaDelPeriodoActual(t),
            HorasAusencia    = 8.5, TieneGoceSalario = true
        });
        vm.Cargar();

        Assert.Contains("0", vm.ResumenDescAusencias);
        Assert.Contains("8", vm.ResumenAusenciaGoce);
    }

    // ── La deducción es configurable ──

    [Fact]
    public void LaDeduccionUsaElPorcentajeConfigurado()
    {
        using var t  = new BaseDePrueba();
        var       vm = Abrir(t);

        Assert.Contains("10,83%", vm.ResumenDeduccionCCSS);   // ya no 10,67 clavado
    }

    [Fact]
    public void CambiarElPorcentajeCambiaElCalculo()
    {
        using var t  = new BaseDePrueba();
        var       vm = Abrir(t);

        var cfg = t.Db.ObtenerConfiguracionLaboral(t.UsuarioId);
        cfg.PorcentajeDeduccion = 12m;
        t.Db.GuardarConfiguracionLaboral(cfg);
        vm.Cargar();

        Assert.Contains("12%", vm.ResumenDeduccionCCSS);
    }

    // ── Navegación entre períodos ──

    [Fact]
    public void SePuedeConsultarElPeriodoAnterior()
    {
        using var t  = new BaseDePrueba();
        var       vm = Abrir(t);
        var       cfg = t.Db.ObtenerConfiguracionLaboral(t.UsuarioId);

        var (inicioActual, _) = cfg.PeriodoQueContiene(DateTime.Today);
        var (inicioAnterior, _) = cfg.PeriodoAnterior(inicioActual);

        // 4 horas extra en el período anterior, que antes era imposible consultar.
        t.Db.InsertarOActualizarRegistroDia(new RegistroDiaLaboral
        {
            UsuarioId = t.UsuarioId, Fecha = inicioAnterior, HorasExtraDiurnas = 4
        });

        Assert.True(vm.EsPeriodoActual);
        Assert.Empty(vm.Registros);

        vm.PeriodoAnteriorCommand.Execute(null);

        Assert.False(vm.EsPeriodoActual);
        Assert.Single(vm.Registros);

        vm.PeriodoActualCommand.Execute(null);
        Assert.True(vm.EsPeriodoActual);
    }

    // La vista se cachea y sólo llama a Cargar() al volver a mostrarse, así que "hoy" tiene que
    // poder avanzar entre una carga y la siguiente. Cortes 14 y 28 → períodos 29–14 y 15–28.

    [Fact]
    public void AlCambiarDeQuincenaLaPantallaPasaALaNueva()
    {
        using var t   = new BaseDePrueba();
        var       hoy = new DateTime(2026, 10, 10);
        var       vm  = Abrir(t, reloj: () => hoy);
        Assert.StartsWith("29/09/2026", vm.PeriodoRangoTexto);

        hoy = new DateTime(2026, 10, 16);
        vm.Cargar();

        Assert.StartsWith("15/10/2026", vm.PeriodoRangoTexto);
        Assert.True(vm.EsPeriodoActual);
    }

    [Fact]
    public void SiSeEstaMirandoOtroPeriodoRecargarNoLoMueve()
    {
        using var t   = new BaseDePrueba();
        var       hoy = new DateTime(2026, 10, 10);
        var       vm  = Abrir(t, reloj: () => hoy);

        vm.PeriodoAnteriorCommand.Execute(null);
        vm.Cargar();

        Assert.StartsWith("15/09/2026", vm.PeriodoRangoTexto);
        Assert.False(vm.EsPeriodoActual);
    }

    [Fact]
    public void AlCambiarElModoDePagoSeMuestraUnPeriodoDelModoNuevo()
    {
        using var t   = new BaseDePrueba();
        var       hoy = new DateTime(2026, 10, 10);
        var       vm  = Abrir(t, reloj: () => hoy);

        vm.ConfigModoPago = "Mensual";
        vm.ConfigDiaPago  = "30";
        vm.GuardarConfigCommand.Execute(null);

        Assert.True(vm.EsPeriodoActual);
    }

    [Fact]
    public void LaVistaSeEnteraDeLosCambiosDePeriodo()
    {
        using var t   = new BaseDePrueba();
        var       hoy = new DateTime(2026, 10, 10);
        var       vm  = Abrir(t, reloj: () => hoy);
        var notificadas = new List<string?>();
        vm.PropertyChanged += (_, e) => notificadas.Add(e.PropertyName);

        // Lo que enlaza el XAML, no las propiedades de las que derivan.
        vm.PeriodoAnteriorCommand.Execute(null);
        Assert.Contains(nameof(vm.VolverAlActualVisibility), notificadas);
        Assert.Contains(nameof(vm.PeriodoRangoTexto), notificadas);

        notificadas.Clear();
        vm.PeriodoActualCommand.Execute(null);
        hoy = new DateTime(2026, 10, 16);
        vm.Cargar();
        Assert.Contains(nameof(vm.VolverAlActualVisibility), notificadas);
        Assert.Contains(nameof(vm.PeriodoRangoTexto), notificadas);
        Assert.Contains(nameof(vm.HorasDiariasTexto), notificadas);
    }

    // ── Cierre del período ──

    [Fact]
    public void SePuedenCerrarDosQuincenasDelMismoMes()
    {
        using var t   = new BaseDePrueba();
        Abrir(t);
        var cfg = t.Db.ObtenerConfiguracionLaboral(t.UsuarioId);

        var primera = cfg.PeriodoQueContiene(new DateTime(2026, 9, 20));   // 15–28 set
        var segunda = cfg.PeriodoSiguiente(primera.Fin);                   // 29 set–14 oct

        t.Db.ConsolidarPeriodoLaboral(t.UsuarioId, primera.Inicio, primera.Fin, 200000, 21660, 178340);
        t.Db.ConsolidarPeriodoLaboral(t.UsuarioId, segunda.Inicio, segunda.Fin, 240000, 25992, 214008);

        // Con la clave (Anio, Mes) la segunda sobrescribía a la primera: el modo Quincenal
        // que ofrecía la interfaz no se podía representar en la tabla.
        var periodos = t.Db.ObtenerPeriodosLaborales(t.UsuarioId);
        Assert.Equal(2, periodos.Count);
        // Las dos arrancan en setiembre, aunque la segunda termine en octubre.
        Assert.Equal(2, periodos.Count(p => p.FechaInicio.Month == 9));
        Assert.Contains(periodos, p => p.FechaFin.Month == 10);
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    [Fact]
    public void ElPeriodoGuardaSuRangoDeFechas()
    {
        using var t = new BaseDePrueba();
        Abrir(t);

        t.Db.ConsolidarPeriodoLaboral(t.UsuarioId,
            new DateTime(2026, 9, 15), new DateTime(2026, 9, 28), 200000, 21660, 178340);

        var p = t.Db.ObtenerPeriodosLaborales(t.UsuarioId).Single();
        Assert.Equal(new DateTime(2026, 9, 15), p.FechaInicio);
        Assert.Equal(new DateTime(2026, 9, 28), p.FechaFin);
        Assert.Equal(14, p.Dias);
    }
}
