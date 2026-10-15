using FinanzasPersonales.Models;
using FinanzasPersonales.ViewModels;
using Xunit;

namespace FinanzasPersonales.Tests;

/// <summary>
/// Transferencias entre cuentas. Antes había que registrar un gasto en una y un ingreso en la
/// otra, y los dos contaban en reportes y presupuesto como dinero gastado y ganado.
/// </summary>
public class TransferenciasTests
{
    private static int CrearCuenta(BaseDePrueba t, string nombre, decimal saldoInicial)
    {
        t.Db.InsertarCuenta(new Cuenta { Nombre = nombre, SaldoInicial = saldoInicial }, t.UsuarioId);
        return t.Db.ObtenerCuentas(t.UsuarioId).First(c => c.Nombre == nombre).Id;
    }

    private static decimal Saldo(BaseDePrueba t, int id) =>
        t.Db.ObtenerCuentas(t.UsuarioId).Single(c => c.Id == id).SaldoActual;

    [Fact]
    public void MueveElDineroDeUnaCuentaALaOtra()
    {
        using var t = new BaseDePrueba();
        var corriente = CrearCuenta(t, "Corriente", 500_000);
        var ahorros   = CrearCuenta(t, "Ahorros",   100_000);

        t.Db.Transferir(corriente, ahorros, 200_000, DateTime.Today, "ahorro del mes", t.UsuarioId);

        Assert.Equal(300_000m, Saldo(t, corriente));
        Assert.Equal(300_000m, Saldo(t, ahorros));

        var txs = t.Db.ObtenerTransacciones(t.UsuarioId);
        Assert.Equal(2, txs.Count);
        Assert.All(txs, x => Assert.Equal("Transferencia", x.Categoria));
        Assert.Single(txs.Select(x => x.TransferenciaId).Distinct());
        Assert.Contains(txs, x => x.Descripcion == "Transferencia a Ahorros"        && x.Tipo == "Gasto");
        Assert.Contains(txs, x => x.Descripcion == "Transferencia desde Corriente"  && x.Tipo == "Ingreso");
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    /// <summary>No es gasto ni ingreso: queda fuera del presupuesto y de los totales del mes.</summary>
    [Fact]
    public void NoCuentaComoGastoNiComoIngreso()
    {
        using var t = new BaseDePrueba();
        var corriente = CrearCuenta(t, "Corriente", 500_000);
        var ahorros   = CrearCuenta(t, "Ahorros",   0);
        t.Db.GuardarPresupuesto(t.UsuarioId, 100_000);

        t.Db.Transferir(corriente, ahorros, 200_000, DateTime.Today, "", t.UsuarioId);
        var vm = new DashboardViewModel(t.Db, t.UsuarioId);

        Assert.All(t.Db.ObtenerTransacciones(t.UsuarioId), x => Assert.False(x.AfectaPresupuesto));
        Assert.Equal(0m, vm.IngresosMes);
        Assert.Equal(0m, vm.GastosMes);
        Assert.False(vm.PresupuestoExcedido);
        Assert.Equal(500_000m, vm.BalanceTotal);   // el disponible no cambia: sólo se movió
    }

    [Fact]
    public void DepositarEfectivoEnElBanco()
    {
        using var t = new BaseDePrueba();
        var banco = CrearCuenta(t, "Banco", 0);
        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Ingreso", Monto = 50_000, Categoria = "Salario", Fecha = DateTime.Today
        }, t.UsuarioId);   // efectivo

        t.Db.Transferir(null, banco, 30_000, DateTime.Today, "depósito", t.UsuarioId);

        var vm = new DashboardViewModel(t.Db, t.UsuarioId);
        Assert.Equal(30_000m, Saldo(t, banco));
        Assert.Contains("En efectivo ₡20.000", vm.DesgloseDisponibleTexto);
        Assert.Equal(50_000m, vm.BalanceTotal);
    }

    [Fact]
    public void RechazaElMismoOrigenYDestinoYMontosNoPositivos()
    {
        using var t = new BaseDePrueba();
        var a = CrearCuenta(t, "A", 100);

        Assert.Throws<ArgumentException>(() => t.Db.Transferir(a, a, 10, DateTime.Today, "", t.UsuarioId));
        Assert.Throws<ArgumentException>(() => t.Db.Transferir(null, null, 10, DateTime.Today, "", t.UsuarioId));
        Assert.Throws<ArgumentException>(() => t.Db.Transferir(a, null, 0, DateTime.Today, "", t.UsuarioId));
        Assert.Empty(t.Db.ObtenerTransacciones(t.UsuarioId));
    }

    /// <summary>D5: el dinero apartado en metas no se puede transferir sin liberarlo antes.</summary>
    [Fact]
    public void NoSePuedeTransferirDineroApartadoEnMetas()
    {
        using var t = new BaseDePrueba();
        var ahorros = CrearCuenta(t, "Ahorros", 300_000);
        var otra    = CrearCuenta(t, "Otra", 0);
        t.Db.InsertarMeta(new MetaAhorro
        {
            Nombre = "Refri", MontoObjetivo = 300_000, FechaLimite = DateTime.Today.AddMonths(3), CuentaId = ahorros
        }, t.UsuarioId);
        t.Db.ApartarEnMeta(t.Db.ObtenerMetas(t.UsuarioId).Single().Id, 250_000, t.UsuarioId);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            t.Db.Transferir(ahorros, otra, 100_000, DateTime.Today, "", t.UsuarioId));
        Assert.Contains("50.000", ex.Message);

        t.Db.Transferir(ahorros, otra, 50_000, DateTime.Today, "", t.UsuarioId);   // lo libre, sí
        Assert.Equal(50_000m, Saldo(t, otra));
    }

    /// <summary>Borrar una mitad borra las dos: si no, el dinero desaparecería o aparecería.</summary>
    [Fact]
    public void BorrarUnaMitadBorraLaTransferenciaEntera()
    {
        using var t = new BaseDePrueba();
        var a = CrearCuenta(t, "A", 100_000);
        var b = CrearCuenta(t, "B", 0);
        t.Db.Transferir(a, b, 40_000, DateTime.Today, "", t.UsuarioId);
        var entrada = t.Db.ObtenerTransacciones(t.UsuarioId).Single(x => x.Tipo == "Ingreso");

        Assert.NotNull(t.Db.MotivoNoEditable(entrada.Id));   // editar una mitad descuadraría la otra
        t.Db.EliminarTransaccion(entrada.Id);

        Assert.Empty(t.Db.ObtenerTransacciones(t.UsuarioId));
        Assert.Equal(100_000m, Saldo(t, a));
        Assert.Equal(0m, Saldo(t, b));
    }

    [Fact]
    public void NoSeRegistraUnaTransferenciaAMano()
    {
        using var t = new BaseDePrueba();
        Assert.Throws<ArgumentException>(() => t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = 1000, Categoria = "Transferencia", Fecha = DateTime.Today
        }, t.UsuarioId));
    }

    [Fact]
    public void ElVerificadorDetectaUnaTransferenciaIncompleta()
    {
        using var t = new BaseDePrueba();
        var a = CrearCuenta(t, "A", 100_000);
        var b = CrearCuenta(t, "B", 0);
        t.Db.Transferir(a, b, 40_000, DateTime.Today, "", t.UsuarioId);
        t.Sql("DELETE FROM Transacciones WHERE Tipo='Ingreso'");   // queda sólo la salida

        Assert.Contains(t.Db.VerificarIntegridad(), p => p.Descripcion.Contains("Transferencias"));
    }

    [Fact]
    public void LaCategoriaTransferenciaNoSeOfreceEnLosFormularios()
    {
        using var t = new BaseDePrueba();
        var vm = new TransaccionesViewModel(t.Db, t.UsuarioId);

        Assert.DoesNotContain("Transferencia", vm.CategoriasForm);
        vm.EsIngreso = true;
        Assert.DoesNotContain("Transferencia", vm.CategoriasForm);
        Assert.Contains("Transferencia", vm.CategoriasFiltro);   // sí para consultarlas
    }

    // ── Pantalla Cuentas ──

    [Fact]
    public void DesdeLaPantallaCuentas()
    {
        using var t = new BaseDePrueba();
        CrearCuenta(t, "Corriente", 500_000);
        CrearCuenta(t, "Ahorros", 0);
        var vm = new CuentasViewModel(t.Db, t.UsuarioId);

        Assert.Equal("Efectivo", vm.OpcionesTransferencia.First().Nombre);
        vm.TransOrigen  = vm.OpcionesTransferencia.Single(o => o.Nombre == "Corriente");
        vm.TransDestino = vm.OpcionesTransferencia.Single(o => o.Nombre == "Ahorros");
        vm.TransMonto   = "150.000";
        vm.TransferirCommand.Execute(null);

        Assert.Equal("", vm.TransError);
        Assert.Contains("150.000", vm.TransOk);
        Assert.Equal(150_000m, vm.Cuentas.Single(c => c.Nombre == "Ahorros").SaldoActual);
        // Tras recargar, lo elegido sigue elegido.
        Assert.Equal("Corriente", vm.TransOrigen?.Nombre);
        Assert.Equal("Ahorros",   vm.TransDestino?.Nombre);
    }

    // ── La fila Efectivo de Cuentas ──

    [Fact]
    public void CuentasMuestraElEfectivoConLaMismaCifraQueElDashboard()
    {
        using var t = new BaseDePrueba();
        var banco = CrearCuenta(t, "Banco", 0);
        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Ingreso", Monto = 50_000, Categoria = "Salario", Fecha = DateTime.Today
        }, t.UsuarioId);
        t.Db.Transferir(null, banco, 30_000, DateTime.Today, "", t.UsuarioId);

        var cuentas = new CuentasViewModel(t.Db, t.UsuarioId);
        var dash    = new DashboardViewModel(t.Db, t.UsuarioId);

        var efectivo = cuentas.Cuentas.Last();
        Assert.True(efectivo.EsEfectivo);
        Assert.Equal(20_000m, efectivo.SaldoActual);
        Assert.Contains("En efectivo ₡20.000", dash.DesgloseDisponibleTexto);
        // Todo el dinero en un sitio: la tabla suma lo mismo que el Disponible.
        Assert.Equal(dash.BalanceTotal, cuentas.Cuentas.Sum(c => c.SaldoActual));
    }

    [Fact]
    public void LaFilaEfectivoNoSeSeleccionaNiSeDuplica()
    {
        using var t = new BaseDePrueba();
        CrearCuenta(t, "Banco", 0);
        var vm = new CuentasViewModel(t.Db, t.UsuarioId);

        vm.Seleccionada = vm.Cuentas.Single(c => c.EsEfectivo);
        Assert.Null(vm.Seleccionada);                 // no abre el panel de edición

        Assert.Single(vm.OpcionesTransferencia, o => o.Nombre == "Efectivo");

        vm.NuevaNombre = "Efectivo";                  // nombre ocupado
        vm.AgregarCommand.Execute(null);
        Assert.NotEqual("", vm.NuevaError);
        Assert.DoesNotContain(t.Db.ObtenerCuentas(t.UsuarioId), c => c.Nombre == "Efectivo");
    }
}
