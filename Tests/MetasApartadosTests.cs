using FinanzasPersonales.Data;
using FinanzasPersonales.Models;
using FinanzasPersonales.ViewModels;
using Xunit;

namespace FinanzasPersonales.Tests;

/// <summary>
/// D5: una meta es un apartado ("bolsillo") dentro de una cuenta. Apartar reserva sin sacar
/// dinero; Usar registra el gasto real; Liberar devuelve lo reservado a lo libre. Lo apartado se
/// calcula de sus movimientos, igual que el saldo se calcula de las transacciones (D1).
/// </summary>
public class MetasApartadosTests
{
    private static int CrearCuenta(BaseDePrueba t, string nombre, decimal saldoInicial)
    {
        t.Db.InsertarCuenta(new Cuenta { Nombre = nombre, SaldoInicial = saldoInicial }, t.UsuarioId);
        return t.Db.ObtenerCuentas(t.UsuarioId).First(c => c.Nombre == nombre).Id;
    }

    private static int CrearMeta(BaseDePrueba t, string nombre, int cuentaId, decimal objetivo = 500_000)
    {
        t.Db.InsertarMeta(new MetaAhorro
        {
            Nombre = nombre, MontoObjetivo = objetivo, FechaLimite = DateTime.Today.AddMonths(6), CuentaId = cuentaId
        }, t.UsuarioId);
        return t.Db.ObtenerMetas(t.UsuarioId).First(m => m.Nombre == nombre).Id;
    }

    private static Cuenta Cuenta(BaseDePrueba t, int id) => t.Db.ObtenerCuentas(t.UsuarioId).Single(c => c.Id == id);
    private static MetaAhorro Meta(BaseDePrueba t, int id) => t.Db.ObtenerMetas(t.UsuarioId).Single(m => m.Id == id);

    // ── El ejemplo: una cuenta, dos destinos ──

    [Fact]
    public void ApartarReservaSinSacarDineroDeLaCuenta()
    {
        using var t = new BaseDePrueba();
        var ahorros = CrearCuenta(t, "Ahorros", 300_000);
        var refri   = CrearMeta(t, "Refrigeradora", ahorros);
        var viaje   = CrearMeta(t, "Viaje", ahorros);

        t.Db.ApartarEnMeta(refri, 200_000, t.UsuarioId);
        t.Db.ApartarEnMeta(viaje,  60_000, t.UsuarioId);

        var c = Cuenta(t, ahorros);
        Assert.Equal(300_000m, c.SaldoActual);   // el dinero sigue ahí
        Assert.Equal(260_000m, c.Apartado);
        Assert.Equal( 40_000m, c.Libre);
        Assert.Equal(200_000m, Meta(t, refri).Apartado);
        Assert.Empty(t.Db.ObtenerTransacciones(t.UsuarioId));
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    [Fact]
    public void NoSePuedeApartarMasDeLoLibre()
    {
        using var t = new BaseDePrueba();
        var ahorros = CrearCuenta(t, "Ahorros", 300_000);
        var refri   = CrearMeta(t, "Refrigeradora", ahorros);
        var viaje   = CrearMeta(t, "Viaje", ahorros);
        t.Db.ApartarEnMeta(refri, 250_000, t.UsuarioId);

        var ex = Assert.Throws<InvalidOperationException>(() => t.Db.ApartarEnMeta(viaje, 60_000, t.UsuarioId));

        Assert.Contains("50.000", ex.Message);   // lo libre que queda
        Assert.Equal(0m, Meta(t, viaje).Apartado);
    }

    /// <summary>La refrigeradora se compra: gasto real desde la cuenta, y el apartado baja igual.</summary>
    [Fact]
    public void UsarRegistraElGastoRealYBajaLoApartado()
    {
        using var t = new BaseDePrueba();
        var ahorros = CrearCuenta(t, "Ahorros", 300_000);
        var refri   = CrearMeta(t, "Refrigeradora", ahorros);
        t.Db.ApartarEnMeta(refri, 200_000, t.UsuarioId);

        t.Db.UsarDeMeta(refri, 180_000, "Hogar", "Refri Samsung", new DateTime(2026, 10, 13), t.UsuarioId);

        var tx = t.Db.ObtenerTransacciones(t.UsuarioId).Single();
        Assert.Equal("Gasto", tx.Tipo);
        Assert.Equal(180_000m, tx.Monto);
        Assert.Equal("Hogar", tx.Categoria);
        Assert.Equal("Refri Samsung", tx.Descripcion);
        Assert.Equal(ahorros, tx.CuentaId);

        var c = Cuenta(t, ahorros);
        Assert.Equal(120_000m, c.SaldoActual);
        Assert.Equal( 20_000m, c.Apartado);
        Assert.Equal(100_000m, c.Libre);   // lo libre no cambió: se gastó dinero que ya tenía destino
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    [Fact]
    public void NoSePuedeUsarNiLiberarMasDeLoApartado()
    {
        using var t = new BaseDePrueba();
        var ahorros = CrearCuenta(t, "Ahorros", 300_000);
        var refri   = CrearMeta(t, "Refrigeradora", ahorros);
        t.Db.ApartarEnMeta(refri, 50_000, t.UsuarioId);

        Assert.Throws<InvalidOperationException>(() =>
            t.Db.UsarDeMeta(refri, 60_000, "Hogar", "", DateTime.Today, t.UsuarioId));
        Assert.Throws<InvalidOperationException>(() => t.Db.LiberarDeMeta(refri, 60_000, t.UsuarioId));

        Assert.Empty(t.Db.ObtenerTransacciones(t.UsuarioId));
        Assert.Equal(50_000m, Meta(t, refri).Apartado);
    }

    [Fact]
    public void LiberarDevuelveALoLibreSinGastar()
    {
        using var t = new BaseDePrueba();
        var ahorros = CrearCuenta(t, "Ahorros", 300_000);
        var viaje   = CrearMeta(t, "Viaje", ahorros);
        t.Db.ApartarEnMeta(viaje, 100_000, t.UsuarioId);

        t.Db.LiberarDeMeta(viaje, 30_000, t.UsuarioId);

        var c = Cuenta(t, ahorros);
        Assert.Equal(300_000m, c.SaldoActual);
        Assert.Equal( 70_000m, c.Apartado);
        Assert.Empty(t.Db.ObtenerTransacciones(t.UsuarioId));
    }

    // ── Lo que pasa alrededor ──

    /// <summary>Borrar el gasto de un "Usar" lo deshace: el dinero vuelve a estar apartado.</summary>
    [Fact]
    public void BorrarElGastoDeUnUsoDevuelveElDineroALaMeta()
    {
        using var t = new BaseDePrueba();
        var ahorros = CrearCuenta(t, "Ahorros", 300_000);
        var refri   = CrearMeta(t, "Refrigeradora", ahorros);
        t.Db.ApartarEnMeta(refri, 200_000, t.UsuarioId);
        t.Db.UsarDeMeta(refri, 180_000, "Hogar", "", DateTime.Today, t.UsuarioId);
        var tx = t.Db.ObtenerTransacciones(t.UsuarioId).Single();

        Assert.NotNull(t.Db.MotivoNoEditable(tx.Id));   // editarlo descuadraría la meta
        t.Db.EliminarTransaccion(tx.Id);

        Assert.Equal(200_000m, Meta(t, refri).Apartado);
        Assert.Equal(300_000m, Cuenta(t, ahorros).SaldoActual);
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    [Fact]
    public void BorrarLaMetaLiberaLoApartadoYConservaLoUsado()
    {
        using var t = new BaseDePrueba();
        var ahorros = CrearCuenta(t, "Ahorros", 300_000);
        var refri   = CrearMeta(t, "Refrigeradora", ahorros);
        t.Db.ApartarEnMeta(refri, 200_000, t.UsuarioId);
        t.Db.UsarDeMeta(refri, 50_000, "Hogar", "", DateTime.Today, t.UsuarioId);

        t.Db.EliminarMeta(refri);

        var c = Cuenta(t, ahorros);
        Assert.Equal(0m, c.Apartado);
        Assert.Equal(250_000m, c.SaldoActual);
        Assert.Single(t.Db.ObtenerTransacciones(t.UsuarioId));
        Assert.Equal(0, t.Escalar<long>("SELECT COUNT(*) FROM MovimientosMeta"));
        Assert.Empty(t.Db.VerificarIntegridad());
    }

    [Fact]
    public void UnaCuentaConMetasNoSeElimina()
    {
        using var t = new BaseDePrueba();
        var ahorros = CrearCuenta(t, "Ahorros", 0);
        CrearMeta(t, "Viaje", ahorros);

        var ex = Assert.Throws<InvalidOperationException>(() => t.Db.EliminarCuenta(ahorros));
        Assert.Contains("meta", ex.Message);
    }

    /// <summary>
    /// Gastar por fuera de la meta dinero que estaba apartado deja más reservado que saldo. No es
    /// un error de la base —el gasto ocurrió—, pero las metas prometen dinero que ya no está.
    /// </summary>
    [Fact]
    public void ElVerificadorAvisaSiHayMasApartadoQueSaldo()
    {
        using var t = new BaseDePrueba();
        var ahorros = CrearCuenta(t, "Ahorros", 100_000);
        var viaje   = CrearMeta(t, "Viaje", ahorros);
        t.Db.ApartarEnMeta(viaje, 100_000, t.UsuarioId);
        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = 30_000, Categoria = "Alimentación", Fecha = DateTime.Today, CuentaId = ahorros
        }, t.UsuarioId);

        Assert.Equal(-30_000m, Cuenta(t, ahorros).Libre);
        Assert.Contains(t.Db.VerificarIntegridad(), p => p.Area == "Metas");
    }

    [Fact]
    public void NoSeTocaLaMetaDeOtroUsuario()
    {
        using var t = new BaseDePrueba();
        var ahorros = CrearCuenta(t, "Ahorros", 300_000);
        var refri   = CrearMeta(t, "Refrigeradora", ahorros);
        var otro    = t.Db.InsertarUsuario("otro" + Guid.NewGuid().ToString("N")[..6], "clave123").Id;

        Assert.Throws<InvalidOperationException>(() => t.Db.ApartarEnMeta(refri, 1_000, otro));
        Assert.Equal(0m, Meta(t, refri).Apartado);
    }

    // ── Metas de antes de D5 ──

    private static int MetaVieja(BaseDePrueba t, decimal contador)
    {
        t.Sql($"INSERT INTO MetasAhorro (UsuarioId, Nombre, MontoObjetivo, MontoActual, FechaLimite) " +
              $"VALUES ({t.UsuarioId}, 'Vieja', 500000, {contador}, '2027-01-01')");
        return (int)t.Escalar<long>("SELECT Id FROM MetasAhorro WHERE Nombre='Vieja'");
    }

    /// <summary>Al elegir la cuenta, el contador viejo se convierte en un apartado.</summary>
    [Fact]
    public void AlAsignarCuentaElContadorViejoPasaASerApartado()
    {
        using var t = new BaseDePrueba();
        var ahorros = CrearCuenta(t, "Ahorros", 300_000);
        var vieja   = MetaVieja(t, 80_000);
        Assert.False(Meta(t, vieja).TieneCuenta);
        Assert.Equal(80_000m, Meta(t, vieja).MontoAnterior);

        t.Db.AsignarCuentaAMeta(vieja, ahorros, t.UsuarioId);

        var m = Meta(t, vieja);
        Assert.Equal(ahorros, m.CuentaId);
        Assert.Equal(80_000m, m.Apartado);
        Assert.Equal(0m, m.MontoAnterior);
        Assert.Equal(220_000m, Cuenta(t, ahorros).Libre);
    }

    [Fact]
    public void SiElContadorViejoNoCabeNoSeAsignaNada()
    {
        using var t = new BaseDePrueba();
        var ahorros = CrearCuenta(t, "Ahorros", 50_000);
        var vieja   = MetaVieja(t, 80_000);

        Assert.Throws<InvalidOperationException>(() => t.Db.AsignarCuentaAMeta(vieja, ahorros, t.UsuarioId));

        var m = Meta(t, vieja);
        Assert.False(m.TieneCuenta);           // todo o nada
        Assert.Equal(80_000m, m.MontoAnterior);
    }

    /// <summary>Una base anterior: la columna y la tabla nuevas se crean sin tocar las metas.</summary>
    [Fact]
    public void LaMigracionDejaLasMetasViejasSinCuentaYConSuMonto()
    {
        using var t = new BaseDePrueba();
        var vieja = MetaVieja(t, 80_000);
        t.Sql("DROP TABLE MovimientosMeta; ALTER TABLE MetasAhorro DROP COLUMN CuentaId;");

        var db = new AppDatabase(t.Ruta);

        var m = db.ObtenerMetas(t.UsuarioId).Single(x => x.Id == vieja);
        Assert.False(m.TieneCuenta);
        Assert.Equal(80_000m, m.MontoAnterior);
        Assert.Empty(db.VerificarIntegridad());
    }

    // ── Pantalla ──

    [Fact]
    public void CrearUnaMetaExigeCuentaYElErrorDeApartarSaleEnSuTarjeta()
    {
        using var t = new BaseDePrueba();
        CrearCuenta(t, "Ahorros", 100_000);
        var vm = new MetasViewModel(t.Db, t.UsuarioId);

        vm.Nombre = "Viaje"; vm.MontoObjetivo = "150.000";
        vm.CuentaNueva = null;
        vm.CrearMetaCommand.Execute(null);
        Assert.Contains("cuenta", vm.MensajeError);

        vm.CuentaNueva = vm.Cuentas.Single();
        vm.CrearMetaCommand.Execute(null);
        var tarjeta = vm.Metas.Single();

        tarjeta.Monto = "120.000";               // más de lo que hay
        tarjeta.ApartarCommand.Execute(null);
        Assert.NotEqual("", vm.Metas.Single().MensajeError);
        Assert.Equal(0m, Meta(t, tarjeta.Id).Apartado);

        tarjeta = vm.Metas.Single();
        tarjeta.Monto = "60.000";
        tarjeta.ApartarCommand.Execute(null);
        Assert.Equal(60_000m, vm.Metas.Single().Apartado);
        Assert.Contains("40.000", vm.Metas.Single().LibreTexto);
    }

    [Fact]
    public void LaTarjetaDelDashboardMuestraLoApartado()
    {
        using var t = new BaseDePrueba();
        var ahorros = CrearCuenta(t, "Ahorros", 300_000);
        var viaje   = CrearMeta(t, "Viaje", ahorros);
        t.Db.ApartarEnMeta(viaje, 75_000, t.UsuarioId);

        var vm = new DashboardViewModel(t.Db, t.UsuarioId);

        Assert.Equal(System.Windows.Visibility.Visible, vm.ApartadoMetasVisibility);
        Assert.Contains("75.000", vm.ApartadoMetasTexto);
    }
}
