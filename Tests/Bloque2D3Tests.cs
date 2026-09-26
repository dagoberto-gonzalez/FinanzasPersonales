using FinanzasPersonales.Models;
using Xunit;

namespace FinanzasPersonales.Tests;

/// <summary>
/// Decisión D3 — el saldo de una tarjeta se calcula (compras − pagos) en lugar de mantenerse
/// como contador que se reiniciaba solo en la fecha de corte.
/// </summary>
public class Bloque2D3Tests
{
    private static int Tarjeta(BaseDePrueba t) =>
        (int)t.Escalar<long>("SELECT Id FROM TarjetasCredito LIMIT 1");

    private static TarjetaCredito Leer(BaseDePrueba t, int id) =>
        t.Db.ObtenerTarjetas(t.UsuarioId).First(x => x.Id == id);

    private static void Comprar(BaseDePrueba t, int tarjetaId, decimal monto, DateTime fecha) =>
        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = monto, Categoria = "Tecnología",
            Fecha = fecha, TarjetaCreditoId = tarjetaId
        }, t.UsuarioId);

    private static void Pagar(BaseDePrueba t, int tarjetaId, decimal monto, DateTime fecha, int? cuentaId = null) =>
        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = monto, Categoria = "Tarjeta", Descripcion = "Pago",
            Fecha = fecha, CuentaId = cuentaId, PagoDeTarjetaId = tarjetaId
        }, t.UsuarioId);

    // ── §2.1: leer ya no destruye el saldo ──

    /// <summary>
    /// Regresión del defecto más destructivo del diagnóstico. <c>ObtenerTarjetas()</c> era un
    /// SELECT que ESCRIBÍA: al cruzar el día de corte ponía el saldo en cero, así que bastaba
    /// abrir cualquier pantalla ese día para que la deuda desapareciera de la vista.
    /// </summary>
    [Fact]
    public void LeerLasTarjetasNoBorraElSaldo()
    {
        using var t  = new BaseDePrueba();
        var       id = Tarjeta(t);

        Comprar(t, id, 45000, DateTime.Today);

        // Antes se llamaba varias veces a lo largo de la app: Dashboard, Tarjetas, combos…
        for (int i = 0; i < 5; i++) t.Db.ObtenerTarjetas(t.UsuarioId);

        Assert.Equal(45000m, Leer(t, id).SaldoUsado);
        Assert.Single(t.Db.ObtenerTransaccionesPorTarjeta(id));
    }

    /// <summary>
    /// El fondo del asunto: el corte NO es cuando se paga. Entre el corte y el día de pago la
    /// deuda sigue viva, y antes la aplicación afirmaba que no se debía nada.
    /// </summary>
    [Fact]
    public void LaDeudaSobreviveAlDiaDeCorte()
    {
        using var t  = new BaseDePrueba();
        var       id = Tarjeta(t);

        // Compra de un período anterior, muy por detrás de cualquier fecha de corte.
        Comprar(t, id, 80000, DateTime.Today.AddMonths(-3));

        var tarjeta = Leer(t, id);
        Assert.Equal(80000m, tarjeta.SaldoUsado);       // se sigue debiendo
        Assert.Equal(0m,     tarjeta.ConsumoPeriodo);   // pero no se consumió en este período
    }

    // ── El saldo es compras menos pagos ──

    [Fact]
    public void ElSaldoEsComprasMenosPagos()
    {
        using var t  = new BaseDePrueba();
        var       id = Tarjeta(t);
        t.Db.InsertarCuenta(new Cuenta { Nombre = "Principal", SaldoInicial = 500000 }, t.UsuarioId);
        var cuentaId = t.Db.ObtenerCuentas(t.UsuarioId).Single().Id;

        Comprar(t, id, 25000, new DateTime(2026, 9, 3));
        Comprar(t, id, 60000, new DateTime(2026, 9, 10));
        Assert.Equal(85000m, Leer(t, id).SaldoUsado);

        Pagar(t, id, 85000, new DateTime(2026, 10, 5), cuentaId);
        Assert.Equal(0m, Leer(t, id).SaldoUsado);

        Assert.Empty(t.Db.VerificarIntegridad());
    }

    [Fact]
    public void UnPagoParcialDejaElResto()
    {
        using var t  = new BaseDePrueba();
        var       id = Tarjeta(t);

        Comprar(t, id, 100000, new DateTime(2026, 9, 3));
        Pagar(t,   id,  30000, new DateTime(2026, 10, 5));

        Assert.Equal(70000m, Leer(t, id).SaldoUsado);
    }

    [Fact]
    public void BorrarUnaCompraBajaLaDeudaSinRevertirNada()
    {
        using var t  = new BaseDePrueba();
        var       id = Tarjeta(t);

        Comprar(t, id, 40000, new DateTime(2026, 9, 3));
        Comprar(t, id, 10000, new DateTime(2026, 9, 4));
        Assert.Equal(50000m, Leer(t, id).SaldoUsado);

        var compra = t.Db.ObtenerTransaccionesPorTarjeta(id).First(x => x.Monto == 40000);
        t.Db.EliminarTransaccion(compra.Id);

        // Antes esto restaba de un contador con MAX(0,...) que enmascaraba el desajuste.
        Assert.Equal(10000m, Leer(t, id).SaldoUsado);
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    // ── Deuda total vs. consumo del período ──

    [Fact]
    public void LaDeudaTotalYElConsumoDelPeriodoSonNumerosDistintos()
    {
        using var t  = new BaseDePrueba();
        var       id = Tarjeta(t);

        Comprar(t, id, 70000, DateTime.Today.AddMonths(-2));   // período viejo, sin pagar
        Comprar(t, id, 20000, DateTime.Today);                 // período en curso

        var tarjeta = Leer(t, id);
        Assert.Equal(90000m, tarjeta.SaldoUsado);      // lo que realmente se debe
        Assert.Equal(20000m, tarjeta.ConsumoPeriodo);  // lo consumido desde el corte

        // Antes estos dos números estaban colapsados en uno, y por eso ninguno era correcto.
        Assert.NotEqual(tarjeta.SaldoUsado, tarjeta.ConsumoPeriodo);
    }

    [Fact]
    public void ElCupoDisponibleSaleDeLaDeudaReal()
    {
        using var t  = new BaseDePrueba();
        var       id = Tarjeta(t);

        Comprar(t, id, 200000, DateTime.Today.AddMonths(-1));
        var tarjeta = Leer(t, id);

        Assert.Equal(tarjeta.LimiteCredito - 200000m, tarjeta.SaldoDisponible);
    }

    // ── El verificador avisa de lo que ahora sí puede pasar ──

    [Fact]
    public void AvisaSiSePagoMasDeLoComprado()
    {
        using var t  = new BaseDePrueba();
        var       id = Tarjeta(t);

        Comprar(t, id, 10000, new DateTime(2026, 9, 3));
        Pagar(t,   id, 50000, new DateTime(2026, 10, 5));

        var p = t.Db.VerificarIntegridad().FirstOrDefault(x => x.Area == "Tarjetas");
        Assert.NotNull(p);
        Assert.Contains("más de lo comprado", p!.Descripcion);
    }

    // ── El esquema ya no guarda lo que se calcula ──

    [Fact]
    public void LaTablaYaNoTieneSaldoUsadoNiFechaDeCorte()
    {
        using var t = new BaseDePrueba();

        Assert.Equal(0, t.Escalar<long>(
            "SELECT COUNT(*) FROM pragma_table_info('TarjetasCredito') WHERE name='SaldoUsado'"));
        Assert.Equal(0, t.Escalar<long>(
            "SELECT COUNT(*) FROM pragma_table_info('TarjetasCredito') WHERE name='FechaUltimoCorte'"));
    }
}
