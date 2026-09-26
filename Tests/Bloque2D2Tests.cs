using System.IO;
using FinanzasPersonales.Models;
using Xunit;

namespace FinanzasPersonales.Tests;

/// <summary>
/// Decisión D2 — una compra con tarjeta es un gasto en el momento de comprar (eje presupuesto)
/// aunque el dinero no salga hasta pagarla (eje efectivo). Pagar la tarjeta mueve efectivo pero
/// no es un gasto nuevo: ya se contó al comprar.
/// </summary>
public class Bloque2D2Tests
{
    /// <summary>
    /// Monta el caso real: dos compras con tarjeta en setiembre y el pago de la tarjeta en
    /// octubre desde una cuenta.
    /// </summary>
    private static (int CuentaId, int TarjetaId) Escenario(BaseDePrueba t)
    {
        t.Db.InsertarCuenta(new Cuenta { Nombre = "Principal", SaldoInicial = 500000 }, t.UsuarioId);
        var cuentaId  = t.Db.ObtenerCuentas(t.UsuarioId).Single().Id;
        var tarjetaId = (int)t.Escalar<long>("SELECT Id FROM TarjetasCredito LIMIT 1");

        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = 25000, Categoria = "Alimentación", Descripcion = "Cena",
            Fecha = new DateTime(2026, 9, 3), TarjetaCreditoId = tarjetaId
        }, t.UsuarioId);

        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = 60000, Categoria = "Alimentación", Descripcion = "Súper",
            Fecha = new DateTime(2026, 9, 10), TarjetaCreditoId = tarjetaId
        }, t.UsuarioId);

        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = 85000, Categoria = "Tarjeta", Descripcion = "Pago tarjeta",
            Fecha = new DateTime(2026, 10, 5), CuentaId = cuentaId, PagoDeTarjetaId = tarjetaId
        }, t.UsuarioId);

        return (cuentaId, tarjetaId);
    }

    // ── Eje presupuesto: en qué se gastó ──

    [Fact]
    public void LasComprasConTarjetaCuentanEnElMesEnQueSeCompro()
    {
        using var t = new BaseDePrueba();
        Escenario(t);

        var setiembre = t.Db.ObtenerTotalesMes(t.UsuarioId, 2026, 9);

        // Antes de D2 esto daba 0: las compras con tarjeta se excluían de todos los totales.
        Assert.Equal(85000m, setiembre.Gastos);
        Assert.Equal(2,      setiembre.NumTrans);
    }

    [Fact]
    public void PagarLaTarjetaNoCuentaComoGastoNuevo()
    {
        using var t = new BaseDePrueba();
        Escenario(t);

        var octubre = t.Db.ObtenerTotalesMes(t.UsuarioId, 2026, 10);

        // El gasto ya se contó en setiembre; contarlo otra vez sería duplicarlo.
        Assert.Equal(0m, octubre.Gastos);
        Assert.Equal(0,  octubre.NumTrans);
    }

    [Fact]
    public void ElGastoQuedaEnSuCategoriaReal_NoEnTarjeta()
    {
        using var t = new BaseDePrueba();
        Escenario(t);

        var porCategoria = t.Db.ObtenerTransacciones(t.UsuarioId)
            .Where(x => x.AfectaPresupuesto && x.Tipo == "Gasto" && x.Fecha.Year == 2026)
            .GroupBy(x => x.Categoria)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Monto));

        Assert.Equal(85000m, porCategoria["Alimentación"]);
        Assert.DoesNotContain("Tarjeta", porCategoria.Keys);
    }

    // ── Eje efectivo: cuánto dinero hay ──

    [Fact]
    public void ComprarConTarjetaNoMueveElSaldoDeLaCuenta()
    {
        using var t = new BaseDePrueba();
        t.Db.InsertarCuenta(new Cuenta { Nombre = "Principal", SaldoInicial = 500000 }, t.UsuarioId);
        var tarjetaId = (int)t.Escalar<long>("SELECT Id FROM TarjetasCredito LIMIT 1");

        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = 25000, Categoria = "Alimentación",
            Fecha = new DateTime(2026, 9, 3), TarjetaCreditoId = tarjetaId
        }, t.UsuarioId);

        Assert.Equal(500000m, t.Db.ObtenerCuentas(t.UsuarioId).Single().SaldoActual);
    }

    [Fact]
    public void PagarLaTarjetaSiMueveElSaldoDeLaCuenta()
    {
        using var t = new BaseDePrueba();
        Escenario(t);

        Assert.Equal(415000m, t.Db.ObtenerCuentas(t.UsuarioId).Single().SaldoActual); // 500.000 - 85.000
    }

    // ── Los dos ejes no se pisan ──

    [Fact]
    public void ElMismoGastoNoSeCuentaDosVeces()
    {
        using var t = new BaseDePrueba();
        Escenario(t);

        var todas = t.Db.ObtenerTransacciones(t.UsuarioId);

        var presupuesto = todas.Where(x => x.AfectaPresupuesto && x.Tipo == "Gasto").Sum(x => x.Monto);
        var efectivo    = todas.Where(x => x.AfectaEfectivo    && x.Tipo == "Gasto").Sum(x => x.Monto);

        Assert.Equal(85000m, presupuesto);  // las dos compras
        Assert.Equal(85000m, efectivo);     // el pago
        Assert.Equal(3, todas.Count);       // tres filas, pero 85.000 en cada eje, no 170.000
    }

    [Fact]
    public void UnaTransaccionNoPuedeSerCompraYPagoALaVez()
    {
        using var t = new BaseDePrueba();
        var tarjetaId = (int)t.Escalar<long>("SELECT Id FROM TarjetasCredito LIMIT 1");

        var ex = Assert.Throws<ArgumentException>(() => t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = 1000, Categoria = "Otros", Fecha = DateTime.Today,
            TarjetaCreditoId = tarjetaId, PagoDeTarjetaId = tarjetaId
        }, t.UsuarioId));

        Assert.Contains("a la vez", ex.Message);
    }

    // ── El cierre de mes y el verificador hablan del mismo eje ──

    [Fact]
    public void ElCierreDeMesRegistraLoGastadoYElAcumuladoEsElEfectivo()
    {
        using var t = new BaseDePrueba();
        Escenario(t);

        t.Db.CerrarMes(t.UsuarioId, 2026, 9);
        var resumen = t.Db.ObtenerResumenesMensuales(t.UsuarioId).Single();

        Assert.Equal(85000m, resumen.Gastos);            // lo gastado en setiembre
        Assert.Equal(0m,     resumen.BalanceAcumulado);  // aún no había salido efectivo

        // El verificador usa el mismo eje que CerrarMes, así que no reporta desfase de cierre.
        // Lo único que queda abierto es el saldo de la tarjeta: sigue siendo un contador que se
        // reinicia en la fecha de corte (§2.1), y eso lo resuelve D3.
        Assert.All(t.Db.VerificarIntegridad(), p => Assert.Equal("Tarjetas", p.Area));
        Assert.DoesNotContain(t.Db.VerificarIntegridad(), p => p.Area == "Cierre mensual");
    }

    [Fact]
    public void ElAbonoDesdeLaPantallaDeTarjetasQuedaMarcadoComoPago()
    {
        using var t = new BaseDePrueba();
        t.Db.InsertarCuenta(new Cuenta { Nombre = "Principal", SaldoInicial = 200000 }, t.UsuarioId);

        var vm = new ViewModels.TarjetasViewModel(t.Db, t.UsuarioId);
        vm.Seleccionada = vm.Tarjetas.First();
        vm.CuentaAbono  = vm.CuentasDisponibles.Single();
        vm.AbonoMonto   = "50.000";
        vm.AbonarCommand.Execute(null);

        var pago = t.Db.ObtenerTransacciones(t.UsuarioId).Single();
        Assert.Equal(vm.Tarjetas.First().Id, pago.PagoDeTarjetaId);
        Assert.False(pago.AfectaPresupuesto);   // no es un gasto nuevo
        Assert.True(pago.AfectaEfectivo);       // pero sí sale dinero
        Assert.Equal(150000m, t.Db.ObtenerCuentas(t.UsuarioId).Single().SaldoActual);
    }

    // ── Un solo camino para registrar un pago de tarjeta ──

    /// <summary>
    /// Sin esta guarda, registrar el pago a mano como un gasto normal lo contaría otra vez:
    /// el gasto ya se registró al comprar con la tarjeta.
    /// </summary>
    [Fact]
    public void UnGastoEnCategoriaTarjetaSinMarcarLaTarjeta_EsRechazado()
    {
        using var t = new BaseDePrueba();
        t.Db.InsertarCuenta(new Cuenta { Nombre = "Principal" }, t.UsuarioId);
        var cuentaId = t.Db.ObtenerCuentas(t.UsuarioId).Single().Id;

        var ex = Assert.Throws<ArgumentException>(() => t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = 85000, Categoria = "Tarjeta", Descripcion = "Pago BAC",
            Fecha = new DateTime(2026, 10, 5), CuentaId = cuentaId
        }, t.UsuarioId));

        Assert.Contains("pantalla Tarjetas", ex.Message);
        Assert.Empty(t.Db.ObtenerTransacciones(t.UsuarioId));
    }

    [Fact]
    public void ElFormularioDeTransaccionesNoOfreceLaCategoriaTarjeta()
    {
        using var t  = new BaseDePrueba();
        var       vm = new ViewModels.TransaccionesViewModel(t.Db, t.UsuarioId);

        Assert.DoesNotContain("Tarjeta", vm.CategoriasForm);

        // Pero sí se puede filtrar por ella para consultar los pagos ya hechos.
        Assert.Contains("Tarjeta", vm.CategoriasFiltro);
    }

    [Fact]
    public void ElPagoDesdeTarjetasSiPuedeUsarLaCategoriaTarjeta()
    {
        using var t = new BaseDePrueba();
        t.Db.InsertarCuenta(new Cuenta { Nombre = "Principal", SaldoInicial = 200000 }, t.UsuarioId);
        var tarjetaId = (int)t.Escalar<long>("SELECT Id FROM TarjetasCredito LIMIT 1");
        var cuentaId  = t.Db.ObtenerCuentas(t.UsuarioId).Single().Id;

        // La vía correcta: lleva PagoDeTarjetaId, así que la guarda la deja pasar.
        t.Db.InsertarTransaccion(new Transaccion
        {
            Tipo = "Gasto", Monto = 85000, Categoria = "Tarjeta", Descripcion = "Pago BAC",
            Fecha = new DateTime(2026, 10, 5), CuentaId = cuentaId, PagoDeTarjetaId = tarjetaId
        }, t.UsuarioId);

        Assert.Single(t.Db.ObtenerTransacciones(t.UsuarioId));
    }

    // ── Migración de bases anteriores ──

    /// <summary>
    /// Sin esto, al migrar una base anterior el gasto se contaría dos veces: una al comprar con
    /// la tarjeta y otra al pagarla, porque el pago viejo no está marcado como tal.
    /// </summary>
    [Fact]
    public void LosPagosDeTarjetaAnterioresSeMarcanAlMigrar()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "fp-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
        var ruta = Path.Combine(carpeta, "vieja.db");

        try
        {
            // Esquema anterior: Transacciones SIN la columna PagoDeTarjetaId.
            using (var c = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={ruta}"))
            {
                c.Open();
                void X(string sql)
                {
                    using var k = c.CreateCommand();
                    k.CommandText = sql;
                    k.ExecuteNonQuery();
                }

                X("""
                  CREATE TABLE TarjetasCredito (
                      Id INTEGER PRIMARY KEY AUTOINCREMENT, UsuarioId INTEGER NOT NULL DEFAULT 1,
                      Nombre TEXT NOT NULL, LimiteCredito REAL NOT NULL DEFAULT 0,
                      SaldoUsado REAL NOT NULL DEFAULT 0, DiaCierre INTEGER NOT NULL DEFAULT 15,
                      DiaPago INTEGER NOT NULL DEFAULT 5, FechaUltimoCorte TEXT NOT NULL DEFAULT '',
                      UNIQUE(UsuarioId, Nombre))
                  """);
                X("""
                  CREATE TABLE Transacciones (
                      Id INTEGER PRIMARY KEY AUTOINCREMENT, UsuarioId INTEGER NOT NULL DEFAULT 1,
                      Tipo TEXT NOT NULL, Monto REAL NOT NULL, Categoria TEXT NOT NULL,
                      Descripcion TEXT NOT NULL DEFAULT '', Fecha TEXT NOT NULL,
                      Notas TEXT NOT NULL DEFAULT '', CuentaNombre TEXT NOT NULL DEFAULT 'Efectivo',
                      TarjetaCreditoId INTEGER, CuentaId INTEGER)
                  """);

                X("INSERT INTO TarjetasCredito (Id,UsuarioId,Nombre) VALUES (7,1,'BAC Economía')");
                // Un pago tal y como lo generaba la versión anterior de Abonar.
                X("""
                  INSERT INTO Transacciones (UsuarioId,Tipo,Monto,Categoria,Descripcion,Fecha)
                  VALUES (1,'Gasto',40000,'Tarjeta','Pago tarjeta BAC Economía','2026-07-01')
                  """);
                // Y una compra normal, que NO debe marcarse como pago.
                X("""
                  INSERT INTO Transacciones (UsuarioId,Tipo,Monto,Categoria,Descripcion,Fecha,TarjetaCreditoId)
                  VALUES (1,'Gasto',15000,'Alimentación','Súper','2026-07-02',7)
                  """);
            }

            var db    = new FinanzasPersonales.Data.AppDatabase(ruta);
            var filas = db.ObtenerTransacciones(1);

            var pago = filas.Single(x => x.Descripcion.StartsWith("Pago tarjeta"));
            Assert.Equal(7, pago.PagoDeTarjetaId);
            Assert.False(pago.AfectaPresupuesto);   // si no, se contaría dos veces

            var compra = filas.Single(x => x.Descripcion == "Súper");
            Assert.Null(compra.PagoDeTarjetaId);
            Assert.True(compra.AfectaPresupuesto);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(carpeta, recursive: true); } catch { }
        }
    }
}
