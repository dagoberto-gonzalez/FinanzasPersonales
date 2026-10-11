using FinanzasPersonales.Models;
using FinanzasPersonales.ViewModels;
using Xunit;

namespace FinanzasPersonales.Tests;

/// <summary>
/// Editar una transacción. Antes sólo se podía borrar y volver a crear. Con los saldos
/// derivados (D1, D3) editar es un UPDATE: las cuentas y las tarjetas se recalculan solas.
/// </summary>
public class EditarTransaccionesTests
{
    private static int CrearCuenta(BaseDePrueba t, string nombre)
    {
        t.Db.InsertarCuenta(new Cuenta { Nombre = nombre }, t.UsuarioId);
        return t.Db.ObtenerCuentas(t.UsuarioId).First(c => c.Nombre == nombre).Id;
    }

    private static decimal SaldoDe(BaseDePrueba t, int cuentaId) =>
        t.Db.ObtenerCuentas(t.UsuarioId).First(c => c.Id == cuentaId).SaldoActual;

    private static Transaccion Gasto(int? cuentaId, decimal monto = 5000) => new()
    {
        Tipo = "Gasto", Monto = monto, Categoria = "Alimentación",
        Fecha = new DateTime(2026, 9, 10), CuentaId = cuentaId, CuentaNombre = "Principal"
    };

    private static Transaccion Unica(BaseDePrueba t) => t.Db.ObtenerTransacciones(t.UsuarioId).Single();

    // ── Base de datos ──

    [Fact]
    public void CambiarElMontoCambiaElSaldoDeLaCuenta()
    {
        using var t = new BaseDePrueba();
        var cuenta = CrearCuenta(t, "Principal");
        t.Db.InsertarTransaccion(Gasto(cuenta, 5000), t.UsuarioId);

        var tx = Unica(t);
        tx.Monto = 8000;
        t.Db.ActualizarTransaccion(tx, t.UsuarioId);

        Assert.Equal(-8000m, SaldoDe(t, cuenta));
        Assert.Single(t.Db.ObtenerTransacciones(t.UsuarioId));   // se editó, no se duplicó
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    [Fact]
    public void PasarDeUnaCuentaAUnaTarjetaMueveLosDosSaldos()
    {
        using var t = new BaseDePrueba();
        var cuenta  = CrearCuenta(t, "Principal");
        var tarjeta = t.Db.ObtenerTarjetas(t.UsuarioId).First();
        t.Db.InsertarTransaccion(Gasto(cuenta, 5000), t.UsuarioId);

        var tx = Unica(t);
        tx.CuentaId = null;
        tx.TarjetaCreditoId = tarjeta.Id;
        t.Db.ActualizarTransaccion(tx, t.UsuarioId);

        Assert.Equal(0m, SaldoDe(t, cuenta));
        Assert.Equal(5000m, t.Db.ObtenerTarjetas(t.UsuarioId).First(x => x.Id == tarjeta.Id).SaldoUsado);
    }

    [Fact]
    public void AlEditarSeAplicanLasMismasReglasQueAlCrear()
    {
        using var t = new BaseDePrueba();
        var tarjeta = t.Db.ObtenerTarjetas(t.UsuarioId).First();
        t.Db.InsertarTransaccion(Gasto(null), t.UsuarioId);

        var tx = Unica(t);
        tx.Tipo = "Ingreso";
        tx.TarjetaCreditoId = tarjeta.Id;   // un ingreso con tarjeta: prohibido

        Assert.Throws<ArgumentException>(() => t.Db.ActualizarTransaccion(tx, t.UsuarioId));
        Assert.Equal("Gasto", Unica(t).Tipo);
    }

    [Fact]
    public void UnPagoDeTarjetaNoSeEditaDesdeAqui()
    {
        using var t = new BaseDePrueba();
        var tarjeta = t.Db.ObtenerTarjetas(t.UsuarioId).First();
        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = 10000, Categoria = "Tarjeta",
            Fecha = new DateTime(2026, 9, 10), PagoDeTarjetaId = tarjeta.Id
        }, t.UsuarioId);

        var tx = Unica(t);
        Assert.Contains("Tarjetas", t.Db.MotivoNoEditable(tx.Id));

        tx.Monto = 1;
        Assert.Throws<InvalidOperationException>(() => t.Db.ActualizarTransaccion(tx, t.UsuarioId));
        Assert.Equal(10000m, Unica(t).Monto);
    }

    /// <summary>IngresoLaboralDirecto guarda su propio monto: editarlo aquí dejaría dos versiones.</summary>
    [Fact]
    public void UnIngresoDeControlLaboralNoSeEditaDesdeAqui()
    {
        using var t = new BaseDePrueba();
        t.Db.InsertarIngresoLaboralDirecto(t.UsuarioId, new DateTime(2026, 9, 30), 250000, "Quincena", null);

        var tx = Unica(t);
        Assert.Contains("Control Laboral", t.Db.MotivoNoEditable(tx.Id));
    }

    /// <summary>PagosMensuales no guarda el monto, lo lee de la transacción: se puede editar.</summary>
    [Fact]
    public void ElPagoDeUnGastoFijoSeEditaYSigueMarcadoComoPagado()
    {
        using var t = new BaseDePrueba();
        var cuenta = CrearCuenta(t, "Principal");
        var hoy    = DateTime.Today;
        t.Db.InsertarGastoFijo(new GastoFijo { Nombre = "Internet", Monto = 25000, DiaVencimiento = 10 }, t.UsuarioId);
        var pago = t.Db.ObtenerGastosFijosConEstado(hoy.Year, hoy.Month, t.UsuarioId).Single();
        t.Db.MarcarGastoFijoPagado(pago.PagoMensualId, true, "Principal", t.UsuarioId, cuenta, null);

        var tx = Unica(t);
        Assert.Null(t.Db.MotivoNoEditable(tx.Id));
        tx.Monto = 27500;   // ese mes vino más caro
        t.Db.ActualizarTransaccion(tx, t.UsuarioId);

        Assert.Equal(-27500m, SaldoDe(t, cuenta));
        Assert.True(t.Db.ObtenerGastosFijosConEstado(hoy.Year, hoy.Month, t.UsuarioId).Single().PagadoEsteMes);
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    [Fact]
    public void NoSeEditaLaTransaccionDeOtroUsuario()
    {
        using var t = new BaseDePrueba();
        var otro = t.Db.InsertarUsuario("otro" + Guid.NewGuid().ToString("N")[..6], "clave123").Id;
        t.Db.InsertarTransaccion(Gasto(null), otro);

        var tx = t.Db.ObtenerTransacciones(otro).Single();
        tx.Monto = 1;

        Assert.Throws<InvalidOperationException>(() => t.Db.ActualizarTransaccion(tx, t.UsuarioId));
        Assert.Equal(5000m, t.Db.ObtenerTransacciones(otro).Single().Monto);
    }

    // ── Pantalla ──

    [Fact]
    public void EditarRellenaElFormularioYGuardarActualiza()
    {
        using var t = new BaseDePrueba();
        var cuenta = CrearCuenta(t, "Principal");
        t.Db.InsertarTransaccion(Gasto(cuenta, 15000), t.UsuarioId);
        var vm = new TransaccionesViewModel(t.Db, t.UsuarioId);

        vm.EditarCommand.Execute(vm.Transacciones.Single());

        Assert.True(vm.EstaEditando);
        Assert.Equal("Guardar cambios", vm.TextoGuardar);
        Assert.Equal("Alimentación", vm.Categoria);
        Assert.Equal("Principal", vm.SelectedSubItem?.Nombre);

        vm.Monto = "18.500";
        vm.GuardarCommand.Execute(null);

        Assert.False(vm.EstaEditando);
        Assert.Equal(18500m, Unica(t).Monto);
        Assert.Equal(-18500m, SaldoDe(t, cuenta));
        Assert.Single(vm.Transacciones);
    }

    /// <summary>
    /// El formulario se rellena con separador de miles. Con el decimal.TryParse de antes,
    /// "15.000" se leía como 15: guardar sin tocar nada habría cambiado el monto a ₡15.
    /// </summary>
    [Fact]
    public void GuardarSinCambiosNoAlteraElMonto()
    {
        using var t = new BaseDePrueba();
        t.Db.InsertarTransaccion(Gasto(null, 15000), t.UsuarioId);
        var vm = new TransaccionesViewModel(t.Db, t.UsuarioId);

        vm.EditarCommand.Execute(vm.Transacciones.Single());
        vm.GuardarCommand.Execute(null);

        Assert.Equal(15000m, Unica(t).Monto);
    }

    [Fact]
    public void UnaCompraConTarjetaSeAbreConSuTarjeta()
    {
        using var t = new BaseDePrueba();
        var tarjeta = t.Db.ObtenerTarjetas(t.UsuarioId).First();
        var compra  = Gasto(null);
        compra.TarjetaCreditoId = tarjeta.Id;
        t.Db.InsertarTransaccion(compra, t.UsuarioId);
        var vm = new TransaccionesViewModel(t.Db, t.UsuarioId);

        vm.EditarCommand.Execute(vm.Transacciones.Single());

        Assert.Equal("Tarjeta", vm.MetodoPago);
        Assert.Equal(tarjeta.Nombre, vm.SelectedSubItem?.Nombre);
    }

    [Fact]
    public void CancelarVuelveAlFormularioDeAlta()
    {
        using var t = new BaseDePrueba();
        t.Db.InsertarTransaccion(Gasto(null, 15000), t.UsuarioId);
        var vm = new TransaccionesViewModel(t.Db, t.UsuarioId);

        vm.EditarCommand.Execute(vm.Transacciones.Single());
        vm.CancelarEdicionCommand.Execute(null);

        Assert.False(vm.EstaEditando);
        Assert.Equal("", vm.Monto);
        Assert.Equal("Guardar", vm.TextoGuardar);

        // Y Guardar vuelve a crear, no a editar.
        vm.Monto = "1000";
        vm.GuardarCommand.Execute(null);
        Assert.Equal(2, t.Db.ObtenerTransacciones(t.UsuarioId).Count);
    }
}
