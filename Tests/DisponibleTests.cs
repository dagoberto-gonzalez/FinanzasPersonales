using FinanzasPersonales.Models;
using FinanzasPersonales.ViewModels;
using Xunit;

namespace FinanzasPersonales.Tests;

/// <summary>
/// El "Disponible" del Dashboard. Era la suma de las transacciones del eje efectivo, que deja
/// fuera el saldo inicial de las cuentas: con ₡2.700.000 de saldos iniciales decía ₡162.000.
/// Ahora es el saldo de cada cuenta (como en la pantalla Cuentas) más el efectivo.
/// </summary>
public class DisponibleTests
{
    private static int CrearCuenta(BaseDePrueba t, string nombre, decimal saldoInicial)
    {
        t.Db.InsertarCuenta(new Cuenta { Nombre = nombre, SaldoInicial = saldoInicial }, t.UsuarioId);
        return t.Db.ObtenerCuentas(t.UsuarioId).First(c => c.Nombre == nombre).Id;
    }

    private static void Mov(BaseDePrueba t, string tipo, decimal monto, int? cuenta = null,
                            int? tarjeta = null, int? pagoDe = null) =>
        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = tipo, Monto = monto, Categoria = pagoDe is null ? "Otros" : "Tarjeta",
            Fecha = DateTime.Today, CuentaId = cuenta, TarjetaCreditoId = tarjeta, PagoDeTarjetaId = pagoDe
        }, t.UsuarioId);

    [Fact]
    public void IncluyeElSaldoInicialDeLasCuentas()
    {
        using var t = new BaseDePrueba();
        CrearCuenta(t, "Ahorros", 1_500_000);
        CrearCuenta(t, "Mimimi",  1_200_000);

        var vm = new DashboardViewModel(t.Db, t.UsuarioId);

        Assert.Equal(2_700_000m, vm.BalanceTotal);   // antes: 0
    }

    /// <summary>Por construcción: lo que hay en las cuentas es lo que dice la pantalla Cuentas.</summary>
    [Fact]
    public void CuadraConLaPantallaCuentasMasElEfectivo()
    {
        using var t = new BaseDePrueba();
        var ahorros = CrearCuenta(t, "Ahorros", 1_500_000);
        Mov(t, "Gasto",   25_000, cuenta: ahorros);
        Mov(t, "Ingreso", 10_000);                  // efectivo, sin cuenta
        Mov(t, "Gasto",    4_000);                  // efectivo

        var vm = new DashboardViewModel(t.Db, t.UsuarioId);

        var enCuentas = t.Db.ObtenerCuentas(t.UsuarioId).Sum(c => c.SaldoActual);
        Assert.Equal(1_475_000m, enCuentas);
        Assert.Equal(enCuentas + 6_000m, vm.BalanceTotal);
        Assert.Contains("En cuentas ₡1.475.000", vm.DesgloseDisponibleTexto);
        Assert.Contains("En efectivo ₡6.000", vm.DesgloseDisponibleTexto);
    }

    /// <summary>Eje efectivo (D2): la compra con tarjeta no mueve dinero; pagar la tarjeta sí.</summary>
    [Fact]
    public void LaCompraConTarjetaNoRestaYElPagoSi()
    {
        using var t = new BaseDePrueba();
        var ahorros = CrearCuenta(t, "Ahorros", 100_000);
        var tarjeta = t.Db.ObtenerTarjetas(t.UsuarioId).First().Id;

        Mov(t, "Gasto", 30_000, tarjeta: tarjeta);
        Assert.Equal(100_000m, new DashboardViewModel(t.Db, t.UsuarioId).BalanceTotal);

        Mov(t, "Gasto", 30_000, cuenta: ahorros, pagoDe: tarjeta);
        Assert.Equal(70_000m, new DashboardViewModel(t.Db, t.UsuarioId).BalanceTotal);
    }

    [Fact]
    public void IncluyeLasCuentasDesactivadasConDinero()
    {
        using var t = new BaseDePrueba();
        var vieja = CrearCuenta(t, "Vieja", 50_000);
        var c = t.Db.ObtenerCuentas(t.UsuarioId).Single(x => x.Id == vieja);
        c.Activa = false;
        t.Db.ActualizarCuenta(c);

        // Desactivar una cuenta la oculta de los formularios; el dinero sigue existiendo.
        Assert.Equal(50_000m, new DashboardViewModel(t.Db, t.UsuarioId).BalanceTotal);
    }
}
