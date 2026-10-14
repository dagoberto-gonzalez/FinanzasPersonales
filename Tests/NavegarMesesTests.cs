using FinanzasPersonales.Data;
using FinanzasPersonales.Models;
using FinanzasPersonales.ViewModels;
using Xunit;

namespace FinanzasPersonales.Tests;

/// <summary>
/// Navegar entre meses en Gastos e Ingresos Fijos. Antes las dos pantallas estaban clavadas al
/// mes actual. Para que mirar atrás tenga sentido, cada fijo existe desde el mes de su alta.
/// </summary>
public class NavegarMesesTests
{
    private static readonly DateTime Hoy      = DateTime.Today;
    private static readonly DateTime EsteMes  = new(Hoy.Year, Hoy.Month, 1);
    private static readonly DateTime MesPasado = EsteMes.AddMonths(-1);

    private static List<GastoFijo> Gastos(BaseDePrueba t, DateTime mes) =>
        t.Db.ObtenerGastosFijosConEstado(mes.Year, mes.Month, t.UsuarioId);

    // ── Desde cuándo existe un fijo ──

    /// <summary>Sin esto, un gasto creado hoy salía pendiente en todos los meses anteriores.</summary>
    [Fact]
    public void UnFijoCreadoHoyNoApareceEnMesesAnteriores()
    {
        using var t = new BaseDePrueba();
        t.Db.InsertarGastoFijo(new GastoFijo { Nombre = "Netflix", Monto = 6000, DiaVencimiento = 5 }, t.UsuarioId);

        Assert.Single(Gastos(t, EsteMes));
        Assert.Single(Gastos(t, EsteMes.AddMonths(1)));
        Assert.Empty(Gastos(t, MesPasado));
        Assert.Equal(0, t.Escalar<long>(
            $"SELECT COUNT(*) FROM PagosMensuales WHERE Anio={MesPasado.Year} AND Mes={MesPasado.Month}"));
    }

    [Fact]
    public void UnFijoConInicioAnteriorApareceDesdeEseMes()
    {
        using var t = new BaseDePrueba();
        t.Db.InsertarIngresoFijo(new IngresoFijo
        {
            Nombre = "Sueldo", Monto = 600_000, DiaIngreso = 15, FechaInicio = MesPasado
        }, t.UsuarioId);

        Assert.Single(t.Db.ObtenerIngresosFijosConEstado(MesPasado.Year, MesPasado.Month, t.UsuarioId));
        Assert.Empty(t.Db.ObtenerIngresosFijosConEstado(MesPasado.AddMonths(-1).Year, MesPasado.AddMonths(-1).Month, t.UsuarioId));
    }

    /// <summary>
    /// Una base anterior no guardaba el alta: se toma el primer mes con registro en
    /// PagosMensuales, que la pantalla crea en cuanto se abre.
    /// </summary>
    [Fact]
    public void AlMigrarElInicioEsElPrimerMesRegistrado()
    {
        using var t = new BaseDePrueba();
        var hace2 = EsteMes.AddMonths(-2);
        t.Db.InsertarGastoFijo(new GastoFijo { Nombre = "Internet", Monto = 25_000, DiaVencimiento = 10, FechaInicio = hace2 }, t.UsuarioId);
        Gastos(t, hace2);   // la pantalla abierta aquel mes creó su registro
        t.Sql("ALTER TABLE GastosFijos DROP COLUMN FechaInicio");   // como estaba antes

        var db = new AppDatabase(t.Ruta);

        var id = t.Escalar<long>("SELECT Id FROM GastosFijos");
        Assert.Equal(hace2, db.ObtenerGastoFijo((int)id, t.UsuarioId)!.FechaInicio);
    }

    // ── Estado según el mes ──

    [Fact]
    public void EnUnMesPasadoLoNoPagadoEstaVencidoYNoUrgente()
    {
        using var t = new BaseDePrueba();
        t.Db.InsertarGastoFijo(new GastoFijo { Nombre = "Agua", Monto = 9000, DiaVencimiento = 28, FechaInicio = MesPasado }, t.UsuarioId);

        var fila = Gastos(t, MesPasado).Single();

        Assert.True (fila.EsVencido);
        Assert.False(fila.EsUrgente);
    }

    [Fact]
    public void EnUnMesFuturoNadaEstaVencidoNiUrgente()
    {
        using var t = new BaseDePrueba();
        t.Db.InsertarGastoFijo(new GastoFijo { Nombre = "Agua", Monto = 9000, DiaVencimiento = 1 }, t.UsuarioId);

        var fila = Gastos(t, EsteMes.AddMonths(2)).Single();   // 2: el día 1 del siguiente puede caer a 3 días

        Assert.False(fila.EsVencido);
        Assert.False(fila.EsUrgente);
    }

    /// <summary>El pago olvidado del mes pasado se registra con fecha de ese mes, no de hoy.</summary>
    [Fact]
    public void PagarUnMesPasadoCreaLaTransaccionEnEseMes()
    {
        using var t = new BaseDePrueba();
        t.Db.InsertarGastoFijo(new GastoFijo { Nombre = "Luz", Monto = 15_000, DiaVencimiento = 20, FechaInicio = MesPasado }, t.UsuarioId);

        var fila = Gastos(t, MesPasado).Single();
        t.Db.MarcarGastoFijoPagado(fila.PagoMensualId, true, "Efectivo", t.UsuarioId);

        var tx = t.Db.ObtenerTransacciones(t.UsuarioId).Single();
        Assert.Equal(MesPasado.Year,  tx.Fecha.Year);
        Assert.Equal(MesPasado.Month, tx.Fecha.Month);
        Assert.True (Gastos(t, MesPasado).Single().PagadoEsteMes);
        Assert.False(Gastos(t, EsteMes).Single().PagadoEsteMes);
    }

    // ── Pantalla ──

    [Fact]
    public void LaPantallaNavegaYVuelveAlMesActual()
    {
        using var t = new BaseDePrueba();
        t.Db.InsertarGastoFijo(new GastoFijo { Nombre = "Luz", Monto = 15_000, DiaVencimiento = 20, FechaInicio = MesPasado }, t.UsuarioId);
        var vm = new GastosFijosViewModel(t.Db, t.UsuarioId);
        Assert.StartsWith("Mes actual", vm.MesAnioTexto);

        vm.MesAnteriorCommand.Execute(null);

        Assert.Equal(MesPasado.Month, vm.Mes);
        Assert.DoesNotContain("Mes actual", vm.MesAnioTexto);
        Assert.Equal(System.Windows.Visibility.Visible, vm.VolverAlActualVisibility);

        // Marcar el pago olvidado desde la pantalla del mes pasado.
        vm.TogglePagoCommand.Execute(vm.GastosFijos.Single());
        Assert.True(vm.GastosFijos.Single().PagadoEsteMes);

        // Recargar (volver a la sección) no la mueve del mes que se mira.
        vm.Actualizar();
        Assert.Equal(MesPasado.Month, vm.Mes);

        vm.MesActualCommand.Execute(null);
        Assert.Equal(EsteMes.Month, vm.Mes);
        Assert.False(vm.GastosFijos.Single().PagadoEsteMes);
        Assert.Equal(System.Windows.Visibility.Collapsed, vm.VolverAlActualVisibility);
    }

    /// <summary>Dar de alta mirando un mes pasado: el fijo existe desde ese mes.</summary>
    [Fact]
    public void UnFijoCreadoMirandoUnMesPasadoEmpiezaEnEseMes()
    {
        using var t = new BaseDePrueba();
        var vm = new IngresosFijosViewModel(t.Db, t.UsuarioId);
        vm.MesAnteriorCommand.Execute(null);

        vm.Nombre = "Alquiler cobrado"; vm.Monto = "150.000"; vm.DiaIngreso = "5";
        vm.GuardarCommand.Execute(null);

        Assert.Single(vm.IngresosFijos);
        var id = vm.IngresosFijos.Single().Id;
        Assert.Equal(MesPasado, t.Db.ObtenerIngresoFijo(id, t.UsuarioId)!.FechaInicio);
    }

    [Fact]
    public void ElMesNavegableSigueAlActualSiSeEstabaMirandoElActual()
    {
        var hoy = new DateTime(2026, 9, 30);
        var mes = new MesNavegable(() => hoy);
        mes.SeguirAlActual();
        Assert.Equal(9, mes.Mes);

        hoy = new DateTime(2026, 10, 1);   // pasó la medianoche del último día
        mes.SeguirAlActual();
        Assert.Equal(10, mes.Mes);

        mes.Anterior();                    // ahora se mira septiembre a propósito
        hoy = new DateTime(2026, 11, 1);
        mes.SeguirAlActual();
        Assert.Equal(9, mes.Mes);
    }
}
