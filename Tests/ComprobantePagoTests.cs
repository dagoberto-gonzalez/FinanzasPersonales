using FinanzasPersonales.Models;
using FinanzasPersonales.Services;
using FinanzasPersonales.ViewModels;
using Xunit;

namespace FinanzasPersonales.Tests;

/// <summary>
/// Control Laboral, Parte 2: anotar el comprobante de pago y compararlo con lo calculado.
/// Escenario: el comprobante 16237 de setiembre de 2026 (11 h dobles, viáticos ₡25.000).
/// </summary>
[Collection("Sesion")]
public class ComprobantePagoTests
{
    private static readonly DateTime Domingo = new(2026, 9, 20);   // período 15–28 set

    private static void Configurar(BaseDePrueba t, decimal viaticos)
    {
        SessionService.IniciarSesion(t.UsuarioId, "tester", "Normal");
        t.Db.GuardarConfiguracionLaboral(new ConfiguracionLaboral
        {
            UsuarioId           = t.UsuarioId,
            SalarioMensual      = 430_230.76m,
            JornadaSemanal      = 42.5m,
            DiasPorSemana       = 5,
            PorcentajeDeduccion = 10.83m,
            ModoPago            = "Quincenal",
            DiaPago             = 14,
            DiaPago2            = 28,
            Viaticos            = viaticos
        });
    }

    /// <summary>Registra horas dobles el domingo y cierra su período.</summary>
    private static PeriodoLaboral CerrarConDobles(BaseDePrueba t, double horasDobles)
    {
        t.Db.InsertarOActualizarRegistroDia(new RegistroDiaLaboral
        {
            UsuarioId = t.UsuarioId, Fecha = Domingo, HorasDobles = horasDobles
        });
        var (ini, fin) = t.Db.ObtenerConfiguracionLaboral(t.UsuarioId).PeriodoQueContiene(Domingo);
        t.Db.ConsolidarPeriodoLaboral(t.UsuarioId, ini, fin);
        return t.Db.ObtenerPeriodosLaborales(t.UsuarioId).Single();
    }

    // ── El cierre congela los parámetros ──

    /// <summary>
    /// Los viáticos pasaron de ₡15.000 a ₡25.000. Sin congelar los parámetros al cerrar, el
    /// aumento reescribía lo que se debió pagar en todas las quincenas anteriores.
    /// </summary>
    [Fact]
    public void SubirLosViaticosNoCambiaUnPeriodoYaCerrado()
    {
        using var t = new BaseDePrueba();
        Configurar(t, viaticos: 15_000m);
        var periodo = CerrarConDobles(t, 11);

        Configurar(t, viaticos: 25_000m);

        var cfgActual = t.Db.ObtenerConfiguracionLaboral(t.UsuarioId);
        var regs      = t.Db.ObtenerRegistrosDia(t.UsuarioId, periodo.FechaInicio, periodo.FechaFin);
        var calc      = CalculoPeriodo.De(periodo.ConfiguracionAlCierre(cfgActual), regs);

        Assert.Equal(15_000m, periodo.ViaticosAlCierre);
        Assert.Equal(15_000m, calc.Viaticos);
    }

    [Fact]
    public void ElCierreGuardaLoQueCalculaConLosRegistrosGuardados()
    {
        using var t = new BaseDePrueba();
        Configurar(t, viaticos: 25_000m);
        var periodo = CerrarConDobles(t, 11);

        // Ya no recibe cifras de fuera: calcula él mismo.
        Assert.InRange(periodo.SalarioNeto, 251_985.10m, 251_985.14m);
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    // ── El comprobante se guarda aparte ──

    [Fact]
    public void AnotarElComprobanteNoTocaLoCalculado()
    {
        using var t = new BaseDePrueba();
        Configurar(t, viaticos: 25_000m);
        var periodo = CerrarConDobles(t, 12);   // se registraron 12, se pagaron 11

        t.Db.GuardarComprobante(new ComprobantePago
        {
            PeriodoId = periodo.Id, SalarioFijo = 215_115.38m, HorasDobles = 11,
            MontoDobles = 39_437.86m, Viaticos = 25_000m, Deducciones = 27_568.12m, Notas = "N.º 16237"
        });

        var despues = t.Db.ObtenerPeriodosLaborales(t.UsuarioId).Single();
        Assert.Equal(periodo.SalarioNeto, despues.SalarioNeto);   // "Editar" lo sobrescribía

        var comp = t.Db.ObtenerComprobante(periodo.Id)!;
        Assert.Equal(11m, comp.HorasDobles);
        Assert.Equal("N.º 16237", comp.Notas);
        Assert.Equal(251_985.12m, comp.Neto);
    }

    [Fact]
    public void BorrarElPeriodoBorraSuComprobante()
    {
        using var t = new BaseDePrueba();
        Configurar(t, viaticos: 25_000m);
        var periodo = CerrarConDobles(t, 11);
        t.Db.GuardarComprobante(new ComprobantePago { PeriodoId = periodo.Id, SalarioFijo = 1 });

        t.Db.EliminarPeriodoLaboral(periodo.Id);

        Assert.Null(t.Db.ObtenerComprobante(periodo.Id));
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    // ── En pantalla ──

    private static (ControlLaboralViewModel Vm, HistorialLaboralItem Item) AbrirComprobante(BaseDePrueba t)
    {
        var vm   = new ControlLaboralViewModel(t.Db, t.UsuarioId, () => Domingo);
        var item = vm.Historial.Single(h => h.EsCierre);
        vm.AbrirComprobanteCommand.Execute(item);
        return (vm, item);
    }

    private static void CopiarSetiembre(ControlLaboralViewModel vm)
    {
        // Tal como vienen impresos, con espacio de miles y coma decimal.
        vm.CompSalarioFijo = "215 115,38";
        vm.CompHorasDobles = "11,00";
        vm.CompMontoDobles = "39 437,86";
        vm.CompViaticos    = "25 000,00";
        vm.CompDeducciones = "-27 568,12";
    }

    [Fact]
    public void ElFormularioEmpiezaVacioYNoConLoCalculado()
    {
        using var t = new BaseDePrueba();
        Configurar(t, viaticos: 25_000m);
        CerrarConDobles(t, 11);

        var (vm, _) = AbrirComprobante(t);

        Assert.True(vm.ComprobanteVisible);
        Assert.Equal("", vm.CompSalarioFijo);
        Assert.Equal("", vm.CompHorasDobles);

        // Sin nada copiado no se marca ninguna diferencia: antes salía todo en rojo.
        Assert.All(vm.Comparacion, l =>
        {
            Assert.Equal("—", l.PagadoTexto);
            Assert.Equal("", l.DiferenciaTexto);
        });

        // En cuanto se escribe algo, se compara.
        vm.CompSalarioFijo = "215 115,38";
        Assert.Equal("✓", vm.Comparacion.Single(l => l.Concepto == "Salario fijo").DiferenciaTexto);
    }

    [Fact]
    public void SiElComprobanteCoincideElHistorialDiceQueCuadra()
    {
        using var t = new BaseDePrueba();
        Configurar(t, viaticos: 25_000m);
        CerrarConDobles(t, 11);

        var (vm, _) = AbrirComprobante(t);
        CopiarSetiembre(vm);

        Assert.All(vm.Comparacion, l => Assert.True(l.Cuadra, l.Concepto));

        vm.GuardarComprobanteCommand.Execute(null);

        Assert.False(vm.ComprobanteVisible);
        Assert.Equal("✓ Cuadra", vm.Historial.Single(h => h.EsCierre).Comprobante);
    }

    [Fact]
    public void SiPaganMenosHorasElHistorialLoSenala()
    {
        using var t = new BaseDePrueba();
        Configurar(t, viaticos: 25_000m);
        CerrarConDobles(t, 12);

        var (vm, _) = AbrirComprobante(t);
        CopiarSetiembre(vm);

        var dobles = vm.Comparacion.Single(l => l.Concepto == "Horas dobles");
        Assert.Equal(-1m, dobles.DiferenciaHoras);

        vm.GuardarComprobanteCommand.Execute(null);

        Assert.StartsWith("⚠", vm.Historial.Single(h => h.EsCierre).Comprobante);
    }

    [Fact]
    public void UnImporteIlegibleNoSeGuarda()
    {
        using var t = new BaseDePrueba();
        Configurar(t, viaticos: 25_000m);
        var periodo = CerrarConDobles(t, 11);

        var (vm, _) = AbrirComprobante(t);
        CopiarSetiembre(vm);
        vm.CompSalarioFijo = "1.200.00";   // ambiguo: Dinero lo rechaza

        vm.GuardarComprobanteCommand.Execute(null);

        Assert.Contains("salario fijo", vm.CompError);
        Assert.True(vm.ComprobanteVisible);
        Assert.Null(t.Db.ObtenerComprobante(periodo.Id));
    }
}
