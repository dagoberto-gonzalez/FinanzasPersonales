using System.Windows;
using FinanzasPersonales.Data;
using FinanzasPersonales.Models;
using FinanzasPersonales.ViewModels;
using Xunit;

namespace FinanzasPersonales.Tests;

/// <summary>
/// El presupuesto mensual. Era una clave global fija en ₡500.000, compartida por todos los
/// usuarios y sin pantalla para cambiarla, que sólo avisaba al pasarse.
/// </summary>
public class PresupuestoTests
{
    private static readonly DateTime Hoy = DateTime.Today;

    private static void Gastar(BaseDePrueba t, decimal monto, int? tarjeta = null, int? pagoDe = null) =>
        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = monto, Categoria = pagoDe is null ? "Alimentación" : "Tarjeta",
            Fecha = Hoy, TarjetaCreditoId = tarjeta, PagoDeTarjetaId = pagoDe
        }, t.UsuarioId);

    // ── Base de datos ──

    [Fact]
    public void UnUsuarioNuevoNoTienePresupuesto()
    {
        using var t = new BaseDePrueba();
        Assert.Equal(0m, t.Db.ObtenerPresupuesto(t.UsuarioId));
    }

    [Fact]
    public void CadaUsuarioTieneElSuyo()
    {
        using var t = new BaseDePrueba();
        var otro = t.Db.InsertarUsuario("otro" + Guid.NewGuid().ToString("N")[..6], "clave123").Id;

        t.Db.GuardarPresupuesto(t.UsuarioId, 300_000);
        t.Db.GuardarPresupuesto(otro, 800_000);

        Assert.Equal(300_000m, t.Db.ObtenerPresupuesto(t.UsuarioId));
        Assert.Equal(800_000m, t.Db.ObtenerPresupuesto(otro));
    }

    /// <summary>Los usuarios que ya existían conservan el valor global que su Dashboard usaba.</summary>
    [Fact]
    public void AlMigrarLosUsuariosConservanElValorGlobal()
    {
        using var t = new BaseDePrueba();
        t.Sql("ALTER TABLE Usuarios DROP COLUMN PresupuestoMensual");   // como estaba antes
        t.Sql("INSERT OR REPLACE INTO Configuracion (Clave, Valor) VALUES ('presupuesto_mensual', '500000')");

        var db = new AppDatabase(t.Ruta);

        Assert.Equal(500_000m, db.ObtenerPresupuesto(t.UsuarioId));
        Assert.Equal(0, t.Escalar<long>("SELECT COUNT(*) FROM Configuracion WHERE Clave='presupuesto_mensual'"));
    }

    // ── Dashboard ──

    [Fact]
    public void SinPresupuestoSeOfreceDefinirloYNoHayAviso()
    {
        using var t = new BaseDePrueba();
        Gastar(t, 999_999);
        var vm = new DashboardViewModel(t.Db, t.UsuarioId);

        Assert.Equal(Visibility.Visible,   vm.SinPresupuestoVisibility);
        Assert.Equal(Visibility.Collapsed, vm.ConPresupuestoVisibility);
        Assert.Equal(Visibility.Collapsed, vm.BannerPresupuestoVisibility);
    }

    [Fact]
    public void DefinirloDesdeLaTarjetaLoGuardaYMuestraElProgreso()
    {
        using var t = new BaseDePrueba();
        Gastar(t, 120_000);
        var vm = new DashboardViewModel(t.Db, t.UsuarioId);

        vm.EditarPresupuestoCommand.Execute(null);
        Assert.Equal(Visibility.Visible, vm.EdicionPresupuestoVisibility);
        vm.PresupuestoEditado = "400.000";
        vm.GuardarPresupuestoCommand.Execute(null);

        Assert.Equal(400_000m, t.Db.ObtenerPresupuesto(t.UsuarioId));
        Assert.Equal(Visibility.Visible,   vm.ConPresupuestoVisibility);
        Assert.Equal(Visibility.Collapsed, vm.EdicionPresupuestoVisibility);
        Assert.Contains("280.000", vm.PresupuestoRestanteTexto);   // te quedan
        Assert.Equal("#4ADE80", vm.PresupuestoColor);              // verde: 30 %
    }

    [Fact]
    public void DesdeElOchentaPorCientoAvisaEnAmarilloYAlPasarseEnRojo()
    {
        using var t = new BaseDePrueba();
        t.Db.GuardarPresupuesto(t.UsuarioId, 100_000);
        Gastar(t, 85_000);
        var vm = new DashboardViewModel(t.Db, t.UsuarioId);
        Assert.Equal("#FCD34D", vm.PresupuestoColor);
        Assert.Equal(Visibility.Collapsed, vm.BannerPresupuestoVisibility);

        Gastar(t, 20_000);
        vm.Cargar();

        Assert.Equal("#FB7185", vm.PresupuestoColor);
        Assert.Contains("Te pasaste por ₡5.000", vm.PresupuestoRestanteTexto);
        Assert.Equal(Visibility.Visible, vm.BannerPresupuestoVisibility);
    }

    /// <summary>
    /// Se notificaba PresupuestoExcedido pero no BannerPresupuestoVisibility, que es lo que enlaza
    /// la vista: el aviso de "te pasaste" podía no aparecer al volver al Dashboard.
    /// </summary>
    [Fact]
    public void LaVistaSeEnteraCuandoCambiaElAviso()
    {
        using var t = new BaseDePrueba();
        t.Db.GuardarPresupuesto(t.UsuarioId, 100_000);
        var vm = new DashboardViewModel(t.Db, t.UsuarioId);
        var notificadas = new List<string?>();
        vm.PropertyChanged += (_, e) => notificadas.Add(e.PropertyName);

        Gastar(t, 150_000);
        vm.Cargar();

        Assert.Contains(nameof(vm.BannerPresupuestoVisibility), notificadas);
        Assert.Contains(nameof(vm.BarraLlena), notificadas);
        Assert.Contains(nameof(vm.PresupuestoColor), notificadas);
    }

    /// <summary>Eje presupuesto (D2): la compra con tarjeta cuenta; el pago de la tarjeta, no.</summary>
    [Fact]
    public void CuentaLasComprasConTarjetaPeroNoSusPagos()
    {
        using var t = new BaseDePrueba();
        t.Db.GuardarPresupuesto(t.UsuarioId, 100_000);
        var tarjeta = t.Db.ObtenerTarjetas(t.UsuarioId).First().Id;
        Gastar(t, 40_000, tarjeta: tarjeta);
        Gastar(t, 40_000, pagoDe: tarjeta);

        var vm = new DashboardViewModel(t.Db, t.UsuarioId);

        Assert.Contains("₡40.000,00 de ₡100.000,00", vm.PresupuestoUsoTexto);
    }

    [Fact]
    public void DejarloVacioLoQuitaYUnMontoIlegibleNoSeGuarda()
    {
        using var t = new BaseDePrueba();
        t.Db.GuardarPresupuesto(t.UsuarioId, 100_000);
        var vm = new DashboardViewModel(t.Db, t.UsuarioId);

        vm.EditarPresupuestoCommand.Execute(null);
        vm.PresupuestoEditado = "1.200.00";   // ambiguo: Dinero lo rechaza
        vm.GuardarPresupuestoCommand.Execute(null);
        Assert.NotEqual("", vm.PresupuestoError);
        Assert.Equal(100_000m, t.Db.ObtenerPresupuesto(t.UsuarioId));

        vm.PresupuestoEditado = "";
        vm.GuardarPresupuestoCommand.Execute(null);
        Assert.Equal(0m, t.Db.ObtenerPresupuesto(t.UsuarioId));
        Assert.Equal(Visibility.Visible, vm.SinPresupuestoVisibility);
    }
}
