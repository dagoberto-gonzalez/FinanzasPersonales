using FinanzasPersonales.Models;
using FinanzasPersonales.ViewModels;
using Xunit;

namespace FinanzasPersonales.Tests;

/// <summary>
/// Bloque 1 · 1.4 — el desplegable de método de pago según el tipo de transacción.
/// Cubre la lógica que el XAML consume; el renderizado hay que verlo en la aplicación.
/// </summary>
public class MetodoPagoTests
{
    [Fact]
    public void EnUnGasto_TarjetaEstaDisponible()
    {
        using var t  = new BaseDePrueba();
        var       vm = new TransaccionesViewModel(t.Db, t.UsuarioId);

        Assert.True(vm.EsGasto);
        Assert.Contains("Tarjeta", vm.MetodosPago);
    }

    [Fact]
    public void AlPasarAIngreso_TarjetaDesaparece()
    {
        using var t  = new BaseDePrueba();
        var       vm = new TransaccionesViewModel(t.Db, t.UsuarioId);

        vm.EsIngreso = true;

        Assert.DoesNotContain("Tarjeta", vm.MetodosPago);
        Assert.Equal(["Efectivo", "Transferencia", "SINPE Móvil"], vm.MetodosPago);
    }

    [Fact]
    public void AlVolverAGasto_TarjetaReaparece()
    {
        using var t  = new BaseDePrueba();
        var       vm = new TransaccionesViewModel(t.Db, t.UsuarioId);

        vm.EsIngreso = true;
        vm.EsGasto   = true;

        Assert.Contains("Tarjeta", vm.MetodosPago);
    }

    [Fact]
    public void SiTarjetaEstabaSeleccionada_AlPasarAIngresoVuelveAEfectivo()
    {
        using var t  = new BaseDePrueba();
        var       vm = new TransaccionesViewModel(t.Db, t.UsuarioId);

        vm.MetodoPago = "Tarjeta";
        Assert.Equal("Tarjeta", vm.MetodoPago);

        vm.EsIngreso = true;

        Assert.Equal("Efectivo", vm.MetodoPago);
    }

    [Fact]
    public void MetodoPagoNuncaQuedaNulo()
    {
        using var t  = new BaseDePrueba();
        var       vm = new TransaccionesViewModel(t.Db, t.UsuarioId);

        // Es lo que hace WPF mientras la colección está vacía durante el Clear().
        vm.MetodoPago = null!;

        Assert.Equal("Efectivo", vm.MetodoPago);
    }

    [Fact]
    public void UnMetodoValidoQueNoEsTarjeta_SeConservaAlCambiarDeTipo()
    {
        using var t  = new BaseDePrueba();
        var       vm = new TransaccionesViewModel(t.Db, t.UsuarioId);

        vm.MetodoPago = "SINPE Móvil";
        vm.EsIngreso  = true;

        Assert.Equal("SINPE Móvil", vm.MetodoPago);
    }

    /// <summary>
    /// La prueba de fondo de 1.4: por la ruta de la interfaz ya no se puede llegar a guardar
    /// un ingreso con tarjeta, así que <c>InsertarTransaccion</c> nunca llega a rechazarlo.
    /// </summary>
    [Fact]
    public void GuardarUnIngreso_NuncaAsociaUnaTarjeta()
    {
        using var t = new BaseDePrueba();
        t.Db.InsertarCuenta(new Cuenta { Nombre = "Principal" }, t.UsuarioId);

        var vm = new TransaccionesViewModel(t.Db, t.UsuarioId);
        vm.MetodoPago = "Tarjeta";      // se elige con el tipo en Gasto
        vm.EsIngreso  = true;           // y luego se cambia a Ingreso
        vm.Monto      = "250000";
        vm.Categoria  = "Salario";

        vm.GuardarCommand.Execute(null);

        var guardada = Assert.Single(t.Db.ObtenerTransacciones(t.UsuarioId));
        Assert.Equal("Ingreso", guardada.Tipo);
        Assert.Null(guardada.TarjetaCreditoId);
        Assert.Empty(t.Db.VerificarIntegridad());
    }
}
