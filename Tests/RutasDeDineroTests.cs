using FinanzasPersonales.Models;
using Xunit;

namespace FinanzasPersonales.Tests;

/// <summary>
/// Caracterización de las rutas de dinero que HOY funcionan bien.
/// <para>
/// Estas pruebas son la red de seguridad del Bloque 2: cuando <c>Cuentas.SaldoActual</c> y
/// <c>TarjetasCredito.SaldoUsado</c> pasen a ser valores derivados, todas deben seguir verdes.
/// Si alguna se pone roja, el refactor rompió algo.
/// </para>
/// </summary>
public class RutasDeDineroTests
{
    private static int CrearCuenta(BaseDePrueba t, string nombre = "Principal")
    {
        t.Db.InsertarCuenta(new Cuenta { Nombre = nombre }, t.UsuarioId);
        return t.Db.ObtenerCuentas(t.UsuarioId).First(c => c.Nombre == nombre).Id;
    }

    private static decimal SaldoDe(BaseDePrueba t, int cuentaId) =>
        t.Db.ObtenerCuentas(t.UsuarioId).First(c => c.Id == cuentaId).SaldoActual;

    [Fact]
    public void Gasto_ConCuenta_BajaElSaldo()
    {
        using var t = new BaseDePrueba();
        var cuentaId = CrearCuenta(t);

        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = 5000, Categoria = "Alimentación",
            Fecha = new DateTime(2026, 9, 10), CuentaId = cuentaId
        }, t.UsuarioId);

        Assert.Equal(-5000m, SaldoDe(t, cuentaId));
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    [Fact]
    public void Ingreso_ConCuenta_SubeElSaldo()
    {
        using var t = new BaseDePrueba();
        var cuentaId = CrearCuenta(t);

        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Ingreso", Monto = 80000, Categoria = "Otros",
            Fecha = new DateTime(2026, 9, 10), CuentaId = cuentaId
        }, t.UsuarioId);

        Assert.Equal(80000m, SaldoDe(t, cuentaId));
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    [Fact]
    public void EliminarTransaccion_RevierteElSaldoDeLaCuenta()
    {
        using var t = new BaseDePrueba();
        var cuentaId = CrearCuenta(t);

        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = 5000, Categoria = "Alimentación",
            Fecha = new DateTime(2026, 9, 10), CuentaId = cuentaId
        }, t.UsuarioId);

        var id = t.Db.ObtenerTransacciones(t.UsuarioId).Single().Id;
        t.Db.EliminarTransaccion(id);

        Assert.Equal(0m, SaldoDe(t, cuentaId));
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    [Fact]
    public void GastoConTarjeta_SubeElSaldoUsado_YNoTocaNingunaCuenta()
    {
        using var t = new BaseDePrueba();
        var cuentaId  = CrearCuenta(t);
        var tarjetaId = (int)t.Escalar<long>("SELECT Id FROM TarjetasCredito LIMIT 1");

        // Una tarjeta recién sembrada tiene FechaUltimoCorte vacía, así que la PRIMERA lectura
        // dispara AutoRenovarPeriodo y pone el saldo en cero. La app hace esto en cada pantalla.
        // Ver BugsConocidosTests.Bug_2_1_LeerLasTarjetasBorraElSaldoUsado.
        t.Db.ObtenerTarjetas(t.UsuarioId);

        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = 30000, Categoria = "Tecnología",
            Fecha = DateTime.Today, TarjetaCreditoId = tarjetaId
        }, t.UsuarioId);

        var tarjeta = t.Db.ObtenerTarjetas(t.UsuarioId).First(x => x.Id == tarjetaId);
        Assert.Equal(30000m, tarjeta.SaldoUsado);
        Assert.Equal(0m, SaldoDe(t, cuentaId)); // comprar a crédito no mueve efectivo
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    [Fact]
    public void GastoFijo_MarcarYDesmarcar_DejaElSaldoComoEstaba()
    {
        using var t = new BaseDePrueba();
        var cuentaId = CrearCuenta(t);
        var hoy      = DateTime.Today;

        t.Db.InsertarGastoFijo(new GastoFijo
        {
            Nombre = "Internet", Monto = 25000, DiaVencimiento = 10
        }, t.UsuarioId);

        var pago = t.Db.ObtenerGastosFijosConEstado(hoy.Year, hoy.Month, t.UsuarioId).Single();

        t.Db.MarcarGastoFijoPagado(pago.PagoMensualId, true, "Principal", t.UsuarioId, cuentaId, null);
        Assert.Equal(-25000m, SaldoDe(t, cuentaId));
        Assert.Single(t.Db.ObtenerTransacciones(t.UsuarioId));
        // La app etiqueta esta transacción con la categoría «Gastos Fijos», que no existe en
        // la tabla Categorias (ver Bug_6_LasCategoriasDelSistemaNoExistenEnLaTabla). Por eso se
        // exige ausencia de críticos y no una lista vacía.
        Assert.Empty(t.Criticos());

        t.Db.MarcarGastoFijoPagado(pago.PagoMensualId, false, "", t.UsuarioId);
        Assert.Equal(0m, SaldoDe(t, cuentaId));
        Assert.Empty(t.Db.ObtenerTransacciones(t.UsuarioId));
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    [Fact]
    public void IngresoFijo_MarcarYDesmarcar_DejaElSaldoComoEstaba()
    {
        using var t = new BaseDePrueba();
        var cuentaId = CrearCuenta(t);
        var hoy      = DateTime.Today;

        t.Db.InsertarIngresoFijo(new IngresoFijo
        {
            Nombre = "Sueldo", Monto = 600000, DiaIngreso = 15, CuentaId = cuentaId
        }, t.UsuarioId);

        var pago = t.Db.ObtenerIngresosFijosConEstado(hoy.Year, hoy.Month, t.UsuarioId).Single();

        t.Db.MarcarIngresoFijoRecibido(pago.PagoMensualId, true, t.UsuarioId);
        Assert.Equal(600000m, SaldoDe(t, cuentaId));
        Assert.Empty(t.Criticos()); // categoría «Ingresos Fijos» inexistente: ver Bug_6_*

        t.Db.MarcarIngresoFijoRecibido(pago.PagoMensualId, false, t.UsuarioId);
        Assert.Equal(0m, SaldoDe(t, cuentaId));
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    [Fact]
    public void IngresoLaboralDirecto_SumaYSuEliminacionResta()
    {
        using var t = new BaseDePrueba();
        var cuentaId = CrearCuenta(t);

        t.Db.InsertarIngresoLaboralDirecto(
            t.UsuarioId, new DateTime(2026, 9, 15), 450000, "Quincena", cuentaId);

        Assert.Equal(450000m, SaldoDe(t, cuentaId));
        Assert.Empty(t.Criticos()); // categoría «Salario» inexistente: ver Bug_6_*

        var directo = t.Db.ObtenerIngresosLaboralesDirectos(t.UsuarioId).Single();
        t.Db.EliminarIngresoLaboralDirecto(directo.Id);

        Assert.Equal(0m, SaldoDe(t, cuentaId));
        Assert.Empty(t.Db.ObtenerTransacciones(t.UsuarioId));
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    [Fact]
    public void CerrarMes_GuardaLosTotalesDelMes()
    {
        using var t = new BaseDePrueba();
        var cuentaId = CrearCuenta(t);

        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Ingreso", Monto = 100000, Categoria = "Otros",
            Fecha = new DateTime(2026, 8, 5), CuentaId = cuentaId
        }, t.UsuarioId);
        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = 40000, Categoria = "Hogar",
            Fecha = new DateTime(2026, 8, 20), CuentaId = cuentaId
        }, t.UsuarioId);

        Assert.False(t.Db.MesCerrado(t.UsuarioId, 2026, 8));
        t.Db.CerrarMes(t.UsuarioId, 2026, 8);
        Assert.True(t.Db.MesCerrado(t.UsuarioId, 2026, 8));

        var resumen = t.Db.ObtenerResumenesMensuales(t.UsuarioId).Single();
        Assert.Equal(100000m, resumen.Ingresos);
        Assert.Equal(40000m,  resumen.Gastos);
        Assert.Equal(60000m,  resumen.Balance);
        Assert.Empty(t.Db.VerificarIntegridad());
    }
}
