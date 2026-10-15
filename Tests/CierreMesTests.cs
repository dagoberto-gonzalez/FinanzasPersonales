using FinanzasPersonales.Data;
using FinanzasPersonales.Models;
using Xunit;

namespace FinanzasPersonales.Tests;

/// <summary>
/// El cierre de mes: el balance acumulado que guarda y qué meses se ofrecen para cerrar.
/// </summary>
public class CierreMesTests
{
    private static readonly DateTime Hoy = new(2026, 10, 14);

    private static void Mov(BaseDePrueba t, string tipo, decimal monto, DateTime fecha, int? cuenta = null) =>
        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = tipo, Monto = monto, Categoria = "Otros", Fecha = fecha, CuentaId = cuenta
        }, t.UsuarioId);

    private static int Cuenta(BaseDePrueba t, string nombre, decimal saldoInicial)
    {
        t.Db.InsertarCuenta(new Cuenta { Nombre = nombre, SaldoInicial = saldoInicial }, t.UsuarioId);
        return t.Db.ObtenerCuentas(t.UsuarioId).Single(c => c.Nombre == nombre).Id;
    }

    // ── Balance acumulado ──

    [Fact]
    public void ElAcumuladoEsElDisponible_ConSaldosInicialesYEfectivo()
    {
        using var t = new BaseDePrueba();
        var banco = Cuenta(t, "Banco", 300_000);
        Mov(t, "Ingreso", 100_000, DateTime.Today, banco);
        Mov(t, "Gasto",    20_000, DateTime.Today);          // en efectivo

        t.Db.CerrarMes(t.UsuarioId, DateTime.Today.Year, DateTime.Today.Month);
        var acumulado = t.Db.ObtenerResumenesMensuales(t.UsuarioId).Single().BalanceAcumulado;

        var disponible = t.Db.ObtenerCuentas(t.UsuarioId).Sum(c => c.SaldoActual)
                       + t.Db.ObtenerSaldoEfectivo(t.UsuarioId);
        Assert.Equal(380_000m, acumulado);
        Assert.Equal(disponible, acumulado);
    }

    [Fact]
    public void ElAcumuladoNoCuentaLoQuePasoDespuesDelMes()
    {
        using var t = new BaseDePrueba();
        var banco = Cuenta(t, "Banco", 50_000);
        Mov(t, "Ingreso", 10_000, new DateTime(2026, 9, 30), banco);
        Mov(t, "Ingreso", 99_000, new DateTime(2026, 10, 1), banco);

        t.Db.CerrarMes(t.UsuarioId, 2026, 9);

        Assert.Equal(60_000m, t.Db.ObtenerResumenesMensuales(t.UsuarioId).Single().BalanceAcumulado);
    }

    [Fact]
    public void LosCierresGuardadosConLaFormulaViejaSeCorrigenUnaVez()
    {
        using var t = new BaseDePrueba();
        Cuenta(t, "Banco", 300_000);
        // Un cierre de setiembre como lo dejaba la versión anterior: sin el saldo inicial.
        t.Sql($"""
            INSERT INTO ResumenMensual (UsuarioId, Anio, Mes, Ingresos, Gastos, Balance, BalanceAcumulado, NumTransacciones, FechaCierre)
            VALUES ({t.UsuarioId}, 2026, 9, 0, 0, 0, 0, 0, '30/09/2026 20:00');
            DELETE FROM Configuracion WHERE Clave = 'cierres_con_saldo_inicial';
            """);

        var reabierta = new AppDatabase(t.Ruta);   // al abrir corren las migraciones

        Assert.Equal(300_000m, reabierta.ObtenerResumenesMensuales(t.UsuarioId).Single().BalanceAcumulado);

        // Una sola vez: un cierre posterior que alguien dejara así no se vuelve a tocar.
        t.Sql("UPDATE ResumenMensual SET BalanceAcumulado = 1");
        _ = new AppDatabase(t.Ruta);
        Assert.Equal(1m, t.Db.ObtenerResumenesMensuales(t.UsuarioId).Single().BalanceAcumulado);
    }

    // ── Meses sin cerrar ──

    private static void AltaEl(BaseDePrueba t, DateTime fecha) =>
        t.Sql($"UPDATE Usuarios SET FechaCreacion = '{fecha:yyyy-MM-dd}' WHERE Id = {t.UsuarioId}");

    [Fact]
    public void UnUsuarioNuevoNoTieneMesesQueCerrar()
    {
        using var t = new BaseDePrueba();
        AltaEl(t, Hoy);

        Assert.Empty(t.Db.MesesSinCerrar(t.UsuarioId, Hoy));
    }

    [Fact]
    public void SeOfrecenTodosLosMesesTerminadosDesdeElAlta_DelMasAntiguo()
    {
        using var t = new BaseDePrueba();
        AltaEl(t, new DateTime(2026, 7, 20));

        Assert.Equal([(2026, 7), (2026, 8), (2026, 9)], t.Db.MesesSinCerrar(t.UsuarioId, Hoy));

        t.Db.CerrarMes(t.UsuarioId, 2026, 7);
        Assert.Equal([(2026, 8), (2026, 9)], t.Db.MesesSinCerrar(t.UsuarioId, Hoy));
    }

    [Fact]
    public void UnMovimientoAnteriorAlAltaTambienCuenta()
    {
        // Quien registra gastos atrasados (con fecha de antes de crear su cuenta) puede cerrarlos.
        using var t = new BaseDePrueba();
        AltaEl(t, Hoy);
        Mov(t, "Gasto", 5_000, new DateTime(2026, 8, 15));

        Assert.Equal([(2026, 8), (2026, 9)], t.Db.MesesSinCerrar(t.UsuarioId, Hoy));
    }
}
