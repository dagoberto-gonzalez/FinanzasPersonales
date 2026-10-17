using System.Windows;
using FinanzasPersonales.Models;
using FinanzasPersonales.Services;
using FinanzasPersonales.ViewModels;
using Xunit;

namespace FinanzasPersonales.Tests;

/// <summary>
/// El ingreso de una quincena queda ligado a ella. Antes el botón desaparecía al cerrar el
/// período (había que registrar un pago que aún no llegaba), y el historial sumaba el neto del
/// cierre más el ingreso, contando dos veces la misma plata.
/// Escenario: la quincena 1–15 oct 2026 sin extras, que pagó ₡216.818,38.
/// </summary>
[Collection("Sesion")]
public class IngresoQuincenaTests
{
    private static readonly DateTime Dia    = new(2026, 10, 10);
    private static readonly DateTime Inicio = new(2026, 10, 1);
    private const decimal Neto = 216_818.38m;

    private static ControlLaboralViewModel Preparar(BaseDePrueba t)
    {
        SessionService.IniciarSesion(t.UsuarioId, "tester", "Normal");
        t.Db.GuardarConfiguracionLaboral(new ConfiguracionLaboral
        {
            UsuarioId = t.UsuarioId, SalarioMensual = 430_230.76m, JornadaSemanal = 48,
            DiasPorSemana = 5, PorcentajeDeduccion = 10.83m, ModoPago = "Quincenal",
            DiaPago = 15, DiaPago2 = 31, Viaticos = 25_000
        });
        return new ControlLaboralViewModel(t.Db, t.UsuarioId, () => Dia);
    }

    private static void Cerrar(BaseDePrueba t) =>
        t.Db.ConsolidarPeriodoLaboral(t.UsuarioId, Inicio, new DateTime(2026, 10, 15));

    [Fact]
    public void SePuedeRegistrarElIngresoDespuesDeCerrar()
    {
        using var t = new BaseDePrueba();
        var vm = Preparar(t);
        Cerrar(t);
        vm.Cargar();

        Assert.Equal(Visibility.Visible, vm.RegistrarIngresoVisibility);
        Assert.Equal(Neto, vm.SalarioNetoCalculado);   // lo que propone la ventana

        vm.EjecutarRegistroIngreso(new DateTime(2026, 10, 16), Neto, "01 – 15 oct 2026", null);

        Assert.Equal(Visibility.Collapsed, vm.RegistrarIngresoVisibility);
        Assert.Equal(Visibility.Visible,   vm.IngresoRegistradoVisibility);
        Assert.Contains("216.818,38", vm.IngresoRegistradoTexto);
    }

    [Fact]
    public void ElHistorialMuestraUnaFilaPorQuincena_SinContarDosVeces()
    {
        using var t = new BaseDePrueba();
        var vm = Preparar(t);
        Cerrar(t);
        vm.Cargar();
        vm.EjecutarRegistroIngreso(new DateTime(2026, 10, 16), Neto, "", null);

        var fila = Assert.Single(vm.Historial);
        Assert.True(fila.EsCierre);
        Assert.Equal("✓ 16/10", fila.Ingreso);
        Assert.Equal(Neto, fila.BalanceAcumulado);
    }

    [Fact]
    public void RegistrarAntesDeCerrarTambienQuedaLigado()
    {
        using var t = new BaseDePrueba();
        var vm = Preparar(t);
        vm.EjecutarRegistroIngreso(new DateTime(2026, 10, 15), Neto, "", null);
        Cerrar(t);
        vm.Cargar();

        Assert.Equal("✓ 15/10", Assert.Single(vm.Historial).Ingreso);
        Assert.Equal(Visibility.Collapsed, vm.RegistrarIngresoVisibility);
    }

    [Fact]
    public void UnaQuincenaNoSeCobraDosVeces()
    {
        using var t = new BaseDePrueba();
        Preparar(t);
        t.Db.InsertarIngresoLaboralDirecto(t.UsuarioId, Dia, Neto, "", null, Inicio);

        Assert.Throws<InvalidOperationException>(() =>
            t.Db.InsertarIngresoLaboralDirecto(t.UsuarioId, Dia, Neto, "", null, Inicio));
    }

    [Fact]
    public void SiSeBorraLaTransaccion_LaQuincenaVuelveAQuedarPendiente()
    {
        using var t = new BaseDePrueba();
        var vm = Preparar(t);
        Cerrar(t);
        t.Db.InsertarIngresoLaboralDirecto(t.UsuarioId, Dia, Neto, "", null, Inicio);

        var transaccion = t.Db.ObtenerTransacciones(t.UsuarioId).Single();
        t.Db.EliminarTransaccion(transaccion.Id);
        vm.Cargar();

        Assert.Null(t.Db.IngresoDelPeriodo(t.UsuarioId, Inicio));
        Assert.Equal("Pendiente", Assert.Single(vm.Historial).Ingreso);
        Assert.Equal(Visibility.Visible, vm.RegistrarIngresoVisibility);
    }
}
